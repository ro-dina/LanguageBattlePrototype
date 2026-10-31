using System;
using System.IO;
using System.Text;
using UnityEngine;

public enum SaveLoadStatus { NewGame, Loaded, Recovered, UnsupportedVersion, Migrated, MigrationFailed }

// File operations are isolated so tests can use private temporary directories.
public sealed class SaveFileStore
{
    public string MainPath { get; }
    public string ActivePath { get; private set; }
    public bool WritesBlocked { get; private set; }
    public string LastError { get; private set; }

    public SaveFileStore(string directory)
    {
        MainPath = Path.Combine(directory, "save.json");
        ActivePath = MainPath;
    }

    public SaveLoadStatus Load(out SaveData data)
    {
        LastError = null;
        WritesBlocked = false;
        ActivePath = MainPath;
        if (File.Exists(MainPath) && TryRead(MainPath, out data)) return SaveLoadStatus.Loaded;
        if (File.Exists(MainPath) && ReadVersion(MainPath) == 1)
        {
            if (TryMigrate(MainPath, out data) && Save(data)) return SaveLoadStatus.Migrated;
            WritesBlocked = true;
            LastError = "Version-1 migration failed; original retained and saving disabled. " + LastError;
            data = NewGameDefaults.Create(); return SaveLoadStatus.MigrationFailed;
        }
        // A newer schema may be perfectly valid. Never replace it with an older game's data.
        if (File.Exists(MainPath) && IsUnsupported(MainPath))
        {
            WritesBlocked = true;
            LastError = "Unsupported save version. Original file retained; saving is disabled for this session.";
            data = NewGameDefaults.Create();
            return SaveLoadStatus.UnsupportedVersion;
        }
        bool damaged = File.Exists(MainPath);
        if (damaged) LastError = "Save could not be loaded. The original file will be preserved: " + MainPath;
        SaveData recovered = null;
        try
        {
            string directory = Path.GetDirectoryName(MainPath);
            if (Directory.Exists(directory))
            {
                var candidates = Directory.GetFiles(directory, "save.recovery-*.json");
                Array.Sort(candidates, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                foreach (string candidate in candidates)
                    if (TryRead(candidate, out recovered) || (ReadVersion(candidate) == 1 && TryMigrate(candidate, out recovered))) { ActivePath = candidate; break; }
            }
            if (recovered == null && !TryRead(MainPath + ".bak", out recovered) && ReadVersion(MainPath + ".bak") == 1)
                TryMigrate(MainPath + ".bak", out recovered);
            if (recovered == null && Directory.Exists(directory))
            {
                var legacyBackups = Directory.GetFiles(directory, "save*.v1.bak*");
                Array.Sort(legacyBackups, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                foreach (string backup in legacyBackups)
                    if (!backup.Contains(".tmp-") && ReadVersion(backup) == 1 && TryMigrate(backup, out recovered)) break;
            }
        }
        catch (Exception exception) { LastError = "Save recovery failed: " + exception.Message; }
        if (damaged && ActivePath == MainPath) ActivePath = RecoveryPath();
        data = recovered ?? NewGameDefaults.Create();
        return damaged || recovered != null ? SaveLoadStatus.Recovered : SaveLoadStatus.NewGame;
    }

    private bool TryRead(string path, out SaveData data)
    {
        data = null;
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 4 * 1024 * 1024) return false;
            if (SaveDataValidator.TryDecode(File.ReadAllText(path), out data, out _)) return true;
            data = null;
            return false;
        }
        catch (Exception) { return false; }
    }

    private int ReadVersion(string path)
    {
        try { return File.Exists(path) && new FileInfo(path).Length <= 4 * 1024 * 1024 ? SaveDataMigration.Version(File.ReadAllText(path)) : 0; }
        catch (Exception) { return 0; }
    }
    private bool IsUnsupported(string path)
    { int version = ReadVersion(path); return version != 0 && version != 1 && version != SaveDataValidator.CurrentVersion; }

    private bool TryMigrate(string path, out SaveData data)
    {
        data = null;
        try
        {
            byte[] original = File.ReadAllBytes(path);
            string json = new UTF8Encoding(false).GetString(original).TrimStart('\uFEFF');
            if (!SaveDataMigration.TryMigrate(json, out data, out string error)) { LastError = error; return false; }
            // Recovery sources that are already protected backups are never modified.
            if (!Path.GetFileName(path).Contains(".v1.bak")) PreserveLegacyBackup(path, original);
            return true;
        }
        catch (Exception exception) { data = null; LastError = "Migration backup failed: " + exception.Message; return false; }
    }

    private void PreserveLegacyBackup(string path, byte[] original)
    {
        string backup = path + ".v1.bak";
        // A prior attempt may already have retained these bytes under a conflict suffix.
        foreach (string candidate in Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(backup) + "*"))
            if (!candidate.Contains(".tmp-") && new FileInfo(candidate).Length == original.Length &&
                Convert.ToBase64String(File.ReadAllBytes(candidate)) == Convert.ToBase64String(original)) return;
        if (File.Exists(backup))
        {
            if (Convert.ToBase64String(File.ReadAllBytes(backup)) == Convert.ToBase64String(original)) return;
            backup += "-" + Guid.NewGuid().ToString("N");
        }
        string temporary = backup + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(original, 0, original.Length); stream.Flush(true); }
            File.Move(temporary, backup);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private string RecoveryPath() => Path.Combine(Path.GetDirectoryName(MainPath),
        "save.recovery-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffff") + "-" + Guid.NewGuid().ToString("N") + ".json");

    public bool Save(SaveData data)
    {
        string temporary = null;
        try
        {
            if (WritesBlocked) return false;
            if (!SaveDataValidator.Validate(data, out string error)) { LastError = error; return false; }
            Directory.CreateDirectory(Path.GetDirectoryName(MainPath));
            if (File.Exists(ActivePath) && !TryRead(ActivePath, out _))
            {
                if (IsUnsupported(ActivePath))
                { WritesBlocked = true; LastError = "Existing save has an unsupported version; preserved."; return false; }
                if (ReadVersion(ActivePath) == 1)
                {
                    if (!TryMigrate(ActivePath, out _)) { WritesBlocked = true; return false; }
                }
                else ActivePath = RecoveryPath(); // Damaged files remain byte-for-byte untouched.
            }
            string json = JsonUtility.ToJson(data, true);
            if (!SaveDataValidator.TryDecode(json, out _, out error)) { LastError = error; return false; }
            temporary = ActivePath + ".tmp-" + Guid.NewGuid().ToString("N");
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(ActivePath))
            {
                string backup = ActivePath + ".bak";
                if (File.Exists(backup) && !TryRead(backup, out _)) backup += "-" + Guid.NewGuid().ToString("N");
                File.Replace(temporary, ActivePath, backup);
            }
            else File.Move(temporary, ActivePath);
            LastError = null;
            return true;
        }
        catch (Exception exception)
        {
            LastError = "Save write failed: " + exception.Message;
            return false;
        }
        finally
        {
            try { if (temporary != null && File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception) { /* A failed temp cleanup must not interrupt gameplay. */ }
        }
    }
}

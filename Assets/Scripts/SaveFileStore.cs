using System;
using System.IO;
using System.Text;
using UnityEngine;

public enum SaveLoadStatus { NewGame, Loaded, Recovered, UnsupportedVersion, Migrated, MigrationFailed, InvalidResetState, Corrupt, ReadFailed }

// File operations are isolated so tests can use private temporary directories.
public sealed class SaveFileStore
{
    public const int MaxBytes = 4 * 1024 * 1024;
    public string MainPath { get; private set; }
    private readonly string rootDirectory;
    public string ActivePath { get; private set; }
    public bool WritesBlocked { get; private set; }
    public string LastError { get; private set; }

    public SaveFileStore(string directory)
    {
        directory = Path.GetFullPath(directory);
        rootDirectory = directory;
        if (SaveGenerationStore.Resolve(directory, out string active, out _)) directory = active;
        MainPath = Path.Combine(directory, "save.json");
        ActivePath = MainPath;
    }

    private bool inspecting, inspectionReadFailed;
    public string RootDirectory => rootDirectory;
    public SaveLoadStatus Load(out SaveData data) => LoadCore(out data, false, true);
    // The title uses exactly the same validation/recovery order, without migrations, backups or writes.
    public SaveLoadStatus Inspect(out SaveData data)
    {
        inspecting = true; inspectionReadFailed = false;
        try
        {
            var status = LoadCore(out data, true, false);
            if (data == null && inspectionReadFailed) { WritesBlocked = true; return SaveLoadStatus.ReadFailed; }
            return status;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        { WritesBlocked = true; LastError = "Progress could not be read: " + e.Message; data = null; return SaveLoadStatus.ReadFailed; }
        finally { inspecting = false; }
    }
    internal SaveLoadStatus LoadExisting(out SaveData data) => LoadCore(out data, false, false);
    private SaveLoadStatus LoadCore(out SaveData data, bool readOnly, bool createDefaults)
    {
        LastError = null;
        WritesBlocked = false;
        if (!SaveGenerationStore.Resolve(rootDirectory, out string directory, out string routeError))
        {
            WritesBlocked = true; LastError = routeError; data = createDefaults ? NewGameDefaults.Create() : null;
            return SaveLoadStatus.InvalidResetState;
        }
        MainPath = Path.Combine(directory, "save.json");
        ActivePath = MainPath;
        if (readOnly)
        {
            if (File.Exists(rootDirectory)) throw new IOException("Progress location is a file.");
            if (Directory.Exists(directory)) Directory.GetFiles(directory); // Do not treat access errors as a missing profile.
        }
        if (File.Exists(MainPath) && TryRead(MainPath, out data)) return SaveLoadStatus.Loaded;
        bool migrationFailed = false;
        if (File.Exists(MainPath) && IsLegacy(MainPath))
        {
            if (TryMigrate(MainPath, out data, !readOnly) && (readOnly || Save(data))) return SaveLoadStatus.Migrated;
            migrationFailed = true; // A valid backup can still recover without replacing this source.
        }
        // A newer schema may be perfectly valid. Never replace it with an older game's data.
        if (File.Exists(MainPath) && IsUnsupported(MainPath))
        {
            WritesBlocked = true;
            LastError = "Unsupported save version. Original file retained; saving is disabled for this session.";
            data = createDefaults ? NewGameDefaults.Create() : null;
            return SaveLoadStatus.UnsupportedVersion;
        }
        bool damaged = File.Exists(MainPath);
        if (damaged) LastError = "Save could not be loaded. The original file will be preserved: " + MainPath;
        SaveData recovered = null;
        try
        {
            string recoveryDirectory = Path.GetDirectoryName(MainPath);
            if (Directory.Exists(recoveryDirectory))
            {
                var candidates = Directory.GetFiles(recoveryDirectory, "save.recovery-*.json");
                Array.Sort(candidates, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                foreach (string candidate in candidates)
                    if (TryRead(candidate, out recovered) || (IsLegacy(candidate) && TryMigrate(candidate, out recovered, !readOnly))) { ActivePath = candidate; break; }
            }
            if (recovered == null && !TryRead(MainPath + ".bak", out recovered) && IsLegacy(MainPath + ".bak"))
                TryMigrate(MainPath + ".bak", out recovered, !readOnly);
            if (recovered == null && Directory.Exists(recoveryDirectory))
            {
                var legacyBackups = Directory.GetFiles(recoveryDirectory, "save*.v*.bak*");
                Array.Sort(legacyBackups, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                foreach (string backup in legacyBackups)
                    if (!backup.Contains(".tmp-") && IsLegacy(backup) && TryMigrate(backup, out recovered, !readOnly)) break;
            }
        }
        catch (Exception exception) { RecordReadFailure(exception); LastError = "Save recovery failed: " + exception.Message; }
        if (migrationFailed && recovered == null)
        {
            WritesBlocked = true;
            LastError = "Legacy migration failed; original retained and saving disabled. " + LastError;
            data = createDefaults ? NewGameDefaults.Create() : null; return SaveLoadStatus.MigrationFailed;
        }
        if (damaged && ActivePath == MainPath) ActivePath = RecoveryPath();
        data = recovered ?? (createDefaults ? NewGameDefaults.Create() : null);
        if (!createDefaults && recovered == null && (damaged || File.Exists(MainPath + ".bak") ||
            Directory.Exists(directory) && Directory.GetFiles(directory, "save*").Length > 0))
        { LastError = LastError ?? "No valid progress could be recovered; original files are retained."; return SaveLoadStatus.Corrupt; }
        return damaged || recovered != null ? SaveLoadStatus.Recovered : SaveLoadStatus.NewGame;
    }

    private bool TryRead(string path, out SaveData data)
    {
        data = null;
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > MaxBytes) return false;
            if (SaveDataValidator.TryDecode(File.ReadAllText(path), out data, out _) && data.version == SaveDataValidator.CurrentVersion) return true;
            data = null;
            return false;
        }
        catch (Exception e) { RecordReadFailure(e); return false; }
    }

    private void RecordReadFailure(Exception error)
    {
        if (inspecting && (error is IOException || error is UnauthorizedAccessException))
        { inspectionReadFailed = true; LastError = "Progress could not be read: " + error.Message; }
    }
    private int ReadVersion(string path)
    {
        try { return File.Exists(path) && new FileInfo(path).Length <= MaxBytes ? SaveDataMigration.Version(File.ReadAllText(path)) : 0; }
        catch (Exception e) { RecordReadFailure(e); return 0; }
    }
    private bool IsLegacy(string path) => ReadVersion(path) == 1 || ReadVersion(path) == 2 || ReadVersion(path) == 3 || ReadVersion(path) == 4 || ReadVersion(path) == 5;
    private bool IsUnsupported(string path)
    { int version = ReadVersion(path); return version != 0 && version != 1 && version != 2 && version != 3 && version != 4 && version != 5 && version != SaveDataValidator.CurrentVersion; }

    private bool TryMigrate(string path, out SaveData data, bool preserveBackup = true)
    {
        data = null;
        try
        {
            byte[] original = File.ReadAllBytes(path);
            string json = new UTF8Encoding(false).GetString(original).TrimStart('\uFEFF');
            if (!SaveDataMigration.TryMigrate(json, out data, out string error)) { LastError = error; return false; }
            // Recovery sources that are already protected backups are never modified.
            if (preserveBackup && !Path.GetFileName(path).Contains(".v1.bak") && !Path.GetFileName(path).Contains(".v2.bak") && !Path.GetFileName(path).Contains(".v3.bak") && !Path.GetFileName(path).Contains(".v4.bak") && !Path.GetFileName(path).Contains(".v5.bak")) PreserveLegacyBackup(path, original);
            return true;
        }
        catch (Exception exception) { RecordReadFailure(exception); data = null; LastError = "Migration backup failed: " + exception.Message; return false; }
    }

    private void PreserveLegacyBackup(string path, byte[] original)
    {
        string backup = path + ".v" + ReadVersion(path) + ".bak";
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
            if (!SaveGenerationStore.Resolve(rootDirectory, out string directory, out string routeError) || directory != Path.GetDirectoryName(MainPath))
            { LastError = routeError ?? "Progress generation changed; reload before saving."; return false; }
            if (!SaveDataValidator.Validate(data, out string error)) { LastError = error; return false; }
            Directory.CreateDirectory(Path.GetDirectoryName(MainPath));
            if (File.Exists(ActivePath) && !TryRead(ActivePath, out _))
            {
                if (IsUnsupported(ActivePath))
                { WritesBlocked = true; LastError = "Existing save has an unsupported version; preserved."; return false; }
                if (IsLegacy(ActivePath))
                {
                    if (!TryMigrate(ActivePath, out var expected)) { WritesBlocked = true; return false; }
                    if (JsonUtility.ToJson(expected) != JsonUtility.ToJson(data))
                    { WritesBlocked = true; LastError = "Legacy source changed or differs from migration; retained."; return false; }
                }
                else ActivePath = RecoveryPath(); // Damaged files remain byte-for-byte untouched.
            }
            string json = JsonUtility.ToJson(data, true);
            if (Encoding.UTF8.GetByteCount(json) > MaxBytes) { LastError = "Save exceeds the existing 4 MB budget; original retained."; return false; }
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

    internal bool CommitReset(SaveData data)
    {
        if (WritesBlocked) { LastError = "Saving is blocked."; return false; }
        if (!SaveDataValidator.Validate(data, out string error)) { LastError = error; return false; }
        // Protect unsupported primary saves even if the caller has not performed Load yet.
        if (IsUnsupported(MainPath)) { WritesBlocked = true; LastError = "Unsupported save version; reset refused."; return false; }
        if (!SaveGenerationStore.Commit(rootDirectory, Path.GetDirectoryName(MainPath), data, out string directory, out error))
        { LastError = error ?? "Progress generation changed; reload before resetting."; return false; }
        MainPath = Path.Combine(directory, "save.json"); ActivePath = MainPath; LastError = null; return true;
    }
}

using System;
using System.IO;
using System.Text;
using UnityEngine;

public enum SaveLoadStatus { NewGame, Loaded, Recovered, UnsupportedVersion }

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
                    if (TryRead(candidate, out recovered)) { ActivePath = candidate; break; }
            }
            if (recovered == null) TryRead(MainPath + ".bak", out recovered);
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

    private bool IsUnsupported(string path)
    {
        try
        {
            if (new FileInfo(path).Length > 4 * 1024 * 1024) return false;
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            return data != null && data.version != 0 && data.version != SaveDataValidator.CurrentVersion;
        }
        catch (Exception) { return false; }
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
                // Even a file damaged after loading remains byte-for-byte untouched.
                ActivePath = RecoveryPath();
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

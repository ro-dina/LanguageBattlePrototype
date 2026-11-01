using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable] public sealed class UserSettingsData
{
    public int version = 1;
    public string uiLanguage = "en";
    public float masterVolume = 1, bgmVolume = 1, sfxVolume = 1, voiceVolume = 1;
    public UserSettingsData Copy() => JsonUtility.FromJson<UserSettingsData>(JsonUtility.ToJson(this));
    public static UserSettingsData Defaults(SystemLanguage language) => new UserSettingsData { uiLanguage = language == SystemLanguage.Japanese ? "ja" : "en" };
}

// This file is deliberately independent of SaveData / player transactions and future progress resets.
public sealed class UserSettingsStore
{
    public readonly string MainPath;
    public string BackupPath => MainPath + ".bak";
    public string TemporaryPath => MainPath + ".tmp";
    public bool WritesBlocked { get; private set; }
    public string LastError { get; private set; }
    public UserSettingsStore(string directory) { MainPath = Path.Combine(directory, "settings.json"); }
    public static bool Valid(UserSettingsData data) => data != null && data.version == 1 &&
        (data.uiLanguage == "en" || data.uiLanguage == "ja") &&
        new[] { data.masterVolume, data.bgmVolume, data.sfxVolume, data.voiceVolume }.All(v => !float.IsNaN(v) && !float.IsInfinity(v) && v >= 0 && v <= 1);
    public static bool Decode(string json, out UserSettingsData data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > 8192 || !json.TrimStart().StartsWith("{")) return false;
        foreach (var key in new[] { "version", "uiLanguage", "masterVolume", "bgmVolume", "sfxVolume", "voiceVolume" })
            if (Regex.Matches(json, "\"" + key + "\"\\s*:").Count != 1) return false;
        try { data = JsonUtility.FromJson<UserSettingsData>(json); return Valid(data); }
        catch (ArgumentException) { return false; }
    }
    private string Read(string path)
    {
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 8192) return null;
        return File.ReadAllText(path, Encoding.UTF8);
    }
    public UserSettingsData Load(SystemLanguage systemLanguage)
    {
        WritesBlocked = false; LastError = null;
        try
        {
            // A future primary, backup or pending write must never be overwritten by this version.
            foreach (string path in new[] { MainPath, TemporaryPath, BackupPath })
            {
                string json = Read(path);
                if (json != null && Regex.IsMatch(json, "\"version\"\\s*:\\s*([2-9]\\d*|1\\d+)(?![\\d.])"))
                { WritesBlocked = true; LastError = "settings.future"; return UserSettingsData.Defaults(systemLanguage); }
            }
            // Existing primary wins over an interrupted replacement. With no valid primary, recover pending/backup.
            foreach (string path in new[] { MainPath, TemporaryPath, BackupPath })
                if (Decode(Read(path), out var data)) return data;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { LastError = "settings.load.failed"; }
        return UserSettingsData.Defaults(systemLanguage);
    }
    public bool Save(UserSettingsData data)
    {
        if (WritesBlocked || !Valid(data)) { LastError = WritesBlocked ? "settings.future" : "settings.invalid"; return false; }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MainPath));
            byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(data, true));
            using (var stream = new FileStream(TemporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (!Decode(Read(TemporaryPath), out _)) throw new IOException("Invalid settings candidate.");
            if (File.Exists(MainPath))
            {
                // Keep corrupt bytes available for repair, and retain a valid recovery backup.
                if (!Decode(Read(MainPath), out _))
                { File.Move(MainPath, MainPath + ".corrupt-" + Guid.NewGuid().ToString("N")); File.Move(TemporaryPath, MainPath); }
                else File.Replace(TemporaryPath, MainPath, BackupPath);
            }
            else File.Move(TemporaryPath, MainPath);
            LastError = null; return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { LastError = "settings.save.failed"; return false; }
    }
}

public static class UserSettings
{
    private static UserSettingsData current;
    private static UserSettingsStore store;
    public static event Action Changed;
    public static string LearningLanguage => "de";
    public static string VoiceLanguage => "de";
    public static UserSettingsData Current { get { Ensure(); return current.Copy(); } }
    public static UserSettingsStore Store { get { Ensure(); return store; } }
    public static string Language { get { Ensure(); return current.uiLanguage; } }
    public static string DefaultDirectory()
    {
#if UNITY_EDITOR
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-languageBattleSettingsDirectory") return args[i + 1];
        string test = UnityEditor.SessionState.GetString(PlayerSaveService.TestDirectorySessionKey, "");
        if (!string.IsNullOrEmpty(test)) return Path.Combine(test, "UserSettings");
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-languageBattleSaveDirectory") return Path.Combine(args[i + 1], "UserSettings");
        if (args.Any(a => a.Equals("-runTests", StringComparison.OrdinalIgnoreCase)))
            return Path.Combine(Path.GetTempPath(), "LanguageBattleSettingsTests", Guid.NewGuid().ToString("N"));
#endif
        return Path.Combine(Application.persistentDataPath, "LanguageBattleSettings");
    }
    private static void Ensure() { if (current == null) Initialize(DefaultDirectory(), Application.systemLanguage); }
    public static void Initialize(string directory, SystemLanguage systemLanguage)
    { store = new UserSettingsStore(directory); current = store.Load(systemLanguage); Publish(); }
    public static bool TryUpdate(UserSettingsData candidate, out string error)
    {
        Ensure(); error = null;
        if (!store.Save(candidate)) { error = store.LastError; return false; }
        current = candidate.Copy(); Publish(); return true;
    }
    private static void Publish()
    { if (Changed != null) foreach (Action listener in Changed.GetInvocationList()) try { listener(); } catch (Exception e) { Debug.LogException(e); } }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { current = null; store = null; Changed = null; }
}

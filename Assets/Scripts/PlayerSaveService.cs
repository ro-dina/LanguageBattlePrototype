using System;
using System.IO;
using UnityEngine;

public sealed class PlayerSaveService : MonoBehaviour
{
    public const string TestDirectorySessionKey = "LanguageBattle.TestSaveDirectory";
    public static PlayerSaveService Instance { get; private set; }
    public SaveFileStore Files { get; private set; }
    public SaveLoadStatus LoadStatus { get; private set; }
    private bool restoring;
    private bool dirty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => Initialize(DefaultDirectory());

    private static string DefaultDirectory()
    {
#if UNITY_EDITOR
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == "-languageBattleSaveDirectory") return args[i + 1];
        string testDirectory = UnityEditor.SessionState.GetString(TestDirectorySessionKey, "");
        if (!string.IsNullOrEmpty(testDirectory)) return testDirectory;
        // Automatic fallback prevents Test Runner from ever loading the real player profile.
        foreach (string arg in args)
            if (string.Equals(arg, "-runTests", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(Path.GetTempPath(), "LanguageBattleTests", Guid.NewGuid().ToString("N"));
#endif
        return Path.Combine(Application.persistentDataPath, "LanguageBattle");
    }

    public static PlayerSaveService Initialize(string directory)
    {
        if (Instance != null) return Instance;
        GameDatabase.EnsureAvailable();
        var service = new GameObject("PlayerSaveService").AddComponent<PlayerSaveService>();
        Instance = service;
        if (Application.isPlaying) DontDestroyOnLoad(service.gameObject);
        service.Files = new SaveFileStore(directory);
        service.Reload();
        PlayerStateChanges.Changed += service.OnStateChanged;
        if (service.LoadStatus != SaveLoadStatus.UnsupportedVersion) service.SaveNow();
        return service;
    }

    private void OnDestroy()
    {
        PlayerStateChanges.Changed -= OnStateChanged;
        if (Instance == this) Instance = null;
    }

    public void Reload()
    {
        restoring = true;
        try
        {
            LoadStatus = Files.Load(out SaveData data);
            if (Files.LastError != null) Debug.LogWarning(Files.LastError);
            using (PlayerStateChanges.BeginBatch())
            {
                InventoryManager.RestoreState(data.inventory);
                OwnedCharacters.RestoreState(data.ownedCharacters);
                CharacterLoadoutStore.RestoreState(data.characterEquipment);
                StageProgress.RestoreState(data.stageProgress);
            }
            GameManager.SelectedCharacterId = data.ownedCharacters.characterIds.Contains(GameManager.SelectedCharacterId)
                ? GameManager.SelectedCharacterId : data.ownedCharacters.characterIds[0];
            GameManager.PlayerCharacterId = GameManager.SelectedCharacterId;
            GameManager.PreviousSceneName = "";
            dirty = false;
        }
        finally { restoring = false; }
    }

    public SaveData Capture() => new SaveData {
        version = SaveDataValidator.CurrentVersion,
        inventory = InventoryManager.CaptureState(),
        ownedCharacters = OwnedCharacters.CaptureState(),
        characterEquipment = CharacterLoadoutStore.CaptureState(),
        stageProgress = StageProgress.CaptureState()
    };

    private void OnStateChanged()
    {
        if (restoring) return;
        dirty = true;
        SaveNow();
    }

    public bool SaveNow()
    {
        bool saved = Files.Save(Capture());
        if (saved) dirty = false;
        else if (!Files.WritesBlocked) Debug.LogWarning(Files.LastError);
        return saved;
    }

    private void OnApplicationPause(bool paused) { if (paused && dirty) SaveNow(); }
    private void OnApplicationQuit() { if (dirty) SaveNow(); }
}

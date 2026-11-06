using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PlayerSaveService : MonoBehaviour
{
    public const string TestDirectorySessionKey = "LanguageBattle.TestSaveDirectory";
    public static PlayerSaveService Instance { get; private set; }
    public SaveFileStore Files { get; private set; }
    public SaveLoadStatus LoadStatus { get; private set; }
    private bool restoring;
    private bool dirty;
    private System.Collections.Generic.List<WordItemConversion> convertedWordItems = new System.Collections.Generic.List<WordItemConversion>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= InitializeGameplay;
        SceneManager.sceneLoaded += InitializeGameplay;
    }
    private static void InitializeGameplay(Scene scene, LoadSceneMode mode)
    {
        // Direct Editor starts in existing gameplay scenes remain supported. Title never creates progress.
        if (scene.name != GameStartService.TitleScene && Instance == null) Initialize(DefaultDirectory());
    }

    public static string DefaultDirectory()
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
        var service = CreateUnloaded(directory);
        service.Reload();
        if (!service.Files.WritesBlocked) service.SaveNow();
        return service;
    }

    internal static PlayerSaveService CreateUnloaded(string directory)
    {
        if (Instance != null) return Instance;
        GameDatabase.EnsureAvailable();
        var service = new GameObject("PlayerSaveService").AddComponent<PlayerSaveService>();
        Instance = service;
        if (Application.isPlaying) DontDestroyOnLoad(service.gameObject);
        service.Files = new SaveFileStore(directory);
        PlayerStateChanges.Changed += service.OnStateChanged;
        return service;
    }

    internal bool ContinueExisting()
    {
        LoadStatus = Files.LoadExisting(out var data);
        if (data == null) return false;
        Restore(data);
        return Files.Save(data); // Keep ordinary migration/recovery persistence, only after explicit Continue.
    }

    internal void ReleaseUnloaded()
    {
        // A failed title action must not leave an autosave subscriber with uninitialized player state.
        dirty = false; PlayerStateChanges.Changed -= OnStateChanged;
        if (Instance == this) Instance = null;
        if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
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
            Restore(data);
        }
        finally { restoring = false; }
    }

    private void Restore(SaveData data)
    {
        restoring = true;
        try
        {
            if (!CharacterInstances.CanRestore(data.characterInstances))
            { Debug.LogWarning("Reload refused: it would remove an active battle instance."); return; }
            using (PlayerStateChanges.BeginBatch())
            {
                CurrencyService.Restore(data.wallet);
                TutorialPreferences.Restore(data.tutorials);
                BattleRewardService.Restore(data.battleRewards);
                InventoryManager.RestoreState(data.inventory);
                CharacterInstances.RestoreState(data.characterInstances);
                CharacterCollection.RestoreState(data.characterCollection);
                CharacterLoadoutStore.RestoreState(data.characterEquipment);
                StageProgress.RestoreState(data.stageProgress);
            }
            convertedWordItems = data.convertedWordItems;
            GameManager.EnsureOwnedSelection();
            GameManager.PreviousSceneName = "";
            dirty = false;
            CurrencyService.PublishCommittedBalance();
        }
        finally { restoring = false; }
    }

    public SaveData Capture() => new SaveData {
        version = SaveDataValidator.CurrentVersion,
        wallet = CurrencyService.CaptureState(), tutorials = TutorialPreferences.CaptureState(),
        battleRewards = BattleRewardService.CaptureState(),
        inventory = InventoryManager.CaptureState(),
        characterInstances = CharacterInstances.CaptureState(),
        characterCollection = CharacterCollection.CaptureState(),
        characterEquipment = CharacterLoadoutStore.CaptureState(),
        stageProgress = StageProgress.CaptureState(),
        convertedWordItems = JsonUtility.FromJson<ConversionHistory>(JsonUtility.ToJson(new ConversionHistory { items = convertedWordItems })).items
    };

    [Serializable] private sealed class ConversionHistory { public System.Collections.Generic.List<WordItemConversion> items; }
    internal bool CommitPrepared(SaveData candidate,out string error)
    {
        error = null;
        if (!Files.Save(candidate)) { error = Files.LastError ?? "Saving is blocked."; return false; }
        restoring = true;
        try { convertedWordItems = candidate.convertedWordItems; PlayerStateTransactions.Apply(candidate); dirty = false; }
        finally { restoring = false; }
        return true;
    }

    internal bool CommitReset(SaveData candidate, out string error)
    {
        error = null;
        if (!Files.CommitReset(candidate)) { error = Files.LastError; return false; }
        restoring = true;
        try
        {
            convertedWordItems = candidate.convertedWordItems; PlayerStateTransactions.Apply(candidate); dirty = false;
            LoadStatus = SaveLoadStatus.Loaded;
        }
        finally { restoring = false; }
        return true;
    }

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

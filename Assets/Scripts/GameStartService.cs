using System;
using System.Linq;
using UnityEngine;

[Serializable] public sealed class LearningLanguageDefinition
{
    public string id, displayName;
    public bool available;
}
[Serializable] internal sealed class LearningLanguageTable { public LearningLanguageDefinition[] languages; }
public static class LearningLanguageCatalog
{
    // UI language and learning language have separate lifecycles. Only German has a playable adapter today.
    public static LearningLanguageDefinition[] All => JsonUtility.FromJson<LearningLanguageTable>(Resources.Load<TextAsset>("Data/learning_languages").text).languages;
    public static bool CanPlay(string id) => id == "de" && All.Any(l => l.id == id && l.available);
}

public sealed class ProgressInspection
{
    public SaveLoadStatus Status { get; }
    public bool CanContinue { get; }
    public bool CanStartNew => Status != SaveLoadStatus.UnsupportedVersion && Status != SaveLoadStatus.InvalidResetState && Status != SaveLoadStatus.ReadFailed && Status != SaveLoadStatus.MigrationFailed;
    public string MessageKey => CanContinue ? (Status == SaveLoadStatus.Loaded ? "title.progress.ready" : "title.progress.recovery") :
        Status == SaveLoadStatus.NewGame ? "title.progress.missing" : Status == SaveLoadStatus.Corrupt ? "title.progress.corrupt" : "title.progress.unavailable";
    internal ProgressInspection(SaveLoadStatus status, SaveData data) { Status = status; CanContinue = data != null; }
}

public static class GameStartService
{
    public const string TitleScene = "Title";
    public const string HomeScene = "GermanyHome";
    public static string ProgressDirectory => PlayerSaveService.Instance != null ? PlayerSaveService.Instance.Files.RootDirectory : PlayerSaveService.DefaultDirectory();
    public static ProgressInspection Inspect()
    {
        GameDatabase.EnsureAvailable();
        var files = new SaveFileStore(ProgressDirectory);
        var status = files.Inspect(out var data);
        return new ProgressInspection(status, data);
    }
    public static bool Continue(out string error)
    {
        error = "title.start.failed";
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != TitleScene || CharacterInstances.HasActiveBattle || !Inspect().CanContinue) return false;
        bool created = PlayerSaveService.Instance == null;
        var service = PlayerSaveService.CreateUnloaded(ProgressDirectory);
        if (!service.ContinueExisting()) { if (created) service.ReleaseUnloaded(); return false; }
        error = null; return true;
    }
    // Called only by the title confirmation's Start button. Reuses the ordinary reset transaction and generation pointer.
    public static bool StartNew(string learningLanguage, out string error)
    {
        error = "title.start.failed";
        if (!LearningLanguageCatalog.CanPlay(learningLanguage) || !Inspect().CanStartNew) return false;
        bool created = PlayerSaveService.Instance == null;
        var service = PlayerSaveService.CreateUnloaded(ProgressDirectory);
        service.Files.Inspect(out _); // Revalidate route/future-version protection before allowing the ordinary reset.
        var request = ProgressResetService.Request(ProgressResetKind.NewGame, out error);
        bool success = request != null && ProgressResetService.TryExecute(request, out error);
        if (!success && created) service.ReleaseUnloaded();
        return success;
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ProgressResetRequest
{
    public ProgressResetKind Kind { get; }
    internal readonly PlayerOperation Operation = new PlayerOperation();
    internal ProgressResetRequest(ProgressResetKind kind) { Kind = kind; }
}

public static class ProgressResetService
{
    private static ProgressResetKind? completion;
    public static bool CanReset(ProgressResetKind kind) => ProgressResetAccess.Allows(kind) && Application.isPlaying &&
        SettingsInstaller.Supports(SceneManager.GetActiveScene().name) && !DialogueUI.IsOpen && TutorialController.Active == null &&
        !CharacterInstances.HasActiveBattle && Object.FindAnyObjectByType<BattleManager>() == null &&
        !PlayerStateTransactions.IsCommitting && PlayerSaveService.Instance != null && !PlayerSaveService.Instance.Files.WritesBlocked;
    public static ProgressResetRequest Request(ProgressResetKind kind, out string error)
    { error = CanReset(kind) ? null : "reset.unavailable"; return error == null ? new ProgressResetRequest(kind) : null; }
    public static bool TryExecute(ProgressResetRequest request, out string error)
    {
        error = null;
        if (request == null || !CanReset(request.Kind)) { error = "reset.unavailable"; return false; }
        if (request.Operation.Committed) { error = "reset.duplicate"; return false; }
        bool success = PlayerStateTransactions.TryResetProgress(request.Kind, request.Operation, out string detail);
        if (!success) { error = "reset.failed"; Debug.LogWarning("Progress reset refused: " + detail); return false; }
        StageSession.Clear(); GameManager.PreviousSceneName = ""; GameManager.EnemyCharacterId = "feuer"; GameManager.IsWin = false;
        return true;
    }
    // Only the active UI requests this after a confirmed, successful save.
    internal static void ShowCompletionAtHome(ProgressResetKind kind) => completion = kind;
    internal static ProgressResetKind? TakeCompletion() { var result = completion; completion = null; return result; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() => completion = null;
}

using System;
using System.Collections.Generic;

[Serializable]
public sealed class StageProgressState
{
    public List<string> unlockedStageIds = new List<string>();
    public List<string> clearedStageIds = new List<string>();
}

// Storage only: unlocking rules, rewards and stage gating remain game design decisions.
public static class StageProgress
{
    private static StageProgressState state = new StageProgressState();

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => state = new StageProgressState();

    public static bool Unlock(string id) => Add(state.unlockedStageIds, id);
    public static bool MarkCleared(string id) => Add(state.clearedStageIds, id);

    private static bool Add(List<string> ids, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || ids.Contains(id)) return false;
        ids.Add(id);
        PlayerStateChanges.Notify();
        return true;
    }

    public static StageProgressState CaptureState() => new StageProgressState {
        unlockedStageIds = new List<string>(state.unlockedStageIds),
        clearedStageIds = new List<string>(state.clearedStageIds)
    };

    public static bool RestoreState(StageProgressState restored)
    {
        if (restored == null || !ValidIds(restored.unlockedStageIds) || !ValidIds(restored.clearedStageIds)) return false;
        state = new StageProgressState {
            unlockedStageIds = new List<string>(restored.unlockedStageIds),
            clearedStageIds = new List<string>(restored.clearedStageIds)
        };
        PlayerStateChanges.Notify();
        return true;
    }

    public static bool ValidIds(List<string> ids)
    {
        if (ids == null) return false;
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in ids)
            if (string.IsNullOrWhiteSpace(id) || !unique.Add(id)) return false;
        return true;
    }
}

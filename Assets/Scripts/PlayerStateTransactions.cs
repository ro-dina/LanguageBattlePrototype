using System;
using System.Collections.Generic;
using UnityEngine;

// Build a detached candidate, validate every section, persist it atomically, then publish one state change.
public static class PlayerStateTransactions
{
    private static bool committing;
    public static bool IsCommitting => committing;
    internal static bool TryResetProgress(ProgressResetKind kind, PlayerOperation operation, out string error)
    {
        error = null;
        if (!ProgressResetService.CanReset(kind) || committing || operation == null || operation.Committed || PlayerSaveService.Instance == null)
        { error = "Reset is unavailable or already committed."; return false; }
        committing = true;
        try
        {
            string before = JsonUtility.ToJson(Capture());
            var candidate = kind == ProgressResetKind.NewGame || kind == ProgressResetKind.DevelopmentNewGame
                ? NewGameDefaults.Create() : JsonUtility.FromJson<SaveData>(before);
            if (kind == ProgressResetKind.Tutorials) candidate.tutorials = new TutorialProgressState();
            if (kind == ProgressResetKind.Stages) candidate.stageProgress = new StageProgressState();
            // No old pending action or receipt can be completed against a reset progress state.
            candidate.battleRewards = new BattleRewardState();
            if (!SaveDataValidator.Validate(candidate, out error) || !CharacterInstances.CanRestore(candidate.characterInstances)) return false;
            if (JsonUtility.ToJson(Capture()) != before) { error = "Progress changed during reset preparation."; return false; }
            if (!PlayerSaveService.Instance.CommitReset(candidate, out error)) return false;
            operation.Committed = true; return true;
        }
        finally { committing = false; }
    }
    public static SaveData Capture() => PlayerSaveService.Instance != null ? PlayerSaveService.Instance.Capture() : new SaveData {
        version = SaveDataValidator.CurrentVersion, wallet = CurrencyService.CaptureState(), tutorials = TutorialPreferences.CaptureState(), battleRewards = BattleRewardService.CaptureState(), inventory = InventoryManager.CaptureState(),
        characterInstances = CharacterInstances.CaptureState(), characterEquipment = CharacterLoadoutStore.CaptureState(),
        characterCollection = CharacterCollection.CaptureState(), stageProgress = StageProgress.CaptureState()
    };
    public static bool TryChange(Func<SaveData,bool> build, out string error, PlayerOperation operation = null)
    {
        error = null;
        if (operation?.Committed == true) { error = "This operation is already committed."; return false; }
        if (committing) { error = "Another ownership transaction is being committed."; return false; }
        committing = true;
        try
        {
            string original = JsonUtility.ToJson(Capture());
            var candidate = JsonUtility.FromJson<SaveData>(original);
            if (!build(candidate)) { error = "The inputs are no longer available or valid."; return false; }
            if (!SaveDataValidator.Validate(candidate,out error) || !CharacterInstances.CanRestore(candidate.characterInstances))
            { error = error ?? "An active battle instance cannot be consumed."; return false; }
            if (JsonUtility.ToJson(Capture()) != original) { error = "Ownership changed during preparation."; return false; }
            bool success;
            if (PlayerSaveService.Instance != null) success = PlayerSaveService.Instance.CommitPrepared(candidate,out error);
            else { Apply(candidate); success = true; }
            if (success && operation != null) operation.Committed = true;
            return success;
        }
        finally { committing = false; }
    }
    internal static void Apply(SaveData data)
    {
        // Prevalidated; no callbacks are published until every section has been replaced.
        using (PlayerStateChanges.BeginBatch())
        {
            CurrencyService.Restore(data.wallet);
            TutorialPreferences.Restore(data.tutorials);
            BattleRewardService.Restore(data.battleRewards);
            CharacterInstances.RestoreState(data.characterInstances);
            CharacterCollection.RestoreState(data.characterCollection);
            CharacterLoadoutStore.RestoreState(data.characterEquipment);
            InventoryManager.RestoreState(data.inventory);
            StageProgress.RestoreState(data.stageProgress);
            GameManager.EnsureOwnedSelection();
        }
        CurrencyService.PublishCommittedBalance();
    }
    internal static CharacterInstance AddCharacter(SaveData data,string characterId,string instanceId = null)
    {
        var instance = new CharacterInstance { instanceId = instanceId ?? CharacterInstances.NewId(), characterId = characterId };
        data.characterInstances.instances.Add(instance);
        if (!data.characterCollection.discoveredCharacterIds.Contains(characterId)) data.characterCollection.discoveredCharacterIds.Add(characterId);
        return instance;
    }
}

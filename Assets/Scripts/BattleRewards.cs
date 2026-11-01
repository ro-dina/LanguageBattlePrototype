using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class BattleRewardSettings
{
    public long victoryCoins;
    public double letterDropChance;
    private static BattleRewardSettings current;
    public static BattleRewardSettings Current
    {
        get
        {
            if (current != null) return current;
            var source = Resources.Load<TextAsset>("Data/battle_rewards");
            var value = source == null ? null : JsonUtility.FromJson<BattleRewardSettings>(source.text);
            if (value == null || value.victoryCoins < 0 || double.IsNaN(value.letterDropChance) || value.letterDropChance < 0 || value.letterDropChance > 1)
                throw new InvalidOperationException("Invalid battle_rewards.json.");
            return current = value;
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() => current = null;
}
public enum BattleRewardStatus { Pending, Victory, Defeat }
[Serializable]
public sealed class BattleRewardRecord
{
    public string battleId;
    public string enemyCharacterId;
    public BattleRewardStatus status;
    public long coins;
    public string stageId;
    public long firstClearCoins;
    public string tutorialId;
    public bool tutorialCompleted;
    public List<InventoryEntry> materials = new List<InventoryEntry>();
    public string characterId;
    public string characterInstanceId;
}
[Serializable]
public sealed class BattleRewardState { public BattleRewardRecord latest; }
public sealed class BattleDropPlan
{
    public readonly List<InventoryEntry> Materials = new List<InventoryEntry>();
    public string CharacterId { get; internal set; }
}
public static class BattleDropGenerator
{
    // Inject one [0,1) sample per Unicode text element for deterministic tests.
    public static BattleDropPlan Roll(CharacterData enemy,double chance,Func<double> sample)
    {
        if (enemy == null || !DynamicMaterials.ValidText(enemy.characterName) || sample == null || double.IsNaN(chance) || chance < 0 || chance > 1)
            throw new ArgumentException("Invalid drop input.");
        var plan = new BattleDropPlan();
        var run = new System.Text.StringBuilder();
        bool all = true;
        Action flush = () => {
            if (run.Length == 0) return;
            var entry = DynamicMaterials.Entry(run.ToString());
            var existing = plan.Materials.Find(e => e.itemId == entry.itemId);
            if (existing == null) plan.Materials.Add(entry); else existing.count++;
            run.Clear();
        };
        var characters = StringInfo.GetTextElementEnumerator(enemy.characterName.Normalize(System.Text.NormalizationForm.FormC));
        while (characters.MoveNext())
        {
            double value = sample();
            if (double.IsNaN(value) || value < 0 || value >= 1) throw new ArgumentException("Drop sample must be in [0,1).");
            if (value < chance) run.Append(characters.GetTextElement());
            else { all = false; flush(); }
        }
        flush();
        if (all) { plan.Materials.Clear(); plan.CharacterId = enemy.id; }
        return plan;
    }
}

// One durable latest ticket, never a list of reclaimable historical IDs. Replaced IDs are refused forever.
public static class BattleRewardService
{
    private static BattleRewardState state = new BattleRewardState();
    private static string plannedId;
    private static BattleDropPlan planned;
    private static PlayerOperation operation;
    private static bool plannedVictory;
    public static BattleRewardState CaptureState() => JsonUtility.FromJson<BattleRewardState>(JsonUtility.ToJson(state));
    public static BattleRewardRecord Latest { get { var r = CaptureState().latest; return string.IsNullOrEmpty(r?.battleId) ? null : r; } }
    internal static void Restore(BattleRewardState value)
    {
        state = JsonUtility.FromJson<BattleRewardState>(JsonUtility.ToJson(value));
        if (state.latest?.battleId != plannedId || state.latest?.status != BattleRewardStatus.Pending) ClearPlan();
    }
    private static void ClearPlan() { plannedId = null; planned = null; operation = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { state = new BattleRewardState(); ClearPlan(); }
    public static bool Begin(string enemyId,out string battleId,out string error,string stageId = null,bool guided = false)
    {
        battleId = null;
        var enemy = GameDatabase.EnsureAvailable().GetCharacter(enemyId);
        if (enemy == null || !DynamicMaterials.ValidText(enemy.characterName)) { error = "Invalid enemy name."; return false; }
        var stage = string.IsNullOrEmpty(stageId) ? null : StageCatalog.Get(stageId);
        if ((!string.IsNullOrEmpty(stageId) && (stage==null || stage.enemyCharacterId!=enemyId || !StageProgress.IsUnlocked(stageId))) ||
            (stage==null && StageCatalog.IsTutorialEnemy(enemyId))) { error="Stage is locked or not selected.";return false; }
        string id = "battle-" + Guid.NewGuid().ToString("N");
        if (!PlayerStateTransactions.TryChange(data => {
            if(stage!=null && !StageProgress.IsUnlocked(stageId,data.stageProgress))return false;
            data.battleRewards.latest = new BattleRewardRecord { battleId=id,enemyCharacterId=enemyId,stageId=stageId,
                tutorialId=guided && TutorialCatalog.IsImplemented(stage?.tutorialId) ? stage.tutorialId : null }; return true; },out error)) return false;
        battleId = id; return true;
    }
    public static bool Complete(string battleId,bool victory,out string error,Func<double> sample = null,bool tutorialCompleted = false)
    {
        error = null;
        var record = Latest;
        if (record == null || record.battleId != battleId) { error = "This battle ticket is no longer eligible."; return false; }
        var status = victory ? BattleRewardStatus.Victory : BattleRewardStatus.Defeat;
        if (record.status != BattleRewardStatus.Pending) return record.status == status;
        if (plannedId == battleId && plannedVictory != victory) { error = "Battle outcome cannot change during a save retry."; return false; }
        if (plannedId != battleId)
        {
            ClearPlan();
            if (victory) planned = BattleDropGenerator.Roll(GameDatabase.Instance.GetCharacter(record.enemyCharacterId),BattleRewardSettings.Current.letterDropChance,
                sample ?? (() => UnityEngine.Random.Range(0,1000000)/1000000d));
            plannedId = battleId; plannedVictory = victory; operation = new PlayerOperation();
        }
        return PlayerStateTransactions.TryChange(data => {
            var latest = data.battleRewards.latest;
            if (latest == null || latest.battleId != battleId || latest.status != BattleRewardStatus.Pending) return false;
            if (victory)
            {
                var stage = string.IsNullOrEmpty(latest.stageId) ? null : StageCatalog.Get(latest.stageId);
                long normal = stage?.repeatClearCoins ?? BattleRewardSettings.Current.victoryCoins;
                long first = 0;
                if (stage!=null && !StageProgress.ApplyVictory(data.stageProgress,stage,battleId,out first)) return false;
                if (normal > long.MaxValue-first) return false;
                if (!RewardGrantService.ApplyTo(data,planned.Materials,normal+first,
                    string.IsNullOrEmpty(planned.CharacterId) ? Array.Empty<string>() : new[] { planned.CharacterId })) return false;
                latest.coins = normal+first;latest.firstClearCoins=first;
                if (tutorialCompleted && !string.IsNullOrEmpty(latest.tutorialId))
                { if (!TutorialPreferences.Complete(data.tutorials,latest.tutorialId)) return false; latest.tutorialCompleted=true; }
                latest.materials = planned.Materials.Select(e => new InventoryEntry(e.itemId,e.count) { text=e.text }).ToList();
                latest.characterId = planned.CharacterId;
                if (!string.IsNullOrEmpty(planned.CharacterId)) latest.characterInstanceId = data.characterInstances.instances.Last().instanceId;
                // First clear, unlocks and tutorial completion are committed with this same receipt.
            }
            latest.status = status; return true;
        },out error,operation);
    }
    public static bool ValidState(BattleRewardState value)
    {
        if (value == null) return false;
        var r = value.latest;
        if (r == null) return true;
        // JsonUtility expands null nested classes into empty objects on round-trip.
        if (string.IsNullOrEmpty(r.battleId)) return string.IsNullOrEmpty(r.enemyCharacterId) && r.status == BattleRewardStatus.Pending && r.coins == 0 &&
            r.materials != null && r.materials.Count == 0 && string.IsNullOrEmpty(r.characterId) && string.IsNullOrEmpty(r.characterInstanceId) &&
            string.IsNullOrEmpty(r.stageId) && r.firstClearCoins==0 && string.IsNullOrEmpty(r.tutorialId) && !r.tutorialCompleted;
        if (r.battleId == null || !r.battleId.StartsWith("battle-",StringComparison.Ordinal) || !Guid.TryParseExact(r.battleId.Substring(7),"N",out _) ||
            !GameDatabase.EnsureAvailable().ContainsCharacter(r.enemyCharacterId) || r.materials == null || r.materials.Count > DynamicMaterials.MaxTextLength || r.coins < 0 || !Enum.IsDefined(typeof(BattleRewardStatus),r.status)) return false;
        if (r.firstClearCoins<0 || r.firstClearCoins>r.coins) return false;
        if (!string.IsNullOrEmpty(r.stageId))
        { var stage=StageCatalog.Get(r.stageId);if(stage==null || stage.enemyCharacterId!=r.enemyCharacterId || (!string.IsNullOrEmpty(r.tutorialId) && stage.tutorialId!=r.tutorialId))return false; }
        else if(r.firstClearCoins!=0 || !string.IsNullOrEmpty(r.tutorialId) || r.tutorialCompleted)return false;
        if(r.tutorialCompleted && (r.status!=BattleRewardStatus.Victory || !TutorialCatalog.IsImplemented(r.tutorialId)))return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var material in r.materials) if (!DynamicMaterials.ValidEntry(material) || !ids.Add(material.itemId)) return false;
        if (r.status != BattleRewardStatus.Victory) return r.coins == 0 && r.firstClearCoins == 0 && !r.tutorialCompleted && r.materials.Count == 0 && string.IsNullOrEmpty(r.characterId) && string.IsNullOrEmpty(r.characterInstanceId);
        if (string.IsNullOrEmpty(r.characterId)) return string.IsNullOrEmpty(r.characterInstanceId);
        // Acquired individuals may later be consumed; a receipt must remain valid afterwards.
        return r.characterId == r.enemyCharacterId && r.materials.Count == 0 && r.characterInstanceId != null &&
            r.characterInstanceId.StartsWith("character-",StringComparison.Ordinal) && Guid.TryParseExact(r.characterInstanceId.Substring(10),"N",out _);
    }
}

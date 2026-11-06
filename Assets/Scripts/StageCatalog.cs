using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable] public sealed class StageBattleModifiers
{
    public int guidedEnemyHp;
    public bool surviveRequiredAttack;
    public bool passiveUntilPractice;
    public bool noEnemyAdverbDuringLesson;
    public string tutorialAdjectiveId;
}
[Serializable] public sealed class StageDefinition
{
    public string stageId;
    public int stageNumber;
    public string displayName;
    public string enemyCharacterId;
    public string[] prerequisiteStageIds;
    public long firstClearCoins;
    public long repeatClearCoins;
    public string tutorialId;
    public string tutorialType;
    public string stageDescription;
    public string enemyBehaviorProfile;
    public StageBattleModifiers battleModifiers;
    public bool isEnabled;
}
[Serializable] public sealed class StageDatabase { public StageDefinition[] stages; }
public static class StageCatalog
{
    private static StageDefinition[] stages;
    public static StageDefinition[] All
    {
        get
        {
            if (stages == null)
            {
                var asset=Resources.Load<TextAsset>("Data/stages");
                var data=asset==null?null:JsonUtility.FromJson<StageDatabase>(asset.text);
                if (!Validate(data?.stages,out string error)) throw new InvalidOperationException(error);
                stages=data.stages;
            }
            return stages.Select(Clone).ToArray();
        }
    }
    private static StageDefinition Clone(StageDefinition s)=>JsonUtility.FromJson<StageDefinition>(JsonUtility.ToJson(s));
    public static StageDefinition Get(string id)=>All.FirstOrDefault(s=>s.stageId==id);
    public static bool IsTutorialEnemy(string id)=>All.Any(s=>s.enemyCharacterId==id && !string.IsNullOrEmpty(s.tutorialId));
    public static bool Validate(StageDefinition[] data,out string error)
    {
        error="Invalid stage master.";
        if(data==null || data.Length==0)return false;
        var ids=new HashSet<string>(StringComparer.Ordinal);var numbers=new HashSet<int>();
        foreach(var s in data)
            if(s==null || string.IsNullOrWhiteSpace(s.stageId) || !ids.Add(s.stageId) || s.stageNumber<0 || !numbers.Add(s.stageNumber) ||
                string.IsNullOrWhiteSpace(s.displayName) || !GameDatabase.EnsureAvailable().ContainsCharacter(s.enemyCharacterId) ||
                s.firstClearCoins<0 || s.repeatClearCoins<0 || s.prerequisiteStageIds==null || s.prerequisiteStageIds.Distinct().Count()!=s.prerequisiteStageIds.Length ||
                s.battleModifiers==null || s.battleModifiers.guidedEnemyHp<0 || (s.enemyBehaviorProfile!="standard" && s.enemyBehaviorProfile!="guided_passive"))return false;
        foreach(var s in data)
        {
            var seen=new HashSet<string>();
            Func<string,bool> visit=null;
            visit=id=>{if(id==s.stageId)return false;if(!seen.Add(id))return true;var next=data.FirstOrDefault(x=>x.stageId==id);return next!=null && next.prerequisiteStageIds.All(visit);};
            if(!s.prerequisiteStageIds.All(visit))return false;
        }
        error=null;return true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>stages=null;
}

// Runtime route and tutorial cursor survive selection/detail scene changes; they are not combat save data.
public static class StageSession
{
    public static string StageId { get; private set; }
    public static bool Guided { get; private set; }
    public static int TutorialCursor { get; internal set; }
    public static string TutorialSkillId { get; internal set; }
    public static string TutorialAdverbId { get; internal set; }
    public static string EquipmentInstanceId { get; private set; }
    public static bool EquipmentChosen { get; internal set; }
    public static bool EquipmentConfirmed { get; internal set; }
    public static bool EquipmentLesson => Guided && !string.IsNullOrEmpty(Stage?.battleModifiers.tutorialAdjectiveId);
    public static bool NeedsEquipmentPreparation(string instanceId)
    {
        if (!EquipmentLesson) return false;
        if (EquipmentInstanceId != instanceId)
        { EquipmentInstanceId = instanceId; EquipmentChosen = EquipmentConfirmed = false; TutorialCursor = 0; }
        if (EquipmentConfirmed && CharacterLoadoutStore.GetLoadout(instanceId)?.adjectiveId != Stage.battleModifiers.tutorialAdjectiveId)
        { EquipmentChosen = EquipmentConfirmed = false; TutorialCursor = 0; }
        return !EquipmentConfirmed;
    }
    public static void StopPreparation() { Guided = false; TutorialCursor = 0; EquipmentChosen = EquipmentConfirmed = false; EquipmentInstanceId = null; }
    public static StageDefinition Stage=>StageCatalog.Get(StageId);
    public static bool Select(string id,bool guided,out string error)
    {
        error=null;var stage=StageCatalog.Get(id);
        if(stage==null || !StageProgress.IsUnlocked(id)){error="This stage is locked or unavailable.";return false;}
        StageId=id;Guided=guided && TutorialCatalog.IsImplemented(stage.tutorialId);TutorialCursor=0;TutorialSkillId=TutorialAdverbId=null;
        EquipmentInstanceId=null;EquipmentChosen=EquipmentConfirmed=false;
        GameManager.EnemyCharacterId=stage.enemyCharacterId;return true;
    }
    public static bool ValidateBattle(out string error, bool requireEquipment = true)
    {
        error=null;
        if(string.IsNullOrEmpty(StageId))
        {if(StageCatalog.IsTutorialEnemy(GameManager.EnemyCharacterId)){error="Select an unlocked stage first.";return false;}return true;}
        var s=Stage;
        if(s==null || !StageProgress.IsUnlocked(StageId) || s.enemyCharacterId!=GameManager.EnemyCharacterId)
        {error="This stage is locked or unavailable.";return false;}
        if(requireEquipment && EquipmentLesson && (NeedsEquipmentPreparation(GameManager.SelectedCharacterInstanceId)))
        {error=Localization.Text("tutorial.equipment.required");return false;}
        return true;
    }
    public static void Clear(){StageId=null;Guided=false;TutorialCursor=0;TutorialSkillId=TutorialAdverbId=null;EquipmentInstanceId=null;EquipmentChosen=EquipmentConfirmed=false;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>Clear();
}

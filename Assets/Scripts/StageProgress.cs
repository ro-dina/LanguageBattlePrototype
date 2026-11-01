using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable] public sealed class StageClearRecord
{
    public string stageId;
    public int clearCount;
    public bool firstClearRewardClaimed;
    public string lastBattleId;
    public string lastClearResult;
}
[Serializable] public sealed class StageProgressState
{
    public List<string> unlockedStageIds=new List<string>();
    public List<string> clearedStageIds=new List<string>();
    public List<StageClearRecord> records=new List<StageClearRecord>();
}
public static class StageProgress
{
    private static StageProgressState state=new StageProgressState();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>state=new StageProgressState();
    public static bool IsUnlocked(string id)=>IsUnlocked(id,state);
    public static bool IsUnlocked(string id,StageProgressState progress)
    {
        var stage=StageCatalog.Get(id);
        return stage!=null && stage.isEnabled && progress!=null && stage.prerequisiteStageIds.All(p=>IsCleared(p,progress));
    }
    public static bool IsCleared(string id)=>IsCleared(id,state);
    public static bool IsCleared(string id,StageProgressState progress)=>progress.clearedStageIds.Contains(id) || progress.records?.Any(r=>r.stageId==id && r.clearCount>0)==true;
    public static int ClearCount(string id)=>state.records?.Find(r=>r.stageId==id)?.clearCount ?? (IsCleared(id)?1:0);
    // Compatibility storage helpers; ordinary UI enters through StageSession and victory transactions.
    public static bool Unlock(string id)=>Add(state.unlockedStageIds,id);
    public static bool MarkCleared(string id)=>Add(state.clearedStageIds,id);
    private static bool Add(List<string> ids,string id){if(string.IsNullOrWhiteSpace(id)||ids.Contains(id))return false;ids.Add(id);PlayerStateChanges.Notify();return true;}
    public static bool ApplyVictory(StageProgressState progress,StageDefinition stage,string battleId,out long firstCoins)
    {
        firstCoins=0;
        if(!IsUnlocked(stage.stageId,progress))return false;
        var record=progress.records.Find(r=>r.stageId==stage.stageId);
        if(record?.lastBattleId==battleId || record?.clearCount==int.MaxValue)return false;
        bool cleared=IsCleared(stage.stageId,progress);
        if(record==null){record=new StageClearRecord{stageId=stage.stageId,clearCount=cleared?1:0,firstClearRewardClaimed=cleared};progress.records.Add(record);}
        if(!record.firstClearRewardClaimed){firstCoins=stage.firstClearCoins;record.firstClearRewardClaimed=true;}
        record.clearCount++;record.lastBattleId=battleId;record.lastClearResult="Victory";
        if(!progress.clearedStageIds.Contains(stage.stageId))progress.clearedStageIds.Add(stage.stageId);
        foreach(var available in StageCatalog.All)
            if(IsUnlocked(available.stageId,progress) && !progress.unlockedStageIds.Contains(available.stageId))progress.unlockedStageIds.Add(available.stageId);
        return true;
    }
    public static void Upgrade(StageProgressState progress)
    {
        progress.records=new List<StageClearRecord>();
        foreach(string id in progress.clearedStageIds)
            if(StageCatalog.Get(id)!=null)progress.records.Add(new StageClearRecord{stageId=id,clearCount=1,firstClearRewardClaimed=true,lastClearResult="Victory"});
    }
    public static StageProgressState CaptureState()=>JsonUtility.FromJson<StageProgressState>(JsonUtility.ToJson(state));
    public static bool RestoreState(StageProgressState restored)
    {
        if(!ValidState(restored))return false;
        state=JsonUtility.FromJson<StageProgressState>(JsonUtility.ToJson(restored));PlayerStateChanges.Notify();return true;
    }
    public static bool ValidState(StageProgressState value)
    {
        if(value==null || !ValidIds(value.unlockedStageIds) || !ValidIds(value.clearedStageIds) || value.records==null)return false;
        var ids=new HashSet<string>();
        foreach(var r in value.records)
            if(r==null || string.IsNullOrWhiteSpace(r.stageId) || !ids.Add(r.stageId) || r.clearCount<=0 || !r.firstClearRewardClaimed ||
                r.lastClearResult!="Victory" || !value.clearedStageIds.Contains(r.stageId) ||
                (!string.IsNullOrEmpty(r.lastBattleId) && (!r.lastBattleId.StartsWith("battle-",StringComparison.Ordinal)||!Guid.TryParseExact(r.lastBattleId.Substring(7),"N",out _))))return false;
        return true;
    }
    public static bool ValidIds(List<string> ids)
    {if(ids==null)return false;var unique=new HashSet<string>(StringComparer.Ordinal);return ids.All(id=>!string.IsNullOrWhiteSpace(id)&&unique.Add(id));}
}

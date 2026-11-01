using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum TutorialStepType { Dialogue, Highlight, WaitForSkillSelection, WaitForAdverbSelection, WaitForEquipmentChange, WaitForModalVerbSelection, WaitForFutureSelection, WaitForAttackResolution, WaitForBattleVictory, Complete }
[Serializable] public sealed class TutorialStep { public string type; public TutorialStepType Kind=>Enum.TryParse(type,out TutorialStepType value)?value:(TutorialStepType)(-1); public string dialogueId; public string target; public string hint; }
[Serializable] public sealed class TutorialDefinition { public string tutorialId; public bool isImplemented; public TutorialStep[] steps; }
[Serializable] public sealed class TutorialDatabase { public TutorialDefinition[] tutorials; }
public static class TutorialCatalog
{
    private static TutorialDefinition[] data;
    public static TutorialDefinition Get(string id)
    {
        if(string.IsNullOrEmpty(id))return null;
        if(data==null)data=JsonUtility.FromJson<TutorialDatabase>(Resources.Load<TextAsset>("Data/tutorials").text).tutorials;
        var value=data.SingleOrDefault(t=>t.tutorialId==id);return value==null?null:JsonUtility.FromJson<TutorialDefinition>(JsonUtility.ToJson(value));
    }
    public static bool IsImplemented(string id)=>Get(id)?.isImplemented==true;
    public static bool Supported(TutorialDefinition tutorial,out string error)
    {
        error=null;
        if(tutorial?.isImplemented!=true || tutorial.steps==null || tutorial.steps.Length==0){error="Detailed guidance is not available yet.";return false;}
        foreach(var step in tutorial.steps)
        {
            if(step==null || !Enum.IsDefined(typeof(TutorialStepType),step.Kind)){error="Unknown tutorial step.";return false;}
            switch(step.Kind)
            {
                case TutorialStepType.Dialogue:case TutorialStepType.Highlight:case TutorialStepType.WaitForSkillSelection:
                case TutorialStepType.WaitForAttackResolution:case TutorialStepType.WaitForBattleVictory:case TutorialStepType.Complete:break;
                default:error="Unsupported tutorial step: "+step.type;return false;
            }
        }
        if(tutorial.steps.Last().Kind!=TutorialStepType.Complete){error="Tutorial has no completion step.";return false;}
        return true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>data=null;
}
public enum TutorialChoice { None, Take, SkipThis, SkipAll }
[Serializable] public sealed class TutorialRecord { public string tutorialId; public bool completed; public bool individuallySkipped; public TutorialChoice lastChoice; }
[Serializable] public sealed class TutorialProgressState { public bool skipAll; public List<TutorialRecord> records=new List<TutorialRecord>(); }
public static class TutorialPreferences
{
    private static TutorialProgressState state=new TutorialProgressState();
    public static bool SkipAll=>state.skipAll;
    public static TutorialRecord Get(string id)=>CaptureState().records.Find(r=>r.tutorialId==id);
    public static bool NeedsChoice(string id)=>!SkipAll && Get(id)?.completed!=true && (Get(id)?.lastChoice is null or TutorialChoice.None);
    public static bool DefaultGuided(string id)=>!SkipAll && Get(id)?.lastChoice==TutorialChoice.Take && Get(id)?.completed!=true && TutorialCatalog.IsImplemented(id);
    public static bool Choose(string id,TutorialChoice choice,out string error)
    {
        if(TutorialCatalog.Get(id)==null || choice==TutorialChoice.None || !Enum.IsDefined(typeof(TutorialChoice),choice)){error="Invalid tutorial choice.";return false;}
        return PlayerStateTransactions.TryChange(data=>{
            var record=data.tutorials.records.Find(t=>t.tutorialId==id);
            if(record==null){record=new TutorialRecord{tutorialId=id};data.tutorials.records.Add(record);}
            record.lastChoice=choice;record.individuallySkipped=choice==TutorialChoice.SkipThis;
            if(choice==TutorialChoice.SkipAll)data.tutorials.skipAll=true;
            return true;
        },out error);
    }
    public static bool SetSkipAll(bool value,out string error)=>PlayerStateTransactions.TryChange(data=>{data.tutorials.skipAll=value;return true;},out error);
    internal static bool Complete(TutorialProgressState progress,string id)
    {
        if(!TutorialCatalog.IsImplemented(id))return false;
        var record=progress.records.Find(t=>t.tutorialId==id);
        if(record==null){record=new TutorialRecord{tutorialId=id};progress.records.Add(record);}
        record.completed=true;return true;
    }
    public static TutorialProgressState CaptureState()=>JsonUtility.FromJson<TutorialProgressState>(JsonUtility.ToJson(state));
    internal static void Restore(TutorialProgressState value)=>state=JsonUtility.FromJson<TutorialProgressState>(JsonUtility.ToJson(value));
    public static bool ValidState(TutorialProgressState value)
    {
        if(value?.records==null)return false;var ids=new HashSet<string>();
        foreach(var r in value.records)
            if(r==null || string.IsNullOrWhiteSpace(r.tutorialId) || r.tutorialId.Length>128 || !ids.Add(r.tutorialId) || !Enum.IsDefined(typeof(TutorialChoice),r.lastChoice) ||
                (r.completed && !TutorialCatalog.IsImplemented(r.tutorialId)))return false;
        return true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>state=new TutorialProgressState();
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public enum BattleAffinity { Normal, Effective, Weak }
[Serializable] public sealed class VerbCollocation
{
    public string lemma;
    public string partOfSpeech;
    public double score;
}
[Serializable] public sealed class NounCollocations
{
    public string characterId;
    public VerbCollocation[] entries;
}
[Serializable] internal sealed class NounCollocationTable { public NounCollocations[] nouns; }

// Corpora/ranked data are master data, not loadout skills or player progress.
public static class CollocationAffinity
{
    private static Dictionary<string,VerbCollocation[]> nouns;
    private static string Key(string value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    private static bool IsVerb(string pos)=>pos=="verb" || pos=="VERB" || pos=="AUX" ||
        pos!=null && (pos.StartsWith("VV",StringComparison.Ordinal) || pos.StartsWith("VA",StringComparison.Ordinal) || pos.StartsWith("VM",StringComparison.Ordinal));
    public static BattleAffinity Classify(IEnumerable<VerbCollocation> source,string infinitive)
    {
        string key=Key(infinitive);if(key==null || source==null)return BattleAffinity.Normal;
        var verbs=source.Where(v=>v!=null && IsVerb(v.partOfSpeech) && Key(v.lemma)!=null && !double.IsNaN(v.score) && !double.IsInfinity(v.score))
            .GroupBy(v=>Key(v.lemma),StringComparer.Ordinal).Select(g=>new {lemma=g.Key,score=g.Max(v=>v.score)}).OrderByDescending(v=>v.score).ToArray();
        int n=verbs.Length,index=Array.FindIndex(verbs,v=>v.lemma==key);
        if(n<2 || index<0)return BattleAffinity.Normal;
        double score=verbs[index].score;
        int first=Array.FindIndex(verbs,v=>v.score==score),last=Array.FindLastIndex(verbs,v=>v.score==score);
        // No arbitrary lexical tie-break: a tie across a boundary remains entirely neutral.
        if(n<5)return first==0 && last==0?BattleAffinity.Effective:BattleAffinity.Normal;
        int k=n/5;
        if(last<k)return BattleAffinity.Effective;
        if(first>=n-k)return BattleAffinity.Weak;
        return BattleAffinity.Normal;
    }
    public static BattleAffinity Resolve(CharacterData defender,SkillData skill)
    {
        var db=GameDatabase.Instance;
        if(defender==null || skill==null || skill.language!="de" || defender.language!="de" || db==null || !db.ContainsSkill(skill.id))return BattleAffinity.Normal;
        string infinitive=!string.IsNullOrWhiteSpace(skill.infinitive)?skill.infinitive:skill.skillName;
        var entries=defender.verbCollocations;
        if(entries==null || entries.Length==0)
        {
            if(nouns==null)
            {
                var data=Resources.Load<TextAsset>("Data/noun_collocations");
                NounCollocationTable table=null;
                if(data!=null)
                {
                    try { table=JsonUtility.FromJson<NounCollocationTable>(data.text); }
                    catch(ArgumentException) { Debug.LogWarning("Invalid noun collocation master data; using normal affinity."); }
                }
                nouns=new Dictionary<string,VerbCollocation[]>(StringComparer.Ordinal);
                foreach(var noun in table?.nouns ?? Array.Empty<NounCollocations>())
                    if(noun!=null && !string.IsNullOrEmpty(noun.characterId))nouns[noun.characterId]=noun.entries;
            }
            nouns.TryGetValue(defender.id ?? "",out entries);
        }
        return Classify(entries,infinitive);
    }
    public static float Multiplier(BattleAffinity value)=>value==BattleAffinity.Effective?1.5f:value==BattleAffinity.Weak?.75f:1f;
    public static string Label(BattleAffinity value)=>Localization.Text(value==BattleAffinity.Effective?"ui.effective":value==BattleAffinity.Weak?"ui.not.effective":"ui.affinity.normal");
    public static string Display(BattleAffinity value)=>value==BattleAffinity.Effective?"<color=#ff5555>"+Label(value)+"</color>":value==BattleAffinity.Weak?"<color=#888888>"+Label(value)+"</color>":Label(value);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>nouns=null;
}

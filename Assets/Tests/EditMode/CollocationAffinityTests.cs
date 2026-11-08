using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
public class CollocationAffinityTests
{
    private static VerbCollocation Entry(string lemma,double score,string pos="verb")=>new VerbCollocation{lemma=lemma,score=score,partOfSpeech=pos};
    private static VerbCollocation[] List(int n)=>Enumerable.Range(1,n).Select(i=>Entry("verb"+i,n-i+1)).ToArray();
    [SetUp] public void Setup()=>GameDatabase.EnsureAvailable();
    [TearDown] public void Cleanup(){if(GameDatabase.Instance!=null)Object.DestroyImmediate(GameDatabase.Instance.gameObject);}
    [TestCase(0,1,BattleAffinity.Normal)][TestCase(1,1,BattleAffinity.Normal)]
    [TestCase(2,1,BattleAffinity.Effective)][TestCase(2,2,BattleAffinity.Normal)]
    [TestCase(3,1,BattleAffinity.Effective)][TestCase(3,3,BattleAffinity.Normal)]
    [TestCase(4,1,BattleAffinity.Effective)][TestCase(4,4,BattleAffinity.Normal)]
    [TestCase(5,1,BattleAffinity.Effective)][TestCase(5,3,BattleAffinity.Normal)][TestCase(5,5,BattleAffinity.Weak)]
    [TestCase(7,1,BattleAffinity.Effective)][TestCase(7,7,BattleAffinity.Weak)]
    [TestCase(10,1,BattleAffinity.Effective)][TestCase(10,2,BattleAffinity.Effective)]
    [TestCase(10,3,BattleAffinity.Normal)][TestCase(10,4,BattleAffinity.Normal)][TestCase(10,5,BattleAffinity.Normal)]
    [TestCase(10,6,BattleAffinity.Normal)][TestCase(10,7,BattleAffinity.Normal)][TestCase(10,8,BattleAffinity.Normal)]
    [TestCase(10,9,BattleAffinity.Weak)][TestCase(10,10,BattleAffinity.Weak)]
    public void PopulationAndFloorBoundaries(int n,int rank,BattleAffinity expected)=>Assert.That(CollocationAffinity.Classify(List(n),"verb"+rank),Is.EqualTo(expected));
    [Test] public void UnknownAndMissingAlwaysRemainNormal()
    {Assert.That(CollocationAffinity.Classify(List(10),"missing"),Is.EqualTo(BattleAffinity.Normal));Assert.That(CollocationAffinity.Classify(null,"verb1"),Is.EqualTo(BattleAffinity.Normal));Assert.That(CollocationAffinity.Classify(List(10),null),Is.EqualTo(BattleAffinity.Normal));}
    [Test] public void PartsOfSpeechInvalidNumbersAndDuplicateVerbsDoNotInflatePopulation()
    {
        var list=new[]{Entry("gehen",20),Entry("laufen",10),Entry("Gehen",15),Entry("Haus",999,"noun"),Entry("schnell",1000,"adverb"),Entry("rot",2000,"adjective"),Entry("bad",double.NaN),Entry("bad2",double.PositiveInfinity),null};
        Assert.That(CollocationAffinity.Classify(list,"gehen"),Is.EqualTo(BattleAffinity.Effective));Assert.That(CollocationAffinity.Classify(list,"laufen"),Is.EqualTo(BattleAffinity.Normal));
    }
    [Test] public void HigherScoreWinsRegardlessOfInputOrderAndDuplicateUsesMaximumRatherThanSum()
    {
        var list=new[]{Entry("gehen",8),Entry("laufen",10),Entry("gehen",8)};
        Assert.That(CollocationAffinity.Classify(list,"laufen"),Is.EqualTo(BattleAffinity.Effective));Assert.That(CollocationAffinity.Classify(list.Reverse(),"laufen"),Is.EqualTo(BattleAffinity.Effective));
    }
    [Test] public void SmallPopulationTiedTopIsEntirelyNormal()
    {var list=new[]{Entry("gehen",10),Entry("laufen",10),Entry("sein",1)};foreach(var v in list)Assert.That(CollocationAffinity.Classify(list,v.lemma),Is.EqualTo(BattleAffinity.Normal));}
    [Test] public void TiesAcrossEitherTwentyPercentBoundaryRemainNeutralAsAWhole()
    {
        var list=List(10);list[2].score=list[1].score;list[7].score=list[8].score;
        Assert.That(CollocationAffinity.Classify(list,"verb1"),Is.EqualTo(BattleAffinity.Effective));
        foreach(string id in new[]{"verb2","verb3","verb8","verb9"})Assert.That(CollocationAffinity.Classify(list,id),Is.EqualTo(BattleAffinity.Normal));
        Assert.That(CollocationAffinity.Classify(list,"verb10"),Is.EqualTo(BattleAffinity.Weak));
    }
    [Test] public void TiedGroupsEntirelyInsideTailKeepTheirSharedAffinity()
    {var list=List(10);list[0].score=list[1].score;list[8].score=list[9].score;Assert.That(CollocationAffinity.Classify(list,"verb1"),Is.EqualTo(BattleAffinity.Effective));Assert.That(CollocationAffinity.Classify(list,"verb2"),Is.EqualTo(BattleAffinity.Effective));Assert.That(CollocationAffinity.Classify(list,"verb9"),Is.EqualTo(BattleAffinity.Weak));}
    [Test] public void AllEqualScoresCannotGrantArbitraryAdvantage()
    {var list=List(10);foreach(var v in list)v.score=5;foreach(var v in list)Assert.That(CollocationAffinity.Classify(list,v.lemma),Is.EqualTo(BattleAffinity.Normal));}
    [Test] public void DefenderRelationsAndSkillInfinitiveDriveDamageIndependentlyOfGenderOrAttackerList()
    {
        var attacker=new CharacterData{id="test-attacker",language="de",grammaticalGender="feminine",attack=12,defense=1,hp=500,skillIds=new[]{"sein"},verbCollocations=new[]{Entry("sein",1)}};
        var defender=new CharacterData{id="test-defender",language="de",grammaticalGender="neuter",defense=5,hp=500,skillIds=new[]{"sein"},verbCollocations=new[]{Entry("sein",30),Entry("werden",20),Entry("haben",10)}};
        var skill=GameDatabase.Instance.GetSkill("sein");Assert.That(CollocationAffinity.Resolve(defender,skill),Is.EqualTo(BattleAffinity.Effective));
        int expected=Mathf.RoundToInt((skill.power+12-5)*1.5f);Assert.That(DamageCalculator.CalculateDamage(attacker,defender,skill),Is.EqualTo(expected));
        attacker.grammaticalGender="masculine";defender.grammaticalGender="masculine";Assert.That(DamageCalculator.CalculateDamage(attacker,defender,skill),Is.EqualTo(expected));
        var unknown=new SkillData{id="unregistered",language="de",skillName="sein"};Assert.That(CollocationAffinity.Resolve(defender,unknown),Is.EqualTo(BattleAffinity.Normal));
        var separated=JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(skill));separated.infinitive="angreifen";separated.separablePrefix="an";defender.verbCollocations=new[]{Entry("angreifen",20,"VVINF"),Entry("sein",1,"VAINF")};Assert.That(CollocationAffinity.Resolve(defender,separated),Is.EqualTo(BattleAffinity.Effective));
    }
    [Test] public void ReservationFreezesAffinityAndExistingRoundedDamageBeforeNormalAction()
    {
        var player=JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(GameDatabase.Instance.GetCharacter("nacht")));var enemy=JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(GameDatabase.Instance.GetCharacter("feuer")));enemy.hp=500;
        enemy.verbCollocations=new[]{Entry("sein",20),Entry("haben",10)};var context=new BattleContext(player,enemy);var action=new BattleAction(BattleSide.Player,GameDatabase.Instance.GetSkill("sein"),tense:BattleTense.Future);
        var reserve=context.Resolve(action);Assert.That(reserve.Affinity,Is.EqualTo(BattleAffinity.Effective));Assert.That(reserve.Damage,Is.Zero);int expected=BattleActionResolver.ScaleDamage(DamageCalculator.CalculateDamage(player,enemy,action.Skill),1.5f);context.Apply(reserve);
        enemy.verbCollocations[0].score=-100;enemy.defense=999;context.SkipEnemyTurn();var pending=context.ResolvePendingAttack();Assert.That(pending.Damage,Is.EqualTo(expected));Assert.That(pending.Affinity,Is.EqualTo(BattleAffinity.Effective));context.Apply(pending);Assert.That(context.TurnState,Is.EqualTo(BattleTurnState.AwaitingPlayer));
    }
}

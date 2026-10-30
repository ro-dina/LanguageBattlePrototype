using NUnit.Framework;
using UnityEngine;

public class BattleRulesTests
{
    [SetUp] public void Setup() => GameDatabase.EnsureAvailable();
    [TearDown] public void Cleanup() { if (GameDatabase.Instance != null) Object.DestroyImmediate(GameDatabase.Instance.gameObject); }
    private CharacterData Character(string id) => JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(GameDatabase.Instance.GetCharacter(id)));
    private SkillData Skill(string id) => GameDatabase.Instance.GetSkill(id);
    private EquipmentData Equipment(string id) => GameDatabase.Instance.GetEquipment(id);

    [Test]
    public void LegacyDamageAndTurnOrderRemainUnchanged()
    {
        var context = new BattleContext(Character("nacht"), Character("feuer"));
        var player = context.Resolve(new BattleAction(BattleSide.Player, Skill("sein")));
        Assert.That(player.CanUse && player.Hit, Is.True);
        Assert.That(player.Damage, Is.EqualTo(14));
        Assert.That(context.Enemy.HP, Is.EqualTo(18), "Presentation does not apply damage early.");
        Assert.That(context.Apply(player), Is.True);
        Assert.That(context.Enemy.HP, Is.EqualTo(4));
        Assert.That(context.Apply(player), Is.False, "A hit cannot apply twice.");
        var enemy = context.Resolve(new BattleAction(BattleSide.Enemy, Skill("brennen")));
        Assert.That(enemy.Damage, Is.EqualTo(4));
        context.Apply(enemy);
        Assert.That(context.Player.HP, Is.EqualTo(19));
        Assert.That(context.Round, Is.EqualTo(2));
        Assert.That(context.TurnState, Is.EqualTo(BattleTurnState.AwaitingPlayer));
    }

    [Test]
    public void AdjectiveAndAdverbBonusesRetainLegacyDamage()
    {
        var context = new BattleContext(Character("nacht"), Character("feuer"), Equipment("schwarz"));
        var hit = context.Resolve(new BattleAction(BattleSide.Player, Skill("sein"), Equipment("sehr")));
        Assert.That(hit.Damage, Is.EqualTo(20));
        context.Apply(hit);
        Assert.That(context.Outcome, Is.EqualTo(BattleOutcome.PlayerWin));
        Assert.That(context.TurnState, Is.EqualTo(BattleTurnState.Finished));
        Assert.That(context.Resolve(new BattleAction(BattleSide.Enemy, Skill("brennen"))).CanUse, Is.False);
    }

    [Test]
    public void EnemyWinAndInvalidActionsAreHandledWithoutPresentation()
    {
        var player = Character("nacht"); player.hp = 1;
        var context = new BattleContext(player, Character("feuer"));
        Assert.That(context.Resolve(new BattleAction(BattleSide.Enemy, Skill("brennen"))).CanUse, Is.False);
        Assert.That(context.Resolve(new BattleAction(BattleSide.Player, Skill("laufen"))).CanUse, Is.False);
        Assert.That(context.Resolve(new BattleAction(BattleSide.Player, null)).CanUse, Is.False);
        context.Apply(context.Resolve(new BattleAction(BattleSide.Player, Skill("sein"))));
        context.Apply(context.Resolve(new BattleAction(BattleSide.Enemy, Skill("brennen"))));
        Assert.That(context.Outcome, Is.EqualTo(BattleOutcome.EnemyWin));
        Assert.That(context.Player.HP, Is.Zero);
    }

    [Test]
    public void AccuracyAndCostRemainCompatibilityData()
    {
        var skill = JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(Skill("sein")));
        skill.accuracy = 0; skill.cost = 999;
        var result = new BattleContext(Character("nacht"), Character("feuer"))
            .Resolve(new BattleAction(BattleSide.Player, skill));
        Assert.That(result.CanUse && result.Hit, Is.True);
        Assert.That(result.Damage, Is.EqualTo(14));
    }

    [TestCase(9, 2, true, true)]
    [TestCase(2, 9, true, false)]
    [TestCase(5, 5, true, true)]
    [TestCase(5, 5, false, false)]
    public void SameAdverbContestUsesSpeedAndOnlyRandomizesTies(int playerSpeed, int enemySpeed, bool tie, bool expected)
    {
        int draws = 0;
        var contest = AdverbContestResolver.Resolve(Equipment("sehr"), Equipment("sehr"), playerSpeed, enemySpeed,
            () => { draws++; return tie; });
        Assert.That(contest.Contested, Is.True);
        Assert.That(contest.PlayerWon, Is.EqualTo(expected));
        Assert.That(contest.PlayerAdverb != null, Is.EqualTo(expected));
        Assert.That(contest.EnemyAdverb != null, Is.EqualTo(!expected));
        Assert.That(draws, Is.EqualTo(playerSpeed == enemySpeed ? 1 : 0));
    }

    [Test]
    public void DifferentAdverbsAndNoAdverbDoNotContest()
    {
        var contest = AdverbContestResolver.Resolve(Equipment("sehr"), Equipment("schnell"), 2, 9,
            () => throw new System.Exception("No tie draw expected"));
        Assert.That(contest.Contested, Is.False);
        Assert.That(contest.PlayerAdverb.id, Is.EqualTo("sehr"));
        Assert.That(contest.EnemyAdverb.id, Is.EqualTo("schnell"));
        Assert.That(AdverbContestResolver.Resolve(null, Equipment("sehr"), 2, 9, () => false).Contested, Is.False);
    }

    [Test]
    public void SentenceDisplayAndSpeechShareGermanVerbSecondOrderAndConjugation()
    {
        var subject = Character("jahr");
        var context = new SentenceContext { Subject = subject, Target = Character("feuer"), Skill = Skill("sein"),
            CharacterEquipment = Equipment("schwarz"), SkillEquipment = Equipment("sehr") };
        var sentence = SentenceGenerator.Generate(context);
        Assert.That(sentence.EquippedSubject, Is.EqualTo("Das schwarze Jahr"));
        Assert.That(sentence.EquippedVerb, Is.EqualTo("ist sehr"));
        Assert.That(sentence.SpeechWords, Is.EqualTo(new[] { "Das", "schwarze", "Jahr", "ist", "sehr", "aktiv" }));
        Assert.That(sentence.SentenceAudioKey, Is.EqualTo("das_schwarze_jahr_ist_sehr_aktiv"));
        subject.grammaticalNumber = "plural";
        Assert.That(SentenceGenerator.Generate(context).BaseVerb, Is.EqualTo("sind"));
        context.Skill = new SkillData { skillName = "fallback" };
        Assert.That(SentenceGenerator.Generate(context).BaseVerb, Is.EqualTo("fallback"));
    }
}

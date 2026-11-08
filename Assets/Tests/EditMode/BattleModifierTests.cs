using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class BattleModifierTests
{
    [SetUp] public void Setup() => GameDatabase.EnsureAvailable();
    [TearDown] public void Cleanup() { if (GameDatabase.Instance != null) Object.DestroyImmediate(GameDatabase.Instance.gameObject); }
    private CharacterData Character(string id) => JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(GameDatabase.Instance.GetCharacter(id)));
    private SkillData Skill => GameDatabase.Instance.GetSkill("sein");
    private BattleContext Battle(int enemyHP = 200, int playerHP = 200)
    {
        var player = Character("nacht"); player.hp = playerHP;
        var enemy = Character("feuer"); enemy.hp = enemyHP;
        // Preserve this modifier fixture's 1.5x affinity through scored verb data, rather than grammatical gender.
        enemy.verbCollocations=new[]{new VerbCollocation{lemma="sein",partOfSpeech="verb",score=2},new VerbCollocation{lemma="haben",partOfSpeech="verb",score=1}};
        return new BattleContext(player, enemy, GameDatabase.Instance.GetEquipment("schwarz"));
    }
    private BattleAction Action(BattleTense tense = BattleTense.Present, BattleModal modal = BattleModal.None) =>
        new BattleAction(BattleSide.Player, Skill, GameDatabase.Instance.GetEquipment("sehr"), tense, modal);
    private BattleActionResult Reserve(BattleContext battle)
    {
        var result = battle.Resolve(Action(BattleTense.Future));
        Assert.That(result.CanUse, Is.True); Assert.That(battle.Apply(result), Is.True); return result;
    }
    private void EnemyTurn(BattleContext battle) => Assert.That(battle.Apply(battle.Resolve(
        new BattleAction(BattleSide.Enemy, GameDatabase.Instance.GetSkill("brennen")))), Is.True);

    [Test] public void FutureReservesZeroDamageAndFreezesSkillAdverbEquipmentAndSentence()
    {
        var battle = Battle(); var sourceSkill = JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(Skill));
        var sourceAdverb = JsonUtility.FromJson<EquipmentData>(JsonUtility.ToJson(GameDatabase.Instance.GetEquipment("sehr")));
        var reservation = battle.Resolve(new BattleAction(BattleSide.Player, sourceSkill, sourceAdverb, BattleTense.Future));
        sourceSkill.skillName = "changed"; sourceSkill.power = 999; sourceAdverb.powerBonus = 999;
        Assert.That(reservation.Damage, Is.Zero); Assert.That(reservation.Hit, Is.False);
        battle.Apply(reservation); Assert.That(battle.Enemy.HP, Is.EqualTo(200));
        Assert.That(battle.PendingAttack.Damage, Is.EqualTo(30));
        battle.Player.Character.attack = 999; battle.Player.Equipment.attackBonus = 999;
        battle.Enemy.Character.defense = 999;
        EnemyTurn(battle);
        Assert.That(battle.TurnState, Is.EqualTo(BattleTurnState.ResolvingPending));
        Assert.That(battle.Resolve(Action()).CanUse, Is.False, "No normal action before the due attack.");
        var due = battle.ResolvePendingAttack();
        Assert.That(due.Damage, Is.EqualTo(30));
        Assert.That(due.Action.Skill.skillName, Is.EqualTo("sein"));
        Assert.That(due.Sentence.Text, Does.Contain("wird").And.Contain("sehr").And.Contain("sein"));
        Assert.That(battle.Apply(due), Is.True); Assert.That(battle.Apply(due), Is.False);
        Assert.That(battle.Enemy.HP, Is.EqualTo(170));
        Assert.That(battle.PendingAttack, Is.Null);
        Assert.That(battle.TurnState, Is.EqualTo(BattleTurnState.AwaitingPlayer));
    }
    [Test] public void DueAttackAndNormalActionShareTheNextTurn()
    {
        var battle = Battle(); Reserve(battle); EnemyTurn(battle);
        battle.Apply(battle.ResolvePendingAttack());
        var normal = battle.Resolve(Action()); Assert.That(normal.CanUse, Is.True);
        battle.Apply(normal); Assert.That(battle.Enemy.HP, Is.EqualTo(150));
        Assert.That(battle.Round, Is.EqualTo(2)); Assert.That(battle.TurnState, Is.EqualTo(BattleTurnState.ResolvingEnemy));
    }
    [Test] public void DueKnockoutFinishesWithoutNormalAction()
    {
        var battle = Battle(enemyHP: 25); Reserve(battle); EnemyTurn(battle);
        battle.Apply(battle.ResolvePendingAttack());
        Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.PlayerWin));
        Assert.That(battle.PendingAttack, Is.Null); Assert.That(battle.Resolve(Action()).CanUse, Is.False);
    }
    [Test] public void DefeatAndCancellationDiscardReservations()
    {
        var battle = Battle(playerHP: 1); Reserve(battle); EnemyTurn(battle);
        Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.EnemyWin));
        Assert.That(battle.PendingAttack, Is.Null); Assert.That(battle.ResolvePendingAttack().CanUse, Is.False);
        battle = Battle(); Reserve(battle); battle.Cancel();
        Assert.That(battle.PendingAttack, Is.Null); Assert.That(battle.ResolvePendingAttack().CanUse, Is.False);
    }
    [Test] public void DuplicateReservationsAndCrossBattleResultsAreRejected()
    {
        var battle = Battle(); var reservation = Reserve(battle);
        Assert.That(battle.Resolve(Action(BattleTense.Future)).CanUse, Is.False);
        EnemyTurn(battle);
        Assert.That(battle.Resolve(Action(BattleTense.Future)).CanUse, Is.False);
        Assert.That(BattleActionResolver.Resolve(battle, Action(BattleTense.Future)).CanUse, Is.False);
        var other = Battle(); other.Resolve(Action());
        Assert.That(other.Apply(reservation), Is.False);
    }
    [Test] public void MuessenDoublesOnceAndResetsForEachBattleWithoutChangingOrdinarySkills()
    {
        var battle = Battle(); var result = battle.Resolve(Action(modal: BattleModal.Muessen));
        Assert.That(result.Damage, Is.EqualTo(40)); Assert.That(battle.MuessenUsed, Is.True, "The accepted command spends the use before its presentation.");
        battle.Apply(result); Assert.That(battle.MuessenUsed, Is.True); EnemyTurn(battle);
        Assert.That(battle.Resolve(Action(modal: BattleModal.Muessen)).CanUse, Is.False);
        Assert.That(battle.Resolve(Action()).CanUse, Is.True);
        var fresh = Battle(); Assert.That(fresh.MuessenUsed, Is.False);
        Assert.That(fresh.Resolve(Action(modal: BattleModal.Muessen)).CanUse, Is.True);
    }
    [TestCase("werden")][TestCase("koennen")][TestCase("sollen")]
    public void LegacyModalNamedSkillsAreOrdinaryAttacks(string id)
    {
        var battle = Battle(); battle.Player.Character.skillIds = new[] { id };
        var skill = GameDatabase.Instance.GetSkill(id);
        int expected = DamageCalculator.CalculateDamage(battle.Player.Character, battle.Enemy.Character, skill, battle.Player.Equipment);
        var result = battle.Resolve(new BattleAction(BattleSide.Player, skill)); battle.Apply(result);
        Assert.That(result.Damage, Is.EqualTo(expected)); Assert.That(result.IsReservation, Is.False);
        Assert.That(battle.PendingAttack, Is.Null); Assert.That(battle.MuessenUsed, Is.False);
    }
    [Test] public void SimultaneousModifiersAreRejectedWithoutConsumingAnything()
    {
        var battle = Battle(); var result = battle.Resolve(Action(BattleTense.Future, BattleModal.Muessen));
        Assert.That(result.CanUse, Is.False); Assert.That(battle.Apply(result), Is.False);
        Assert.That(battle.MuessenUsed, Is.False); Assert.That(battle.PendingAttack, Is.Null);
        Assert.That(battle.TurnState, Is.EqualTo(BattleTurnState.AwaitingPlayer));
    }
    [TestCase(1, 2)][TestCase(3, 4)][TestCase(5, 8)][TestCase(7, 10)][TestCase(14, 21)]
    public void FutureRoundingMatchesNearestEven(int damage, int expected) =>
        Assert.That(BattleActionResolver.ScaleDamage(damage, 1.5f), Is.EqualTo(expected));

    [TestCase(BattleTense.Present, BattleModal.None, "Die Nacht ist sehr aktiv.")]
    [TestCase(BattleTense.Future, BattleModal.None, "Die Nacht wird sehr aktiv sein.")]
    [TestCase(BattleTense.Present, BattleModal.Muessen, "Die Nacht muss sehr aktiv sein.")]
    public void ExistingCopulaSkillsKeepTheirComplementInEveryForm(BattleTense tense, BattleModal modal, string expected)
    {
        var sentence = SentenceGenerator.Generate(new SentenceContext { Subject = Character("nacht"), Target = Character("feuer"),
            Skill = Skill, SkillEquipment = GameDatabase.Instance.GetEquipment("sehr"), Tense = tense, Modal = modal });
        Assert.That(sentence.Text, Is.EqualTo(expected));
    }
    [Test] public void SelectionHistoryCapturesChangesClearsAndFrozenFinalActionWithoutAffectingRules()
    {
        var selection = new BattleSelection();
        selection.Select(BattleElementKind.Adverb, "schnell");
        selection.Select(BattleElementKind.Adverb, "sehr");
        selection.Select(BattleElementKind.Tense, "futur1");
        selection.ClearModifiers();
        selection.Select(BattleElementKind.Modal, "muessen");
        var snapshot = selection.Commit("sein");
        Assert.That(snapshot.History.Count, Is.EqualTo(7));
        Assert.That(snapshot.History[1].Change, Is.EqualTo(BattleSelectionChange.Changed));
        Assert.That(snapshot.History[1].PreviousId, Is.EqualTo("schnell"));
        Assert.That(snapshot.History[3].Change, Is.EqualTo(BattleSelectionChange.Cleared));
        Assert.That(snapshot.History[6].Kind, Is.EqualTo(BattleElementKind.Skill));
        for (int i = 0; i < snapshot.History.Count; i++) Assert.That(snapshot.History[i].Order, Is.EqualTo(i + 1));
        selection.ClearModifiers(); Assert.That(snapshot.Elements[BattleElementKind.Modal], Is.EqualTo("muessen"));
        Assert.That(snapshot.Elements.ContainsKey(BattleElementKind.Adverb), Is.False);
        Assert.That(snapshot.History.Count, Is.EqualTo(7));
    }
    [Test] public void SwitchingModifiersClearsTheOldSelectionAndKeepsOrderedImmutableHistory()
    {
        var selection = new BattleSelection();
        selection.Select(BattleElementKind.Tense,"futur1");
        selection.Select(BattleElementKind.Modal,"muessen");
        Assert.That(selection.Tense, Is.EqualTo(BattleTense.Present));
        Assert.That(selection.Modal, Is.EqualTo(BattleModal.Muessen));
        selection.Select(BattleElementKind.Tense,"futur1");
        Assert.That(selection.Modal, Is.EqualTo(BattleModal.None));
        var snapshot = selection.Commit("sein");
        Assert.That(snapshot.History.Select(e => e.Order), Is.EqualTo(new[] {1,2,3,4,5,6}));
        Assert.That(snapshot.History[1].Kind, Is.EqualTo(BattleElementKind.Tense));
        Assert.That(snapshot.History[1].Change, Is.EqualTo(BattleSelectionChange.Cleared));
        Assert.That(snapshot.History[1].PreviousId, Is.EqualTo("futur1"));
        Assert.That(snapshot.History[3].Kind, Is.EqualTo(BattleElementKind.Modal));
        Assert.That(snapshot.Elements.ContainsKey(BattleElementKind.Modal), Is.False);
        selection.ClearModifiers(); Assert.That(snapshot.Elements[BattleElementKind.Tense], Is.EqualTo("futur1"));
    }
}

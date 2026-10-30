using System;
using UnityEngine;

public enum BattleSide { Player, Enemy }
public enum BattleTurnState { AwaitingPlayer, ResolvingPlayer, ResolvingEnemy, Finished, ResolvingPending }
public enum BattleOutcome { Ongoing, PlayerWin, EnemyWin, Cancelled }

public sealed class BattleAction
{
    public BattleSide Side { get; }
    public SkillData Skill { get; }
    public EquipmentData SkillEquipment { get; }
    public BattleTense Tense { get; }
    public BattleModal Modal { get; }
    public BattleSelectionSnapshot Selection { get; }
    public BattleAction(BattleSide side, SkillData skill, EquipmentData skillEquipment = null,
        BattleTense tense = BattleTense.Present, BattleModal modal = BattleModal.None,
        BattleSelectionSnapshot selection = null)
    {
        Side = side; Skill = Copy(skill); SkillEquipment = Copy(skillEquipment);
        Tense = tense; Modal = modal; Selection = selection;
    }
    private static T Copy<T>(T data) where T : class => data == null ? null : JsonUtility.FromJson<T>(JsonUtility.ToJson(data));
}

public sealed class BattleActionResult
{
    public BattleAction Action { get; }
    public bool CanUse { get; }
    public bool Hit => CanUse && !IsReservation;
    public int Damage { get; }
    public int ReservedDamage { get; }
    public bool IsReservation { get; }
    public bool IsPendingAttack { get; }
    public BattleSentence Sentence { get; }
    public string FailureReason { get; }
    internal BattleContext Owner { get; }
    internal bool Applied { get; set; }
    internal BattleActionResult(BattleContext owner, BattleAction action, int damage, string error = null,
        bool reservation = false, int reservedDamage = 0, bool pending = false, BattleSentence sentence = null)
    {
        Owner = owner; Action = action; Damage = damage; FailureReason = error; CanUse = error == null;
        IsReservation = reservation; ReservedDamage = reservedDamage; IsPendingAttack = pending; Sentence = sentence;
    }
}

public sealed class PendingBattleAttack
{
    public BattleAction Action { get; }
    public int Damage { get; }
    public int DueRound { get; }
    public BattleSentence Sentence { get; }
    internal PendingBattleAttack(BattleActionResult result, int dueRound)
    { Action = result.Action; Damage = result.ReservedDamage; DueRound = dueRound; Sentence = result.Sentence; }
}

public sealed class BattleCombatant
{
    public CharacterData Character { get; }
    public EquipmentData Equipment { get; }
    public int HP { get; internal set; }
    public BattleCombatant(CharacterData character, EquipmentData equipment = null)
    { Character = character; Equipment = equipment; HP = character.hp; }
}

// State is independent of coroutines, UI and audio. Transient modifiers are never player save data.
public sealed class BattleContext
{
    public BattleCombatant Player { get; }
    public BattleCombatant Enemy { get; }
    public BattleTurnState TurnState { get; private set; } = BattleTurnState.AwaitingPlayer;
    public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;
    public int Round { get; private set; } = 1;
    public bool MuessenUsed { get; private set; }
    public PendingBattleAttack PendingAttack { get; private set; }
    public BattleContext(CharacterData player, CharacterData enemy, EquipmentData playerEquipment = null)
    { Player = new BattleCombatant(player, playerEquipment); Enemy = new BattleCombatant(enemy); }

    public BattleActionResult Resolve(BattleAction action)
    {
        var result = BattleActionResolver.Resolve(this, action);
        if (result.CanUse && action.Side == BattleSide.Player) TurnState = BattleTurnState.ResolvingPlayer;
        return result;
    }

    public BattleActionResult ResolvePendingAttack()
    {
        if (Outcome != BattleOutcome.Ongoing || TurnState != BattleTurnState.ResolvingPending ||
            PendingAttack == null || Round < PendingAttack.DueRound)
            return new BattleActionResult(this, null, 0, "No pending attack is due.");
        return new BattleActionResult(this, PendingAttack.Action, PendingAttack.Damage,
            pending: true, sentence: PendingAttack.Sentence);
    }

    // Apply after presentation reaches the hit, exactly once. Results cannot cross battles.
    public bool Apply(BattleActionResult result)
    {
        if (result == null || !result.CanUse || result.Applied || result.Owner != this || Outcome != BattleOutcome.Ongoing)
            return false;
        bool player = result.Action.Side == BattleSide.Player;
        var required = result.IsPendingAttack ? BattleTurnState.ResolvingPending :
            player ? BattleTurnState.ResolvingPlayer : BattleTurnState.ResolvingEnemy;
        if (TurnState != required) return false;
        result.Applied = true;
        if (result.IsReservation) PendingAttack = new PendingBattleAttack(result, Round + 1);
        if (player && result.Action.Modal == BattleModal.Muessen) MuessenUsed = true;
        if (result.IsPendingAttack) PendingAttack = null;
        var target = player ? Enemy : Player;
        target.HP = Math.Max(0, target.HP - result.Damage);
        if (target.HP == 0) Finish(player ? BattleOutcome.PlayerWin : BattleOutcome.EnemyWin);
        else if (result.IsPendingAttack) TurnState = BattleTurnState.AwaitingPlayer;
        else if (player) TurnState = BattleTurnState.ResolvingEnemy;
        else FinishEnemyTurn();
        return true;
    }
    public void SkipEnemyTurn() { if (TurnState == BattleTurnState.ResolvingEnemy) FinishEnemyTurn(); }
    public void Cancel() { if (Outcome == BattleOutcome.Ongoing) Finish(BattleOutcome.Cancelled); }
    private void Finish(BattleOutcome outcome)
    { Outcome = outcome; TurnState = BattleTurnState.Finished; PendingAttack = null; }
    private void FinishEnemyTurn()
    {
        Round++;
        TurnState = PendingAttack != null ? BattleTurnState.ResolvingPending : BattleTurnState.AwaitingPlayer;
    }
}

public static class BattleActionResolver
{
    // Match DamageCalculator's nearest-even rounding, after its existing complete calculation.
    public static int ScaleDamage(int damage, float multiplier) => Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
    public static BattleActionResult Resolve(BattleContext context, BattleAction action)
    {
        string error = null;
        if (context == null || action?.Skill == null) error = "Missing battle action data.";
        else if (action.Side != BattleSide.Player && action.Side != BattleSide.Enemy) error = "Unknown actor side.";
        else if (context.Outcome != BattleOutcome.Ongoing) error = "Battle has finished.";
        else if (context.TurnState != (action.Side == BattleSide.Player ? BattleTurnState.AwaitingPlayer : BattleTurnState.ResolvingEnemy))
            error = "Actor cannot act at this point in the turn.";
        else if (!Enum.IsDefined(typeof(BattleTense), action.Tense) || !Enum.IsDefined(typeof(BattleModal), action.Modal))
            error = "Unknown modifier.";
        else if (action.Tense == BattleTense.Future && action.Modal != BattleModal.None)
            error = "Future and müssen cannot be combined yet.";
        else if (action.Side != BattleSide.Player && (action.Tense != BattleTense.Present || action.Modal != BattleModal.None))
            error = "Enemy modifiers are not enabled.";
        else if (action.Tense == BattleTense.Future && context.PendingAttack != null)
            error = "An attack is already reserved.";
        else if (action.Modal == BattleModal.Muessen && context.MuessenUsed)
            error = "Müssen has already been used in this battle.";
        if (error != null) return new BattleActionResult(context, action, 0, error);
        var attacker = action.Side == BattleSide.Player ? context.Player : context.Enemy;
        var defender = action.Side == BattleSide.Player ? context.Enemy : context.Player;
        if (Array.IndexOf(attacker.Character.skillIds ?? Array.Empty<string>(), action.Skill.id) < 0)
            return new BattleActionResult(context, action, 0, "Skill is not equipped by the actor.");
        if ((action.Tense != BattleTense.Present || action.Modal != BattleModal.None) && attacker.Character.language != "de")
            return new BattleActionResult(context, action, 0, "These modifiers require German battle data.");

        // Existing accuracy/cost/effect fields retain their compatibility policy.
        int damage = DamageCalculator.CalculateDamage(attacker.Character, defender.Character, action.Skill,
            attacker.Equipment, action.SkillEquipment, defender.Equipment);
        var sentence = SentenceGenerator.Generate(new SentenceContext {
            Subject = attacker.Character, Target = defender.Character, Skill = action.Skill,
            CharacterEquipment = attacker.Equipment, SkillEquipment = action.SkillEquipment,
            Tense = action.Tense, Modal = action.Modal
        });
        if (action.Tense == BattleTense.Future)
            return new BattleActionResult(context, action, 0, reservation: true,
                reservedDamage: ScaleDamage(damage, 1.5f), sentence: sentence);
        if (action.Modal == BattleModal.Muessen) damage = ScaleDamage(damage, 2f);
        return new BattleActionResult(context, action, damage, sentence: sentence);
    }
}

public sealed class AdverbContestResult
{
    public EquipmentData PlayerAdverb;
    public EquipmentData EnemyAdverb;
    public bool Contested;
    public bool PlayerWon;
}

public static class AdverbContestResolver
{
    public static AdverbContestResult Resolve(EquipmentData player, EquipmentData enemy,
        int playerSpeed, int enemySpeed, Func<bool> playerWinsTie)
    {
        var result = new AdverbContestResult { PlayerAdverb = player, EnemyAdverb = enemy };
        result.Contested = player != null && enemy != null && !string.IsNullOrEmpty(player.id) && player.id == enemy.id;
        if (!result.Contested) return result;
        result.PlayerWon = playerSpeed > enemySpeed || (playerSpeed == enemySpeed && playerWinsTie());
        if (result.PlayerWon) result.EnemyAdverb = null;
        else result.PlayerAdverb = null;
        return result;
    }
}

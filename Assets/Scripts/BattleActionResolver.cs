using System;

public enum BattleSide { Player, Enemy }
public enum BattleTurnState { AwaitingPlayer, ResolvingPlayer, ResolvingEnemy, Finished }
public enum BattleOutcome { Ongoing, PlayerWin, EnemyWin }

public sealed class BattleAction
{
    public BattleSide Side { get; }
    public SkillData Skill { get; }
    public EquipmentData SkillEquipment { get; }
    public BattleAction(BattleSide side, SkillData skill, EquipmentData skillEquipment = null)
    { Side = side; Skill = skill; SkillEquipment = skillEquipment; }
}

public sealed class BattleActionResult
{
    public BattleAction Action { get; }
    public bool CanUse { get; }
    public bool Hit { get; }
    public int Damage { get; }
    public string FailureReason { get; }
    internal BattleContext Owner { get; }
    internal bool Applied { get; set; }

    internal BattleActionResult(BattleContext owner, BattleAction action, int damage, string error = null)
    {
        Owner = owner; Action = action; Damage = damage;
        CanUse = error == null; Hit = CanUse; FailureReason = error;
    }
}

public sealed class BattleCombatant
{
    public CharacterData Character { get; }
    public EquipmentData Equipment { get; }
    public int HP { get; internal set; }
    public BattleCombatant(CharacterData character, EquipmentData equipment = null)
    { Character = character; Equipment = equipment; HP = character.hp; }
}

// State is independent of coroutines, UI, audio and sentence generation.
public sealed class BattleContext
{
    public BattleCombatant Player { get; }
    public BattleCombatant Enemy { get; }
    public BattleTurnState TurnState { get; private set; } = BattleTurnState.AwaitingPlayer;
    public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;
    public int Round { get; private set; } = 1;

    public BattleContext(CharacterData player, CharacterData enemy, EquipmentData playerEquipment = null)
    { Player = new BattleCombatant(player, playerEquipment); Enemy = new BattleCombatant(enemy); }

    public BattleActionResult Resolve(BattleAction action)
    {
        var result = BattleActionResolver.Resolve(this, action);
        if (result.CanUse && action.Side == BattleSide.Player) TurnState = BattleTurnState.ResolvingPlayer;
        return result;
    }

    // Apply after presentation reaches the hit, exactly once. Results cannot cross battles.
    public bool Apply(BattleActionResult result)
    {
        if (result == null || !result.CanUse || result.Applied || result.Owner != this || Outcome != BattleOutcome.Ongoing)
            return false;
        bool player = result.Action.Side == BattleSide.Player;
        if (TurnState != (player ? BattleTurnState.ResolvingPlayer : BattleTurnState.ResolvingEnemy)) return false;
        result.Applied = true;
        BattleCombatant target = player ? Enemy : Player;
        target.HP = Math.Max(0, target.HP - result.Damage);
        if (target.HP == 0)
        {
            Outcome = player ? BattleOutcome.PlayerWin : BattleOutcome.EnemyWin;
            TurnState = BattleTurnState.Finished;
        }
        else if (player) TurnState = BattleTurnState.ResolvingEnemy;
        else FinishEnemyTurn();
        return true;
    }

    public void SkipEnemyTurn()
    { if (TurnState == BattleTurnState.ResolvingEnemy) FinishEnemyTurn(); }

    private void FinishEnemyTurn() { TurnState = BattleTurnState.AwaitingPlayer; Round++; }
}

public static class BattleActionResolver
{
    public static BattleActionResult Resolve(BattleContext context, BattleAction action)
    {
        string error = null;
        if (context == null || action?.Skill == null) error = "Missing battle action data.";
        else if (action.Side != BattleSide.Player && action.Side != BattleSide.Enemy) error = "Unknown actor side.";
        else if (context.Outcome != BattleOutcome.Ongoing) error = "Battle has finished.";
        else if (context.TurnState != (action.Side == BattleSide.Player ? BattleTurnState.AwaitingPlayer : BattleTurnState.ResolvingEnemy))
            error = "Actor cannot act at this point in the turn.";
        if (error != null) return new BattleActionResult(context, action, 0, error);
        var attacker = action.Side == BattleSide.Player ? context.Player : context.Enemy;
        var defender = action.Side == BattleSide.Player ? context.Enemy : context.Player;
        if (Array.IndexOf(attacker.Character.skillIds ?? Array.Empty<string>(), action.Skill.id) < 0)
            return new BattleActionResult(context, action, 0, "Skill is not equipped by the actor.");

        // Compatibility policy: valid skills still hit immediately with no cost.
        // accuracy/cost/effectType/effectValue remain on SkillData for a future opt-in rule migration.
        int damage = DamageCalculator.CalculateDamage(attacker.Character, defender.Character, action.Skill,
            attacker.Equipment, action.SkillEquipment, defender.Equipment);
        return new BattleActionResult(context, action, damage);
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

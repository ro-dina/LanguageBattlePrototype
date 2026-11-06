using System;
using System.Linq;
using UnityEngine;

// Guided-only opponent setup; damage, actor stats and master data remain the normal battle data.
public static class TutorialBattleSetup
{
    public static CharacterData Enemy(StageDefinition stage, CharacterData player, CharacterData master)
    {
        if (!StageSession.Guided || stage == null) return master;
        var result = JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(master));
        result.hp = Math.Max(result.hp, stage.battleModifiers.guidedEnemyHp);
        if (!stage.battleModifiers.surviveRequiredAttack) return result;
        var db = GameDatabase.EnsureAvailable(); var skill = db.GetSkill(player.skillIds[0]);
        var equipment = db.GetEquipment(player.equippedAdjectiveId);
        int damage = DamageCalculator.CalculateDamage(player,result,skill,equipment);
        foreach (var adverb in db.GetAllEquipments().Where(e => DamageCalculator.AppliesToSkill(e)))
            damage = Math.Max(damage,DamageCalculator.CalculateDamage(player,result,skill,equipment,adverb));
        damage = Math.Max(BattleActionResolver.ScaleDamage(damage,2f),BattleActionResolver.ScaleDamage(damage,1.5f));
        result.hp = Math.Max(result.hp, damage < int.MaxValue ? damage + 1 : int.MaxValue);
        return result;
    }
}

using UnityEngine;

public static class DamageCalculator
{
    public static int CalculateDamage(
        CharacterData attacker,
        CharacterData defender,
        SkillData skill,
        EquipmentData attackerCharacterEquipment = null,
        EquipmentData skillEquipment = null,
        EquipmentData defenderCharacterEquipment = null)
    {
        int attackerAttack = attacker.attack + GetCharacterAttackBonus(attackerCharacterEquipment);
        int defenderDefense = defender.defense + GetCharacterDefenseBonus(defenderCharacterEquipment);
        int skillPower = skill.power + GetSkillPowerBonus(skillEquipment);

        int baseDamage = skillPower + attackerAttack - defenderDefense;

        if (baseDamage < 1)
        {
            baseDamage = 1;
        }

        float genderMultiplier = GetGenderMultiplier(attacker.grammaticalGender, defender.grammaticalGender);
        int finalDamage = Mathf.RoundToInt(baseDamage * genderMultiplier);

        if (finalDamage < 1)
        {
            finalDamage = 1;
        }

        return finalDamage;
    }

    public static float GetGenderMultiplierForLog(string attackerGender, string defenderGender)
    {
        return GetGenderMultiplier(attackerGender, defenderGender);
    }

    public static int GetCharacterAttackBonus(EquipmentData equipment)
    {
        if (!AppliesToCharacter(equipment)) return 0;
        return equipment.attackBonus;
    }

    public static int GetCharacterDefenseBonus(EquipmentData equipment)
    {
        if (!AppliesToCharacter(equipment)) return 0;
        return equipment.defenseBonus;
    }

    public static int GetCharacterSpeedBonus(EquipmentData equipment)
    {
        if (!AppliesToCharacter(equipment)) return 0;
        return equipment.speedBonus;
    }

    public static int GetSkillPowerBonus(EquipmentData equipment)
    {
        if (!AppliesToSkill(equipment)) return 0;
        return equipment.powerBonus;
    }

    public static int GetSkillAccuracyBonus(EquipmentData equipment)
    {
        if (!AppliesToSkill(equipment)) return 0;
        return equipment.accuracyBonus;
    }

    public static bool AppliesToCharacter(EquipmentData equipment)
    {
        if (equipment == null) return false;
        return equipment.targetRole == "character" || equipment.targetRole == "both";
    }

    public static bool AppliesToSkill(EquipmentData equipment)
    {
        if (equipment == null) return false;
        return equipment.targetRole == "skill" || equipment.targetRole == "both";
    }

    private static float GetGenderMultiplier(string attackerGender, string defenderGender)
    {
        if (attackerGender == "masculine" && defenderGender == "feminine") return 1.5f;
        if (attackerGender == "feminine" && defenderGender == "neuter") return 1.5f;
        if (attackerGender == "neuter" && defenderGender == "masculine") return 1.5f;

        if (attackerGender == "feminine" && defenderGender == "masculine") return 0.75f;
        if (attackerGender == "neuter" && defenderGender == "feminine") return 0.75f;
        if (attackerGender == "masculine" && defenderGender == "neuter") return 0.75f;

        return 1.0f;
    }
}

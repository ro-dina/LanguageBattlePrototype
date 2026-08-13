using System;
using UnityEngine;

[Serializable]
public class CharacterData
{
    public string id;
    public string characterName;
    public string language;
    public string grammaticalGender;
    public string grammaticalNumber;
    public int rarity;
    public string illustration;

    public int hp;
    public int attack;
    public int defense;
    public int speed;

    public string[] skillIds;

    public string equippedArticleId;
    public string equippedAdjectiveId;
    public string equippedCaseMarkerId;

    public CharacterSkillSlotData[] skillSlots;
}


[Serializable]
public class SkillData
{
    public string id;
    public string skillName;
    public string language;

    public int power;
    public int accuracy;
    public int cost;

    public string targetType;
    public string effectType;
    public int effectValue;
    public SkillConjugations conjugations;
}

[Serializable]
public class SkillConjugations
{
    public string firstPersonSingular;
    public string secondPersonSingular;
    public string thirdPersonSingular;
    public string firstPersonPlural;
    public string secondPersonPlural;
    public string thirdPersonPlural;
    public string formalSecondPerson;
}

[Serializable]
public class EquipmentData
{
    public string id;
    public string equipmentName;
    public string language;
    public string partOfSpeech;
    public string targetRole;

    public int attackBonus;
    public int defenseBonus;
    public int speedBonus;
    public int powerBonus;
    public int accuracyBonus;

    public string equipmentCategory;
    public string caseRole;
    public string articleType;
    public string validGender;
    public string validNumber;

    public int criticalRateBonus;
    public float equipmentEffectRateBonus;
    public float buffReceiveRateBonus;
    public int receivedDefenseBonus;

    public string description;
}

[Serializable]
public class CharacterList
{
    public CharacterData[] characters;
}

[Serializable]
public class SkillList
{
    public SkillData[] skills;
}

[Serializable]
public class EquipmentList
{
    public EquipmentData[] equipments;
}

[Serializable]
public class CharacterSkillSlotData
{
    public string skillId;
    public string equippedAdverbId;
}

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

    // Optional scored verb relations. Kept separate from equipped skills and save data.
    public VerbCollocation[] verbCollocations;

    // Optional explicit adverb collocations for battle card generation.
    // When omitted, the battle uses registered compatible battle adverbs,
    // then falls back to compatible adverbs in the database.
    public string[] collocationAdverbIds;

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

    // Optional grammar metadata. Existing skillName is already the German infinitive.
    // Separable verbs use e.g. infinitive=angreifen, separablePrefix=an,
    // conjugations.thirdPersonSingular=greift an (or greift).
    public string infinitive;
    public string separablePrefix;
    public string sentenceComplement;

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

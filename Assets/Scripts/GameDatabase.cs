using System.Collections.Generic;
using UnityEngine;

public class GameDatabase : MonoBehaviour
{
    public static GameDatabase Instance;

    private Dictionary<string, CharacterData> characters;
    private Dictionary<string, SkillData> skills;
    private Dictionary<string, EquipmentData> equipments;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadCharacters();
        LoadSkills();
        LoadEquipments();
    }

    private void LoadCharacters()
    {
        TextAsset json = Resources.Load<TextAsset>("Data/characters");
        CharacterList list = JsonUtility.FromJson<CharacterList>(json.text);

        characters = new Dictionary<string, CharacterData>();

        foreach (CharacterData character in list.characters)
        {
            characters[character.id] = character;
        }

        Debug.Log($"Characters loaded: {list.characters.Length}");
    }

    private void LoadSkills()
    {
        TextAsset json = Resources.Load<TextAsset>("Data/skills");
        SkillList list = JsonUtility.FromJson<SkillList>(json.text);

        skills = new Dictionary<string, SkillData>();

        foreach (SkillData skill in list.skills)
        {
            skills[skill.id] = skill;
        }

        Debug.Log($"Skills loaded: {list.skills.Length}");
    }

    private void LoadEquipments()
    {
        TextAsset json = Resources.Load<TextAsset>("Data/equipments");

        if (json == null)
        {
            Debug.LogError("equipments.json was not found at Resources/Data/equipments.json");
            equipments = new Dictionary<string, EquipmentData>();
            return;
        }

        EquipmentList list = JsonUtility.FromJson<EquipmentList>(json.text);

        equipments = new Dictionary<string, EquipmentData>();

        foreach (EquipmentData equipment in list.equipments)
        {
            equipments[equipment.id] = equipment;
        }

        Debug.Log($"Equipments loaded: {list.equipments.Length}");
    }

    public CharacterData GetCharacter(string id)
    {
        if (!characters.ContainsKey(id))
        {
            Debug.LogError($"Character id not found: {id}");
            return null;
        }

        return characters[id];
    }

    public SkillData GetSkill(string id)
    {
        if (!skills.ContainsKey(id))
        {
            Debug.LogError($"Skill id not found: {id}");
            return null;
        }

        return skills[id];
    }

    public EquipmentData GetEquipment(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        if (!equipments.ContainsKey(id))
        {
            Debug.LogError($"Equipment id not found: {id}");
            return null;
        }

        return equipments[id];
    }

    public CharacterData[] GetAllCharacters()
    {
        CharacterData[] result = new CharacterData[characters.Count];
        characters.Values.CopyTo(result, 0);
        return result;
    }

    public EquipmentData[] GetAllEquipments()
    {
        EquipmentData[] result = new EquipmentData[equipments.Count];
        equipments.Values.CopyTo(result, 0);
        return result;
    }
}
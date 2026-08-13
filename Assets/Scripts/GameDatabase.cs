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
        characters = new Dictionary<string, CharacterData>();

        TextAsset json = Resources.Load<TextAsset>("Data/characters");

        if (json == null)
        {
            Debug.LogError("characters.json was not found at Resources/Data/characters.json");
            return;
        }

        CharacterList list;
        try
        {
            list = JsonUtility.FromJson<CharacterList>(json.text);
        }
        catch (System.ArgumentException exception)
        {
            Debug.LogError($"characters.json could not be parsed: {exception.Message}");
            return;
        }

        if (list == null || list.characters == null)
        {
            Debug.LogError("characters.json does not contain a characters array.");
            return;
        }

        foreach (CharacterData character in list.characters)
        {
            if (character == null || string.IsNullOrEmpty(character.id))
            {
                Debug.LogWarning("Skipped a character entry with no id in characters.json.");
                continue;
            }

            characters[character.id] = character;
        }

        Debug.Log($"Characters loaded: {characters.Count}");
    }

    private void LoadSkills()
    {
        skills = new Dictionary<string, SkillData>();

        TextAsset json = Resources.Load<TextAsset>("Data/skills");

        if (json == null)
        {
            Debug.LogError("skills.json was not found at Resources/Data/skills.json");
            return;
        }

        SkillList list;
        try
        {
            list = JsonUtility.FromJson<SkillList>(json.text);
        }
        catch (System.ArgumentException exception)
        {
            Debug.LogError($"skills.json could not be parsed: {exception.Message}");
            return;
        }

        if (list == null || list.skills == null)
        {
            Debug.LogError("skills.json does not contain a skills array.");
            return;
        }

        foreach (SkillData skill in list.skills)
        {
            if (skill == null || string.IsNullOrEmpty(skill.id))
            {
                Debug.LogWarning("Skipped a skill entry with no id in skills.json.");
                continue;
            }

            skills[skill.id] = skill;
        }

        Debug.Log($"Skills loaded: {skills.Count}");
    }

    private void LoadEquipments()
    {
        equipments = new Dictionary<string, EquipmentData>();

        TextAsset json = Resources.Load<TextAsset>("Data/equipments");

        if (json == null)
        {
            Debug.LogError("equipments.json was not found at Resources/Data/equipments.json");
            return;
        }

        EquipmentList list;
        try
        {
            list = JsonUtility.FromJson<EquipmentList>(json.text);
        }
        catch (System.ArgumentException exception)
        {
            Debug.LogError($"equipments.json could not be parsed: {exception.Message}");
            return;
        }

        if (list == null || list.equipments == null)
        {
            Debug.LogError("equipments.json does not contain an equipments array.");
            return;
        }

        foreach (EquipmentData equipment in list.equipments)
        {
            if (equipment == null || string.IsNullOrEmpty(equipment.id))
            {
                Debug.LogWarning("Skipped an equipment entry with no id in equipments.json.");
                continue;
            }

            equipments[equipment.id] = equipment;
        }

        Debug.Log($"Equipments loaded: {equipments.Count}");
    }

    public CharacterData GetCharacter(string id)
    {
        if (string.IsNullOrEmpty(id) ||
            characters == null ||
            !characters.TryGetValue(id, out CharacterData character))
        {
            Debug.LogError($"Character id not found: {id}");
            return null;
        }

        return character;
    }

    public SkillData GetSkill(string id)
    {
        if (string.IsNullOrEmpty(id) ||
            skills == null ||
            !skills.TryGetValue(id, out SkillData skill))
        {
            Debug.LogError($"Skill id not found: {id}");
            return null;
        }

        return skill;
    }

    public EquipmentData GetEquipment(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        if (equipments == null ||
            !equipments.TryGetValue(id, out EquipmentData equipment))
        {
            Debug.LogError($"Equipment id not found: {id}");
            return null;
        }

        return equipment;
    }

    public CharacterData[] GetAllCharacters()
    {
        if (characters == null || characters.Count == 0)
        {
            return new CharacterData[0];
        }

        CharacterData[] result = new CharacterData[characters.Count];
        characters.Values.CopyTo(result, 0);
        return result;
    }

    public EquipmentData[] GetAllEquipments()
    {
        if (equipments == null || equipments.Count == 0)
        {
            return new EquipmentData[0];
        }

        EquipmentData[] result = new EquipmentData[equipments.Count];
        equipments.Values.CopyTo(result, 0);
        return result;
    }
}

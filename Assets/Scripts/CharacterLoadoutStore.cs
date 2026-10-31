using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CharacterLoadout
{
    public string instanceId;
    public string characterId;
    public string adjectiveId;
    public string articleId;
    // Slot order is the equipped skill order. Each skill has its own adverb.
    public CharacterSkillSlotData[] skillSlots;
}

[Serializable]
public sealed class CharacterLoadoutState
{
    public List<CharacterLoadout> characters = new List<CharacterLoadout>();
}

public static class CharacterLoadoutStore
{
    public const int MaxSkillSlots = 4;
    private static readonly Dictionary<string, CharacterLoadout> Overrides =
        new Dictionary<string, CharacterLoadout>(StringComparer.Ordinal);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Overrides.Clear();

    // Always return a detached draft. Merely viewing/editing a character never commits.
    public static CharacterLoadout GetLoadout(string instanceId)
    {
        var instance = CharacterInstances.Get(instanceId);
        if (instance == null) return null;
        if (Overrides.TryGetValue(instanceId, out CharacterLoadout saved)) return Clone(saved);
        return GetDefaultLoadout(instance.characterId, instanceId);
    }

    public static CharacterLoadout GetDefaultLoadout(string characterId, string instanceId = null)
    {
        CharacterData master = GameDatabase.Instance?.GetCharacter(characterId);
        if (master == null) return null;
        var slots = new List<CharacterSkillSlotData>();
        foreach (string skillId in master.skillIds ?? Array.Empty<string>())
        {
            string adverbId = "";
            foreach (var slot in master.skillSlots ?? Array.Empty<CharacterSkillSlotData>())
                if (slot != null && slot.skillId == skillId) { adverbId = slot.equippedAdverbId; break; }
            slots.Add(new CharacterSkillSlotData { skillId = skillId, equippedAdverbId = adverbId });
        }
        return new CharacterLoadout {
            instanceId = instanceId,
            characterId = characterId,
            adjectiveId = master.equippedAdjectiveId ?? "",
            articleId = !string.IsNullOrEmpty(master.equippedArticleId)
                ? master.equippedArticleId : master.equippedCaseMarkerId ?? "",
            skillSlots = slots.ToArray()
        };
    }

    public static bool Commit(CharacterLoadout draft)
    {
        if (!Validate(draft, out _)) return false;
        Overrides[draft.instanceId] = Clone(draft);
        PlayerStateChanges.Notify();
        return true;
    }

    public static bool Validate(CharacterLoadout loadout, out string error)
    {
        var instance = CharacterInstances.Get(loadout?.instanceId);
        if (instance == null || instance.characterId != loadout.characterId)
        { error = "Loadout does not belong to an owned instance."; return false; }
        return ValidateDefinition(loadout, out error);
    }

    // Pure validation also supports save decoding before runtime ownership is restored.
    public static bool ValidateDefinition(CharacterLoadout loadout, out string error)
    {
        error = null;
        GameDatabase db = GameDatabase.Instance;
        if (loadout == null || db == null || !db.ContainsCharacter(loadout.characterId))
            error = "Unknown loadout character.";
        else if (!ValidEquipment(db, loadout.adjectiveId) || !ValidEquipment(db, loadout.articleId))
            error = "Unknown character equipment.";
        else if (loadout.skillSlots == null || loadout.skillSlots.Length == 0 || loadout.skillSlots.Length > MaxSkillSlots)
            error = "Invalid equipped skill count.";
        else
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var slot in loadout.skillSlots)
                if (slot == null || !db.ContainsSkill(slot.skillId) || !ids.Add(slot.skillId) ||
                    !ValidEquipment(db, slot.equippedAdverbId))
                { error = "Unknown or duplicate skill/adverb in loadout."; break; }
        }
        return error == null;
    }

    private static bool ValidEquipment(GameDatabase db, string id) =>
        string.IsNullOrEmpty(id) || db.ContainsEquipment(id);

    public static CharacterData GetCharacterForBattle(string instanceId)
    {
        var instance = CharacterInstances.Get(instanceId);
        if (instance == null) return null;
        CharacterData master = GameDatabase.Instance.GetCharacter(instance.characterId);
        CharacterLoadout loadout = GetLoadout(instanceId);
        if (master == null || loadout == null) return null;
        CharacterData result = JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(master));
        result.equippedAdjectiveId = loadout.adjectiveId;
        result.equippedArticleId = loadout.articleId;
        result.equippedCaseMarkerId = loadout.articleId;
        result.skillSlots = Clone(loadout).skillSlots;
        result.skillIds = Array.ConvertAll(result.skillSlots, slot => slot.skillId);
        return result;
    }

    public static CharacterLoadoutState CaptureState()
    {
        var state = new CharacterLoadoutState();
        foreach (var id in new List<string>(Overrides.Keys)) state.characters.Add(Clone(Overrides[id]));
        state.characters.Sort((a, b) => StringComparer.Ordinal.Compare(a.instanceId, b.instanceId));
        return state;
    }

    public static bool RestoreState(CharacterLoadoutState state)
    {
        if (state?.characters == null) return false;
        var restored = new Dictionary<string, CharacterLoadout>(StringComparer.Ordinal);
        foreach (var loadout in state.characters)
        {
            if (!Validate(loadout, out _) || restored.ContainsKey(loadout.instanceId)) return false;
            restored.Add(loadout.instanceId, Clone(loadout));
        }
        Overrides.Clear();
        foreach (var pair in restored) Overrides.Add(pair.Key, pair.Value);
        PlayerStateChanges.Notify();
        return true;
    }

    public static void RemoveInstance(string instanceId)
    {
        if (instanceId != null && Overrides.Remove(instanceId)) PlayerStateChanges.Notify();
    }

    private static CharacterLoadout Clone(CharacterLoadout source) =>
        JsonUtility.FromJson<CharacterLoadout>(JsonUtility.ToJson(source));
}

using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SaveData
{
    // No initializer: a missing version must not silently become the current version.
    public int version;
    public InventoryState inventory;
    public OwnedCharactersState ownedCharacters;
    public CharacterLoadoutState characterEquipment;
    public StageProgressState stageProgress;
}

public static class NewGameDefaults
{
    // Provisional starter, matching GameManager's existing default. Edit this list to change it.
    public static readonly string[] StarterCharacterIds = { "nacht" };

    public static SaveData Create()
    {
        var data = new SaveData {
            version = SaveDataValidator.CurrentVersion,
            inventory = new InventoryState(),
            ownedCharacters = new OwnedCharactersState(),
            characterEquipment = new CharacterLoadoutState(),
            stageProgress = new StageProgressState()
        };
        foreach (string id in StarterCharacterIds)
            if (GameDatabase.Instance.ContainsCharacter(id)) data.ownedCharacters.characterIds.Add(id);
        if (data.ownedCharacters.characterIds.Count == 0)
            throw new InvalidOperationException("No valid starter character is configured.");
        return data;
    }
}

public static class GameContentIds
{
    private static HashSet<string> itemIds;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => itemIds = null;

    public static bool IsItem(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        if (itemIds == null)
        {
            itemIds = new HashSet<string>(StringComparer.Ordinal);
            TextAsset items = Resources.Load<TextAsset>("Data/gacha_items");
            if (items != null)
                foreach (var item in JsonUtility.FromJson<GachaItemDatabase>(items.text).items)
                    if (item != null && !string.IsNullOrWhiteSpace(item.id)) itemIds.Add(item.id);
            TextAsset recipes = Resources.Load<TextAsset>("Data/crafting_recipes");
            if (recipes != null)
                foreach (var recipe in JsonUtility.FromJson<CraftingRecipeDatabase>(recipes.text).recipes)
                    if (recipe != null && recipe.resultType != "character" && !string.IsNullOrWhiteSpace(recipe.resultId))
                        itemIds.Add(recipe.resultId);
        }
        return itemIds.Contains(id);
    }
}

public static class SaveDataValidator
{
    public const int CurrentVersion = 1;

    public static bool Validate(SaveData data, out string error)
    {
        error = null;
        if (data == null) error = "Save data is null.";
        else if (data.version != CurrentVersion) error = $"Unsupported save version: {data.version}.";
        else if (data.inventory?.items == null || data.ownedCharacters?.characterIds == null ||
                 data.characterEquipment?.characters == null || data.stageProgress == null)
            error = "Required save sections are missing.";
        else
        {
            var items = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in data.inventory.items)
                if (entry == null || !GameContentIds.IsItem(entry.itemId) || entry.count <= 0 || !items.Add(entry.itemId))
                { error = "Invalid inventory ID/count or duplicate entry."; break; }
            var owned = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in data.ownedCharacters.characterIds)
                if (!GameDatabase.Instance.ContainsCharacter(id) || !owned.Add(id))
                { error = "Unknown or duplicate owned character."; break; }
            if (owned.Count == 0) error = "Save contains no playable characters.";
            var loadouts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var loadout in data.characterEquipment.characters)
            {
                if (!CharacterLoadoutStore.Validate(loadout, out string loadoutError)) { error = loadoutError; break; }
                if (!loadouts.Add(loadout.characterId)) { error = "Duplicate character loadout."; break; }
            }
            if (!StageProgress.ValidIds(data.stageProgress.unlockedStageIds) || !StageProgress.ValidIds(data.stageProgress.clearedStageIds))
                error = "Invalid stage progress IDs.";
        }
        return error == null;
    }

    public static bool TryDecode(string json, out SaveData data, out string error)
    {
        data = null;
        try
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
            { error = "Save is not a JSON object."; return false; }
            data = JsonUtility.FromJson<SaveData>(json);
            return Validate(data, out error);
        }
        catch (Exception exception)
        {
            error = "Invalid save JSON: " + exception.Message;
            return false;
        }
    }
}

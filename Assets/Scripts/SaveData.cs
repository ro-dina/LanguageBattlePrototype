using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SaveData
{
    public int version;
    public InventoryState inventory;
    public CharacterInstanceState characterInstances;
    public CharacterLoadoutState characterEquipment;
    public CharacterCollectionState characterCollection;
    public StageProgressState stageProgress;
}

public static class NewGameDefaults
{
    public static readonly string[] StarterCharacterIds = { "nacht" };
    public static SaveData Create()
    {
        var data = new SaveData { version = SaveDataValidator.CurrentVersion,
            inventory = new InventoryState(), characterInstances = new CharacterInstanceState(),
            characterEquipment = new CharacterLoadoutState(), characterCollection = new CharacterCollectionState(),
            stageProgress = new StageProgressState() };
        foreach (string id in StarterCharacterIds)
            if (GameDatabase.Instance.ContainsCharacter(id))
            {
                data.characterInstances.instances.Add(new CharacterInstance { instanceId = CharacterInstances.NewId(), characterId = id });
                data.characterCollection.discoveredCharacterIds.Add(id);
            }
        if (data.characterInstances.instances.Count == 0) throw new InvalidOperationException("No valid starter character is configured.");
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
    public const int CurrentVersion = 2;
    public static bool Validate(SaveData data, out string error)
    {
        error = null;
        if (data == null) { error = "Save data is null."; return false; }
        if (data.version != CurrentVersion) { error = $"Unsupported save version: {data.version}."; return false; }
        if (!ValidateCommon(data.inventory, data.stageProgress, out error)) return false;
        if (!CharacterInstances.ValidState(data.characterInstances, out error)) return false;
        if (!CharacterCollection.ValidState(data.characterCollection) || data.characterEquipment?.characters == null)
        { error = "Missing or invalid collection/loadout sections."; return false; }
        var instances = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var instance in data.characterInstances.instances)
        {
            instances.Add(instance.instanceId, instance.characterId);
            if (!data.characterCollection.discoveredCharacterIds.Contains(instance.characterId))
            { error = "Owned species is missing its discovery record."; return false; }
        }
        var loadouts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var loadout in data.characterEquipment.characters)
        {
            if (!CharacterLoadoutStore.ValidateDefinition(loadout, out error)) return false;
            if (string.IsNullOrEmpty(loadout.instanceId) || !instances.TryGetValue(loadout.instanceId, out var species) ||
                species != loadout.characterId || !loadouts.Add(loadout.instanceId))
            { error = "Orphaned, mismatched or duplicate instance loadout."; return false; }
        }
        return true;
    }
    internal static bool ValidateCommon(InventoryState inventory, StageProgressState stage, out string error)
    {
        error = null;
        if (inventory?.items == null || stage == null || !StageProgress.ValidIds(stage.unlockedStageIds) || !StageProgress.ValidIds(stage.clearedStageIds))
        { error = "Missing inventory or invalid stage progress."; return false; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in inventory.items)
            if (entry == null || !GameContentIds.IsItem(entry.itemId) || entry.count <= 0 || !ids.Add(entry.itemId))
            { error = "Invalid inventory ID/count or duplicate entry."; return false; }
        return true;
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
        catch (Exception exception) { error = "Invalid save JSON: " + exception.Message; return false; }
    }
}

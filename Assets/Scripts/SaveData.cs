using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SaveData
{
    public int version;
    public WalletState wallet;
    public TutorialProgressState tutorials = new TutorialProgressState();
    public BattleRewardState battleRewards = new BattleRewardState();
    public InventoryState inventory;
    public CharacterInstanceState characterInstances;
    public CharacterLoadoutState characterEquipment;
    public CharacterCollectionState characterCollection;
    public StageProgressState stageProgress;
    public List<WordItemConversion> convertedWordItems = new List<WordItemConversion>();
}

[Serializable]
public sealed class WordItemConversion
{
    public string itemId;
    public string characterId;
    public int count;
}

public static class NewGameDefaults
{
    public static readonly string[] StarterCharacterIds = { "nacht" };
    public static SaveData Create()
    {
        var data = new SaveData { version = SaveDataValidator.CurrentVersion,
            wallet = new WalletState { balance = EconomySettings.Current.initialCoins },
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
        return itemIds.Contains(id) || DynamicMaterials.IsId(id);
    }
}

public static class SaveDataValidator
{
    public const int CurrentVersion = 6;
    public static bool Validate(SaveData data, out string error) => Validate(data,CurrentVersion,out error);
    internal static bool ValidateVersionFive(SaveData data,out string error) => Validate(data,5,out error);
    internal static bool ValidateVersionFour(SaveData data,out string error) => Validate(data,4,out error);
    internal static bool ValidateVersionThree(SaveData data,out string error) => Validate(data,3,out error);
    internal static bool ValidateVersionTwo(SaveData data,out string error) => Validate(data,2,out error);
    private static bool Validate(SaveData data,int version,out string error)
    {
        error = null;
        if (data == null) { error = "Save data is null."; return false; }
        if (data.version != version) { error = $"Unsupported save version: {data.version}."; return false; }
        if (!ValidateCommon(data.inventory, data.stageProgress, out error,version >= 5)) return false;
        if (version >= 6 && (!StageProgress.ValidState(data.stageProgress) || !TutorialPreferences.ValidState(data.tutorials)))
        { error = "Invalid stage or tutorial progress."; return false; }
        if (version >= 4 && (data.wallet == null || data.wallet.balance < 0))
        { error = "Missing wallet or negative balance."; return false; }
        if (version >= 5 && !BattleRewardService.ValidState(data.battleRewards))
        { error = "Invalid battle reward state."; return false; }
        if (version >= 3)
        {
            if (data.convertedWordItems == null) { error = "Missing conversion history."; return false; }
            var converted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in data.convertedWordItems)
                if (record == null || record.count <= 0 || !converted.Add(record.itemId) ||
                    WordCharacterRewards.Character(record.itemId)?.id != record.characterId)
                { error = "Invalid word conversion history."; return false; }
            foreach (var item in data.inventory.items)
                if (WordCharacterRewards.Character(item.itemId) != null || WordCharacterRewards.Ambiguous(item.itemId))
                { error = "Legacy character words cannot remain in current inventory."; return false; }
        }
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
    internal static bool ValidateCommon(InventoryState inventory, StageProgressState stage, out string error,bool allowDynamic = false)
    {
        error = null;
        if (inventory?.items == null || stage == null || !StageProgress.ValidIds(stage.unlockedStageIds) || !StageProgress.ValidIds(stage.clearedStageIds))
        { error = "Missing inventory or invalid stage progress."; return false; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in inventory.items)
            if (entry == null || !GameContentIds.IsItem(entry.itemId) || entry.count <= 0 || !ids.Add(entry.itemId) ||
                (DynamicMaterials.IsId(entry.itemId) ? !allowDynamic || !DynamicMaterials.ValidEntry(entry) : !string.IsNullOrEmpty(entry.text)))
            { error = "Invalid inventory ID/count or duplicate entry."; return false; }
        return true;
    }
    public static bool TryDecode(string json, out SaveData data, out string error) => Decode(json,CurrentVersion,out data,out error);
    internal static bool TryDecodeVersionFour(string json,out SaveData data,out string error) => Decode(json,4,out data,out error);
    internal static bool TryDecodeVersionFive(string json,out SaveData data,out string error) => Decode(json,5,out data,out error);
    private static bool Decode(string json,int expectedVersion,out SaveData data,out string error)
    {
        data = null;
        try
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
            { error = "Save is not a JSON object."; return false; }
            data = JsonUtility.FromJson<SaveData>(json);
            if (data?.version >= 5 && !System.Text.RegularExpressions.Regex.IsMatch(json,"\"battleRewards\"\\s*:\\s*\\{"))
            { error = "Missing battle reward state."; return false; }
            if (data?.version == CurrentVersion && !System.Text.RegularExpressions.Regex.IsMatch(json,"\"tutorials\"\\s*:\\s*\\{"))
            { error = "Missing tutorial state."; return false; }
            if (data?.version >= 4)
            {
                var wallets = System.Text.RegularExpressions.Regex.Matches(json,"\"wallet\"\\s*:\\s*\\{[^{}]*?\"balance\"\\s*:\\s*(-?[0-9]+)\\s*[,}]");
                if (wallets.Count != 1 || !long.TryParse(wallets[0].Groups[1].Value,System.Globalization.NumberStyles.AllowLeadingSign,
                    System.Globalization.CultureInfo.InvariantCulture,out long balance) || data.wallet == null || balance != data.wallet.balance)
                { error = "Wallet balance must be an explicit 64-bit nonnegative integer."; return false; }
            }
            return Validate(data,expectedVersion,out error);
        }
        catch (Exception exception) { error = "Invalid save JSON: " + exception.Message; return false; }
    }
}

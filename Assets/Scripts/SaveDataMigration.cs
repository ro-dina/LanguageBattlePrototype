using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class LegacySaveData
{
    public int version;
    public InventoryState inventory;
    public OwnedCharactersState ownedCharacters;
    public CharacterLoadoutState characterEquipment;
    public StageProgressState stageProgress;
}

public static class SaveDataMigration
{
    [Serializable] private sealed class Header { public int version; }
    public static int Version(string json)
    {
        try { return JsonUtility.FromJson<Header>(json)?.version ?? 0; }
        catch (Exception) { return 0; }
    }
    private static bool VersionOneToTwo(string json, out SaveData migrated, out string error)
    {
        migrated = null; error = null;
        try
        {
            var old = JsonUtility.FromJson<LegacySaveData>(json);
            if (old == null || old.version != 1 || old.ownedCharacters?.characterIds == null ||
                old.characterEquipment?.characters == null || old.ownedCharacters.characterIds.Count == 0)
            { error = "Invalid version-1 sections."; return false; }
            if (!SaveDataValidator.ValidateCommon(old.inventory, old.stageProgress, out error)) return false;
            var identities = new Dictionary<string, string>(StringComparer.Ordinal);
            var data = new SaveData { version = 2, inventory = old.inventory, stageProgress = old.stageProgress,
                characterInstances = new CharacterInstanceState(), characterCollection = new CharacterCollectionState(),
                characterEquipment = new CharacterLoadoutState() };
            foreach (string id in old.ownedCharacters.characterIds)
            {
                if (GameDatabase.Instance == null || !GameDatabase.Instance.ContainsCharacter(id) || identities.ContainsKey(id))
                { error = "Invalid version-1 owned character."; return false; }
                string instanceId = MigratedId(json, id);
                identities.Add(id, instanceId);
                data.characterInstances.instances.Add(new CharacterInstance { instanceId = instanceId, characterId = id });
                data.characterCollection.discoveredCharacterIds.Add(id);
            }
            var loadouts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var loadout in old.characterEquipment.characters)
            {
                if (!CharacterLoadoutStore.ValidateDefinition(loadout, out error)) return false;
                if (!identities.TryGetValue(loadout.characterId, out var instanceId) || !loadouts.Add(loadout.characterId))
                { error = "Version-1 equipment has no unique owned character; original retained for repair."; return false; }
                loadout.instanceId = instanceId;
                data.characterEquipment.characters.Add(loadout);
            }
            if (!SaveDataValidator.ValidateVersionTwo(data, out error)) return false;
            migrated = data; return true;
        }
        catch (Exception exception) { error = "Migration failed: " + exception.Message; return false; }
    }
    public static bool TryMigrate(string json,out SaveData migrated,out string error)
    {
        migrated = null; error = null;
        try
        {
            SaveData data;
            int version = Version(json);
            if (version == 3 || version == 4 || version == 5)
            {
                if (version == 5) { if (!SaveDataValidator.TryDecodeVersionFive(json,out data,out error)) return false; }
                else if (version == 4) { if (!SaveDataValidator.TryDecodeVersionFour(json,out data,out error)) return false; }
                else { data = JsonUtility.FromJson<SaveData>(json); if (!SaveDataValidator.ValidateVersionThree(data,out error)) return false; data.wallet = new WalletState { balance = EconomySettings.Current.initialCoins }; }
                if (version < 5) data.battleRewards = new BattleRewardState();
                StageProgress.Upgrade(data.stageProgress);
                data.tutorials = new TutorialProgressState();
                data.version = SaveDataValidator.CurrentVersion;
                if (!SaveDataValidator.Validate(data,out error)) return false;
                if (Encoding.UTF8.GetByteCount(JsonUtility.ToJson(data,true)) > SaveFileStore.MaxBytes)
                { error = "Migrated save exceeds the existing 4 MB budget; original retained."; return false; }
                migrated = data; return true;
            }
            if (version == 1) { if (!VersionOneToTwo(json,out data,out error)) return false; }
            else if (version == 2)
            {
                data = JsonUtility.FromJson<SaveData>(json);
                if (!SaveDataValidator.ValidateVersionTwo(data,out error)) return false;
            }
            else { error = "Only versions 1 through 5 require migration."; return false; }
            data.convertedWordItems = new List<WordItemConversion>();
            var remaining = new List<InventoryEntry>();
            var ids = new HashSet<string>();
            foreach (var instance in data.characterInstances.instances) ids.Add(instance.instanceId);
            foreach (var entry in data.inventory.items)
            {
                if (WordCharacterRewards.Ambiguous(entry.itemId)) { error = "Ambiguous word reward; original retained."; return false; }
                var character = WordCharacterRewards.Character(entry.itemId);
                if (character == null) { remaining.Add(entry); continue; }
                if ((long)data.characterInstances.instances.Count + entry.count > SaveFileStore.MaxBytes / 64)
                { error = "Converted individuals exceed the save size budget; original retained."; return false; }
                int before = data.characterInstances.instances.Count;
                for (int n=0;n<entry.count;n++)
                {
                    string id = MigratedId(json,"word/" + entry.itemId + "/" + n);
                    if (!ids.Add(id)) { error = "Migrated identity collision."; return false; }
                    PlayerStateTransactions.AddCharacter(data,character.id,id);
                }
                if (data.characterInstances.instances.Count-before != entry.count) return false;
                data.convertedWordItems.Add(new WordItemConversion { itemId=entry.itemId, characterId=character.id, count=entry.count });
            }
            data.inventory.items = remaining; data.version = SaveDataValidator.CurrentVersion;
            data.wallet = new WalletState { balance = EconomySettings.Current.initialCoins };
            data.battleRewards = new BattleRewardState();
            StageProgress.Upgrade(data.stageProgress);data.tutorials = new TutorialProgressState();
            if (!SaveDataValidator.Validate(data,out error)) return false;
            if (Encoding.UTF8.GetByteCount(JsonUtility.ToJson(data,true)) > SaveFileStore.MaxBytes)
            { error = "Converted save exceeds the existing 4 MB budget; original retained."; return false; }
            migrated = data; return true;
        }
        catch (Exception exception) { error = "Migration failed: " + exception.Message; return false; }
    }
    // The same source bytes always map to the same identities, including after an interrupted migration.
    private static string MigratedId(string json, string characterId)
    {
        using (var sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes("LanguageBattle/v1/" + characterId + "\n" + json));
            return "character-" + BitConverter.ToString(digest, 0, 16).Replace("-", "").ToLowerInvariant();
        }
    }
}

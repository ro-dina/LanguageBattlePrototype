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
    public static bool TryMigrate(string json, out SaveData migrated, out string error)
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
            if (!SaveDataValidator.Validate(data, out error)) return false;
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

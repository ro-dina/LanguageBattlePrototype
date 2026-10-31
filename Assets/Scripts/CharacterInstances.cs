using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class CharacterInstance
{
    public string instanceId;
    public string characterId;
}

[Serializable]
public sealed class CharacterInstanceState
{
    public List<CharacterInstance> instances = new List<CharacterInstance>();
}

// Owns identities only. CharacterData remains the shared, unmodified species master.
public static class CharacterInstances
{
    private static readonly List<CharacterInstance> Instances = new List<CharacterInstance>();
    private static readonly HashSet<string> BattleLocks = new HashSet<string>(StringComparer.Ordinal);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Instances.Clear(); BattleLocks.Clear(); }
    public static string NewId() => "character-" + Guid.NewGuid().ToString("N");
    private static CharacterInstance Copy(CharacterInstance source) => source == null ? null :
        new CharacterInstance { instanceId = source.instanceId, characterId = source.characterId };
    public static CharacterInstance Create(string characterId)
    {
        if (GameDatabase.Instance == null || !GameDatabase.Instance.ContainsCharacter(characterId)) return null;
        var instance = new CharacterInstance { instanceId = NewId(), characterId = characterId };
        using (PlayerStateChanges.BeginBatch())
        {
            Instances.Add(instance);
            CharacterCollection.Discover(characterId);
            PlayerStateChanges.Notify();
        }
        return Copy(instance);
    }
    public static CharacterInstance Get(string instanceId) => Copy(Instances.Find(i => i.instanceId == instanceId));
    public static bool Owns(string instanceId) => !string.IsNullOrEmpty(instanceId) && Instances.Any(i => i.instanceId == instanceId);
    public static List<CharacterInstance> GetAll() => Instances.Select(Copy).ToList();
    public static List<CharacterInstance> GetByCharacterId(string characterId) => Instances.Where(i => i.characterId == characterId).Select(Copy).ToList();
    public static int Count(string characterId) => Instances.Count(i => i.characterId == characterId);
    public static bool Remove(string instanceId)
    {
        if (BattleLocks.Contains(instanceId ?? "")) return false;
        var instance = Instances.Find(i => i.instanceId == instanceId);
        if (instance == null) return false;
        using (PlayerStateChanges.BeginBatch())
        {
            Instances.Remove(instance);
            CharacterLoadoutStore.RemoveInstance(instanceId);
            GameManager.OnInstanceRemoved(instanceId);
            PlayerStateChanges.Notify();
        }
        return true;
    }
    public static bool TryBeginBattle(string instanceId)
    { return Owns(instanceId) && BattleLocks.Add(instanceId); }
    public static void EndBattle(string instanceId) { if (instanceId != null) BattleLocks.Remove(instanceId); }
    public static bool CanRestore(CharacterInstanceState state)
    {
        return ValidState(state, out _) && BattleLocks.All(id => state.instances.Any(i => i.instanceId == id && i.characterId == Get(id)?.characterId));
    }
    public static bool RestoreState(CharacterInstanceState state)
    {
        if (!CanRestore(state)) return false;
        using (PlayerStateChanges.BeginBatch())
        {
            var removed = Instances.Where(i => !state.instances.Any(n => n.instanceId == i.instanceId && n.characterId == i.characterId)).Select(i => i.instanceId).ToArray();
            Instances.Clear(); Instances.AddRange(state.instances.Select(Copy));
            foreach (string id in removed) CharacterLoadoutStore.RemoveInstance(id);
            foreach (var instance in Instances) CharacterCollection.Discover(instance.characterId);
            if (!Owns(GameManager.SelectedCharacterInstanceId)) GameManager.OnInstanceRemoved(GameManager.SelectedCharacterInstanceId);
            PlayerStateChanges.Notify();
        }
        return true;
    }
    public static CharacterInstanceState CaptureState() => new CharacterInstanceState { instances = GetAll() };
    public static bool ValidState(CharacterInstanceState state, out string error)
    {
        error = null;
        if (state?.instances == null) { error = "Missing character instances."; return false; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var instance in state.instances)
            if (instance == null || string.IsNullOrWhiteSpace(instance.instanceId) || !ids.Add(instance.instanceId) ||
                GameDatabase.Instance == null || !GameDatabase.Instance.ContainsCharacter(instance.characterId))
            { error = "Invalid or duplicate character instance."; return false; }
        return true;
    }
    public static string DisplayName(string instanceId)
    {
        var instance = Get(instanceId);
        return instance == null ? "Not owned" : GameDatabase.Instance.GetCharacter(instance.characterId).characterName + " · " + DisplayBadge(instanceId);
    }
    public static string DisplayBadge(string instanceId)
    {
        var instance = Get(instanceId);
        if (instance == null) return "—";
        string id = instance.instanceId.StartsWith("character-", StringComparison.Ordinal) ? instance.instanceId.Substring(10) : instance.instanceId;
        int length = Math.Min(6, id.Length);
        // Expand a short badge only if another owned identity would have the same badge.
        while (length < id.Length && Instances.Any(i => i.instanceId != instanceId &&
            (i.instanceId.StartsWith("character-", StringComparison.Ordinal) ? i.instanceId.Substring(10) : i.instanceId).StartsWith(id.Substring(0, length), StringComparison.Ordinal))) length++;
        return id.Substring(0, length).ToUpperInvariant();
    }
}

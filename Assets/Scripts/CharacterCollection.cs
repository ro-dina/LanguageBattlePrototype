using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class CharacterCollectionState
{
    public List<string> discoveredCharacterIds = new List<string>();
}

public enum CharacterCollectionStatus { Undiscovered, DiscoveredNotOwned, Owned }

public static class CharacterCollection
{
    private static readonly HashSet<string> Discovered = new HashSet<string>(StringComparer.Ordinal);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Discovered.Clear();
    public static bool IsDiscovered(string characterId) => characterId != null && Discovered.Contains(characterId);
    public static bool Discover(string characterId)
    {
        if (GameDatabase.Instance == null || !GameDatabase.Instance.ContainsCharacter(characterId) || !Discovered.Add(characterId)) return false;
        PlayerStateChanges.Notify(); return true;
    }
    public static CharacterCollectionStatus Status(string characterId) => CharacterInstances.Count(characterId) > 0
        ? CharacterCollectionStatus.Owned : IsDiscovered(characterId) ? CharacterCollectionStatus.DiscoveredNotOwned : CharacterCollectionStatus.Undiscovered;
    public static string StatusLabel(string characterId) => CharacterInstances.Count(characterId) > 0
        ? "Owned: " + CharacterInstances.Count(characterId) : IsDiscovered(characterId) ? "Discovered • 0 owned" : "Undiscovered";
    public static CharacterCollectionState CaptureState()
    {
        var ids = new List<string>(Discovered); ids.Sort(StringComparer.Ordinal);
        return new CharacterCollectionState { discoveredCharacterIds = ids };
    }
    public static bool ValidState(CharacterCollectionState state)
    {
        if (state?.discoveredCharacterIds == null) return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in state.discoveredCharacterIds)
            if (GameDatabase.Instance == null || !GameDatabase.Instance.ContainsCharacter(id) || !ids.Add(id)) return false;
        return true;
    }
    public static bool RestoreState(CharacterCollectionState state)
    {
        if (!ValidState(state) || CharacterInstances.GetAll().Any(i => !state.discoveredCharacterIds.Contains(i.characterId))) return false;
        Discovered.Clear(); Discovered.UnionWith(state.discoveredCharacterIds);
        PlayerStateChanges.Notify(); return true;
    }
}

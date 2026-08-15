using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class OwnedCharactersState
{
    public List<string> characterIds = new List<string>();
}

public static class OwnedCharacters
{
    private static readonly object SyncRoot = new object();
    private static readonly HashSet<string> CharacterIds =
        new HashSet<string>(StringComparer.Ordinal);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        lock (SyncRoot)
        {
            CharacterIds.Clear();
        }
    }

    public static bool UnlockCharacter(string characterId)
    {
        if (!IsValidId(characterId))
        {
            return false;
        }

        lock (SyncRoot)
        {
            return CharacterIds.Add(characterId);
        }
    }

    public static bool IsCharacterOwned(string characterId)
    {
        if (!IsValidId(characterId))
        {
            return false;
        }

        lock (SyncRoot)
        {
            return CharacterIds.Contains(characterId);
        }
    }

    public static List<string> GetAllOwnedCharacters()
    {
        List<string> result;

        lock (SyncRoot)
        {
            result = new List<string>(CharacterIds);
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }

    public static OwnedCharactersState CaptureState()
    {
        return new OwnedCharactersState
        {
            characterIds = GetAllOwnedCharacters()
        };
    }

    public static bool RestoreState(OwnedCharactersState state)
    {
        if (state == null || state.characterIds == null)
        {
            return false;
        }

        HashSet<string> restoredCharacterIds =
            new HashSet<string>(StringComparer.Ordinal);

        foreach (string characterId in state.characterIds)
        {
            if (!IsValidId(characterId))
            {
                return false;
            }

            restoredCharacterIds.Add(characterId);
        }

        lock (SyncRoot)
        {
            CharacterIds.Clear();
            CharacterIds.UnionWith(restoredCharacterIds);
        }

        return true;
    }

    private static bool IsValidId(string characterId)
    {
        return !string.IsNullOrWhiteSpace(characterId);
    }
}

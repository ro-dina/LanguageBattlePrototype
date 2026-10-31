using System;
using System.Collections.Generic;
using System.Linq;

// Version-1 data and a species-only compatibility facade. New gameplay uses CharacterInstances.
[Serializable]
public sealed class OwnedCharactersState
{
    public List<string> characterIds = new List<string>();
}

public static class OwnedCharacters
{
    // Legacy idempotent unlock; creating additional copies is explicitly CharacterInstances.Create.
    public static bool UnlockCharacter(string characterId) => !IsCharacterOwned(characterId) && CharacterInstances.Create(characterId) != null;
    public static bool IsCharacterOwned(string characterId) => CharacterInstances.Count(characterId) > 0;
    public static List<string> GetAllOwnedCharacters() => CharacterInstances.GetAll().Select(i => i.characterId).Distinct().OrderBy(i => i, StringComparer.Ordinal).ToList();
    public static OwnedCharactersState CaptureState() => new OwnedCharactersState { characterIds = GetAllOwnedCharacters() };
    public static bool RestoreState(OwnedCharactersState state)
    {
        if (state?.characterIds == null || state.characterIds.Any(id => GameDatabase.Instance == null || !GameDatabase.Instance.ContainsCharacter(id))) return false;
        var instances = new CharacterInstanceState();
        foreach (string id in state.characterIds.Distinct())
            instances.instances.Add(CharacterInstances.GetByCharacterId(id).FirstOrDefault() ?? new CharacterInstance { instanceId = CharacterInstances.NewId(), characterId = id });
        return CharacterInstances.RestoreState(instances);
    }
}

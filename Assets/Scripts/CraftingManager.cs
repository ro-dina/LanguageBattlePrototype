using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CraftingManager
{
    public static string CombinedWord(IEnumerable<CraftingElement> elements)
    {
        if (elements == null) return null;
        var parts = new List<string>();
        foreach (var element in elements)
        {
            if (element == null || string.IsNullOrEmpty(element.Id)) return null;
            string part = element.Kind == CraftingElementKind.Material ? WordCharacterRewards.Text(element.Id) :
                element.Kind == CraftingElementKind.CharacterInstance && CharacterInstances.Get(element.Id) is CharacterInstance instance &&
                    GameDatabase.EnsureAvailable().ContainsCharacter(instance.characterId) ? GameDatabase.Instance.GetCharacter(instance.characterId).characterName : null;
            if (string.IsNullOrEmpty(part)) return null;
            parts.Add(part);
        }
        return parts.Count == 0 ? null : string.Concat(parts);
    }
    public static CharacterData Preview(IEnumerable<CraftingElement> elements,string language = "de") =>
        GameDatabase.EnsureAvailable().CharacterWords.Find(CombinedWord(elements),language);
    public static bool CanCraft(IReadOnlyList<CraftingElement> elements) => CurrencyService.CanSpend(EconomySettings.Current.craftingCost) && Prepare(elements,PlayerStateTransactions.Capture(),out _);
    private static bool Prepare(IReadOnlyList<CraftingElement> elements,SaveData data,out CharacterData result)
    {
        result = null;
        if (elements == null || elements.Count == 0) return false;
        var requirements = new Dictionary<string,int>(StringComparer.Ordinal);
        var instances = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in elements)
        {
            if (element == null || string.IsNullOrEmpty(element.Id)) return false;
            if (element.Kind == CraftingElementKind.Material)
            {
                if (!GameContentIds.IsItem(element.Id) || WordCharacterRewards.Character(element.Id) != null || WordCharacterRewards.Ambiguous(element.Id)) return false;
                requirements.TryGetValue(element.Id,out int count); if (count == int.MaxValue) return false;
                requirements[element.Id] = count+1;
            }
            else if (element.Kind == CraftingElementKind.CharacterInstance)
            {
                if (!instances.Add(element.Id) || !CharacterInstances.CanConsume(element.Id) || !data.characterInstances.instances.Any(i => i.instanceId == element.Id)) return false;
            }
            else return false;
        }
        foreach (var requirement in requirements)
            if ((data.inventory.items.Find(i => i.itemId == requirement.Key)?.count ?? 0) < requirement.Value) return false;
        result = Preview(elements);
        return result != null && GameDatabase.Instance.ContainsCharacter(result.id);
    }
    public static bool TryCraft(IReadOnlyList<CraftingElement> selection,out CharacterInstance created,out string error,PlayerOperation operation = null)
    {
        CharacterInstance output = null; var elements = selection?.ToArray();
        bool success = PlayerStateTransactions.TryChange(data => {
            if (!Prepare(elements,data,out var result) || !CurrencyService.SpendFrom(data,EconomySettings.Current.craftingCost)) return false;
            foreach (var element in elements)
            {
                if (element.Kind == CraftingElementKind.Material)
                {
                    var entry = data.inventory.items.Find(i => i.itemId == element.Id);
                    if (--entry.count == 0) data.inventory.items.Remove(entry);
                }
                else
                {
                    data.characterInstances.instances.RemoveAll(i => i.instanceId == element.Id);
                    data.characterEquipment.characters.RemoveAll(i => i.instanceId == element.Id);
                }
            }
            output = PlayerStateTransactions.AddCharacter(data,result.id); return true;
        },out error,operation);
        created = success ? output : null; return success;
    }

    // Retained for callers that still name a registered recipe. The UI uses dictionary matching above.
    public static CraftingRecipeData[] GetAllRecipes()
    {
        var json = Resources.Load<TextAsset>("Data/crafting_recipes");
        return json == null ? Array.Empty<CraftingRecipeData>() : JsonUtility.FromJson<CraftingRecipeDatabase>(json.text).recipes;
    }
    public static CraftingRecipeData GetRecipe(string id) => GetAllRecipes().FirstOrDefault(r => r.id == id);
    private static CraftingElement[] LegacySelection(CraftingRecipeData recipe)
    {
        var registered = recipe == null ? null : GetRecipe(recipe.id);
        if (registered?.inputs == null || registered.resultType != "character" || registered.resultCount != 1) return null;
        var selection = new List<CraftingElement>();
        foreach (var input in registered.inputs)
        {
            if (input == null || input.count <= 0) return null;
            var character = WordCharacterRewards.Character(input.itemId);
            if (character != null)
            {
                var owned = CharacterInstances.GetByCharacterId(character.id);
                // A legacy species-only request must never guess which duplicate to consume.
                if (owned.Count != input.count || owned.Any(i => !CharacterInstances.CanConsume(i.instanceId))) return null;
                selection.AddRange(owned.Select(i => CraftingElement.Character(i.instanceId)));
            }
            else for (int n=0;n<input.count;n++) selection.Add(CraftingElement.Material(input.itemId));
        }
        return Preview(selection)?.id == registered.resultId ? selection.ToArray() : null;
    }
    public static bool CanCraft(CraftingRecipeData recipe) { var elements=LegacySelection(recipe); return elements != null && CanCraft(elements); }
    public static bool Craft(string recipeId) => Craft(GetRecipe(recipeId));
    public static bool Craft(CraftingRecipeData recipe) { var elements=LegacySelection(recipe); return elements != null && TryCraft(elements,out _,out _); }
}

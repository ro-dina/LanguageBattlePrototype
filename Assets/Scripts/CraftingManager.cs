using System;
using System.Collections.Generic;
using UnityEngine;

public static class CraftingManager
{
    private const string RecipeResourcePath = "Data/crafting_recipes";

    private enum CraftingResultKind
    {
        InventoryItem,
        Character
    }

    private static bool recipesLoaded;
    private static Dictionary<string, CraftingRecipeData> recipesById;
    private static List<CraftingRecipeData> recipesInLoadOrder;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        recipesLoaded = false;
        recipesById = null;
        recipesInLoadOrder = null;
    }

    public static CraftingRecipeData GetRecipe(string recipeId)
    {
        EnsureRecipesLoaded();

        string normalizedId = NormalizeId(recipeId);
        if (normalizedId == null ||
            !recipesById.TryGetValue(normalizedId, out CraftingRecipeData recipe))
        {
            return null;
        }

        return CloneRecipe(recipe);
    }

    public static CraftingRecipeData[] GetAllRecipes()
    {
        EnsureRecipesLoaded();

        CraftingRecipeData[] result = new CraftingRecipeData[recipesInLoadOrder.Count];
        for (int i = 0; i < recipesInLoadOrder.Count; i++)
        {
            result[i] = CloneRecipe(recipesInLoadOrder[i]);
        }

        return result;
    }

    public static bool CanCraft(CraftingRecipeData recipe)
    {
        if (!TryResolveRegisteredRecipe(recipe, out CraftingRecipeData registeredRecipe) ||
            !TryPrepareRecipe(
                registeredRecipe,
                out Dictionary<string, int> requirements,
                out CraftingResultKind resultKind,
                out _))
        {
            return false;
        }

        if (!HasAllMaterials(requirements))
        {
            return false;
        }

        if (resultKind == CraftingResultKind.Character)
        {
            return !OwnedCharacters.IsCharacterOwned(registeredRecipe.resultId);
        }

        return CanAddInventoryResult(registeredRecipe, requirements);
    }

    public static bool Craft(string recipeId)
    {
        CraftingRecipeData recipe = GetRecipe(recipeId);
        if (recipe == null)
        {
            string displayId = string.IsNullOrWhiteSpace(recipeId) ? "(unknown)" : recipeId.Trim();
            Debug.LogWarning($"Cannot craft {displayId}: recipe not found");
            return false;
        }

        return Craft(recipe);
    }

    public static bool Craft(CraftingRecipeData recipe)
    {
        if (!TryResolveRegisteredRecipe(recipe, out CraftingRecipeData registeredRecipe))
        {
            string displayId = recipe == null || string.IsNullOrWhiteSpace(recipe.id)
                ? "(unknown)"
                : recipe.id.Trim();
            Debug.LogWarning($"Cannot craft {displayId}: recipe not found");
            return false;
        }

        if (!TryPrepareRecipe(
                registeredRecipe,
                out Dictionary<string, int> requirements,
                out CraftingResultKind resultKind,
                out string validationError))
        {
            Debug.LogWarning($"Cannot craft {GetRecipeDisplayName(registeredRecipe)}: {validationError}");
            return false;
        }

        if (resultKind == CraftingResultKind.Character &&
            OwnedCharacters.IsCharacterOwned(registeredRecipe.resultId))
        {
            Debug.LogWarning($"Cannot craft {registeredRecipe.resultId}: character already owned");
            return false;
        }

        if (!HasAllMaterials(requirements))
        {
            Debug.Log($"Cannot craft {registeredRecipe.resultId}: missing materials");
            return false;
        }

        if (resultKind == CraftingResultKind.InventoryItem &&
            !CanAddInventoryResult(registeredRecipe, requirements))
        {
            Debug.LogWarning($"Cannot craft {registeredRecipe.resultId}: inventory count would overflow");
            return false;
        }

        List<InventoryEntry> consumedItems = ToInventoryEntries(requirements);
        if (!InventoryManager.TryConsumeItems(consumedItems))
        {
            Debug.Log($"Cannot craft {registeredRecipe.resultId}: missing materials");
            return false;
        }

        bool resultGranted;
        if (resultKind == CraftingResultKind.Character)
        {
            OwnedCharacters.UnlockCharacter(registeredRecipe.resultId);
            resultGranted = OwnedCharacters.IsCharacterOwned(registeredRecipe.resultId);
        }
        else
        {
            resultGranted = InventoryManager.AddItem(
                registeredRecipe.resultId,
                registeredRecipe.resultCount);
        }

        if (!resultGranted)
        {
            RestoreConsumedItems(consumedItems);
            Debug.LogError(
                $"Cannot craft {registeredRecipe.resultId}: result could not be granted. Materials were restored.");
            return false;
        }

        Debug.Log($"Crafted {registeredRecipe.resultId}");
        return true;
    }

    private static void EnsureRecipesLoaded()
    {
        if (recipesLoaded)
        {
            return;
        }

        recipesLoaded = true;
        recipesById = new Dictionary<string, CraftingRecipeData>(StringComparer.Ordinal);
        recipesInLoadOrder = new List<CraftingRecipeData>();

        TextAsset json = Resources.Load<TextAsset>(RecipeResourcePath);
        if (json == null)
        {
            Debug.LogError(
                "crafting_recipes.json not found. Put it in Assets/Resources/Data/crafting_recipes.json");
            return;
        }

        CraftingRecipeDatabase database;
        try
        {
            database = JsonUtility.FromJson<CraftingRecipeDatabase>(json.text);
        }
        catch (Exception exception)
        {
            Debug.LogError($"crafting_recipes.json could not be parsed: {exception.Message}");
            return;
        }

        if (database == null || database.recipes == null)
        {
            Debug.LogError("crafting_recipes.json does not contain a recipes array.");
            return;
        }

        foreach (CraftingRecipeData recipe in database.recipes)
        {
            if (!TryNormalizeRecipe(recipe, out string normalizationError))
            {
                Debug.LogWarning($"Skipped an invalid crafting recipe: {normalizationError}");
                continue;
            }

            if (recipesById.ContainsKey(recipe.id))
            {
                Debug.LogWarning($"Skipped duplicate crafting recipe id: {recipe.id}");
                continue;
            }

            recipesById.Add(recipe.id, recipe);
            recipesInLoadOrder.Add(recipe);
        }

        Debug.Log($"Crafting recipes loaded: {recipesById.Count}");
    }

    private static bool TryResolveRegisteredRecipe(
        CraftingRecipeData recipe,
        out CraftingRecipeData registeredRecipe)
    {
        registeredRecipe = null;
        if (recipe == null)
        {
            return false;
        }

        EnsureRecipesLoaded();

        string normalizedId = NormalizeId(recipe.id);
        return normalizedId != null && recipesById.TryGetValue(normalizedId, out registeredRecipe);
    }

    private static bool TryNormalizeRecipe(CraftingRecipeData recipe, out string error)
    {
        error = null;
        if (recipe == null)
        {
            error = "recipe entry is null";
            return false;
        }

        recipe.id = NormalizeId(recipe.id);
        recipe.resultId = NormalizeId(recipe.resultId);
        recipe.resultType = NormalizeId(recipe.resultType)?.ToLowerInvariant();

        if (recipe.id == null)
        {
            error = "recipe id is empty";
            return false;
        }

        if (recipe.resultId == null)
        {
            error = $"recipe {recipe.id} has no resultId";
            return false;
        }

        if (!TryGetResultKind(recipe.resultType, out CraftingResultKind resultKind))
        {
            error = $"recipe {recipe.id} has unsupported resultType '{recipe.resultType}'";
            return false;
        }

        if (recipe.resultCount <= 0)
        {
            error = $"recipe {recipe.id} has an invalid resultCount";
            return false;
        }

        if (resultKind == CraftingResultKind.Character && recipe.resultCount != 1)
        {
            error = $"character recipe {recipe.id} must have resultCount 1";
            return false;
        }

        if (recipe.inputs == null || recipe.inputs.Length == 0)
        {
            error = $"recipe {recipe.id} has no inputs";
            return false;
        }

        foreach (CraftingInputData input in recipe.inputs)
        {
            if (input == null)
            {
                error = $"recipe {recipe.id} contains a null input";
                return false;
            }

            input.itemId = NormalizeId(input.itemId);
            if (input.itemId == null || input.count <= 0)
            {
                error = $"recipe {recipe.id} contains an invalid input";
                return false;
            }
        }

        if (!TryBuildRequirements(recipe, out _, out error))
        {
            return false;
        }

        return true;
    }

    private static bool TryPrepareRecipe(
        CraftingRecipeData recipe,
        out Dictionary<string, int> requirements,
        out CraftingResultKind resultKind,
        out string error)
    {
        requirements = null;
        resultKind = default;
        error = null;

        if (recipe == null ||
            string.IsNullOrWhiteSpace(recipe.resultId) ||
            recipe.resultCount <= 0 ||
            recipe.inputs == null ||
            recipe.inputs.Length == 0)
        {
            error = "invalid recipe data";
            return false;
        }

        if (!TryGetResultKind(recipe.resultType, out resultKind))
        {
            error = $"unsupported resultType '{recipe.resultType}'";
            return false;
        }

        if (resultKind == CraftingResultKind.Character && recipe.resultCount != 1)
        {
            error = "character resultCount must be 1";
            return false;
        }

        return TryBuildRequirements(recipe, out requirements, out error);
    }

    private static bool TryBuildRequirements(
        CraftingRecipeData recipe,
        out Dictionary<string, int> requirements,
        out string error)
    {
        requirements = new Dictionary<string, int>(StringComparer.Ordinal);
        error = null;

        if (recipe.inputs == null)
        {
            error = "recipe has no inputs";
            return false;
        }

        foreach (CraftingInputData input in recipe.inputs)
        {
            string itemId = input == null ? null : NormalizeId(input.itemId);
            if (itemId == null || input.count <= 0)
            {
                error = "recipe contains an invalid input";
                return false;
            }

            requirements.TryGetValue(itemId, out int currentCount);
            long combinedCount = (long)currentCount + input.count;
            if (combinedCount > int.MaxValue)
            {
                error = $"required count for {itemId} is too large";
                return false;
            }

            requirements[itemId] = (int)combinedCount;
        }

        return requirements.Count > 0;
    }

    private static bool TryGetResultKind(string resultType, out CraftingResultKind resultKind)
    {
        switch (resultType?.Trim().ToLowerInvariant())
        {
            case "character":
                resultKind = CraftingResultKind.Character;
                return true;
            case "item":
            case "word":
            case "letter":
            case "chunk":
            case "prefix":
            case "suffix":
                resultKind = CraftingResultKind.InventoryItem;
                return true;
            default:
                resultKind = default;
                return false;
        }
    }

    private static bool HasAllMaterials(Dictionary<string, int> requirements)
    {
        foreach (KeyValuePair<string, int> requirement in requirements)
        {
            if (!InventoryManager.HasItem(requirement.Key, requirement.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanAddInventoryResult(
        CraftingRecipeData recipe,
        Dictionary<string, int> requirements)
    {
        long countAfterCraft = InventoryManager.GetItemCount(recipe.resultId);
        if (requirements.TryGetValue(recipe.resultId, out int consumedResultItems))
        {
            countAfterCraft -= consumedResultItems;
        }

        countAfterCraft += recipe.resultCount;
        return countAfterCraft >= 0 && countAfterCraft <= int.MaxValue;
    }

    private static List<InventoryEntry> ToInventoryEntries(
        Dictionary<string, int> requirements)
    {
        List<InventoryEntry> entries = new List<InventoryEntry>(requirements.Count);
        foreach (KeyValuePair<string, int> requirement in requirements)
        {
            entries.Add(new InventoryEntry
            {
                itemId = requirement.Key,
                count = requirement.Value
            });
        }

        return entries;
    }

    private static void RestoreConsumedItems(IEnumerable<InventoryEntry> consumedItems)
    {
        foreach (InventoryEntry entry in consumedItems)
        {
            InventoryManager.AddItem(entry.itemId, entry.count);
        }
    }

    private static CraftingRecipeData CloneRecipe(CraftingRecipeData source)
    {
        CraftingInputData[] clonedInputs = null;
        if (source.inputs != null)
        {
            clonedInputs = new CraftingInputData[source.inputs.Length];
            for (int i = 0; i < source.inputs.Length; i++)
            {
                CraftingInputData input = source.inputs[i];
                if (input != null)
                {
                    clonedInputs[i] = new CraftingInputData
                    {
                        itemId = input.itemId,
                        count = input.count
                    };
                }
            }
        }

        return new CraftingRecipeData
        {
            id = source.id,
            inputs = clonedInputs,
            resultType = source.resultType,
            resultId = source.resultId,
            resultCount = source.resultCount
        };
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string GetRecipeDisplayName(CraftingRecipeData recipe)
    {
        if (recipe != null && !string.IsNullOrWhiteSpace(recipe.resultId))
        {
            return recipe.resultId.Trim();
        }

        if (recipe != null && !string.IsNullOrWhiteSpace(recipe.id))
        {
            return recipe.id.Trim();
        }

        return "(unknown)";
    }
}

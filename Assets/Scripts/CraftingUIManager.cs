using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CraftingUIManager : MonoBehaviour
{
    private const string GachaItemResourcePath = "Data/gacha_items";

    [SerializeField] private Transform inventoryContent;
    [SerializeField] private GameObject craftingMaterialPrefab;
    [SerializeField] private TMP_Text selectedMaterialText;
    [SerializeField] private TMP_Text combinedWordText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button craftButton;

    private readonly List<string> selectedMaterialIds = new List<string>();
    private readonly Dictionary<string, string> displayNames =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly List<GameObject> spawnedMaterialObjects = new List<GameObject>();

    private void Start()
    {
        LoadDisplayNames();
        selectedMaterialIds.Clear();
        UpdateSelectionTexts();
        SetResultText(string.Empty);
        RefreshInventory();

        if (craftButton == null)
        {
            Debug.LogWarning("Craft Button is not assigned in CraftingUIManager.");
        }
    }

    public void RefreshInventory()
    {
        ClearSpawnedMaterialObjects();

        if (inventoryContent == null)
        {
            Debug.LogError(
                "Inventory Content is not assigned in CraftingUIManager. " +
                "Assign InventoryScrollView/Viewport/Content.");
            return;
        }

        if (craftingMaterialPrefab == null)
        {
            Debug.LogError("Crafting Material Prefab is not assigned in CraftingUIManager.");
            return;
        }

        List<InventoryEntry> inventoryItems = InventoryManager.GetAllItems();
        foreach (InventoryEntry entry in inventoryItems)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.itemId) || entry.count <= 0)
            {
                continue;
            }

            GameObject materialObject = Instantiate(craftingMaterialPrefab, inventoryContent);
            materialObject.SetActive(true);
            spawnedMaterialObjects.Add(materialObject);

            TMP_Text itemNameText = FindChildText(materialObject, "ItemNameText");
            TMP_Text itemCountText = FindChildText(materialObject, "ItemCountText");

            if (itemNameText != null)
            {
                itemNameText.text = GetDisplayName(entry.itemId);
            }
            else
            {
                Debug.LogError(
                    $"ItemNameText was not found in CraftingMaterialPrefab for {entry.itemId}.");
            }

            if (itemCountText != null)
            {
                itemCountText.text = $"×{entry.count}";
            }
            else
            {
                Debug.LogError(
                    $"ItemCountText was not found in CraftingMaterialPrefab for {entry.itemId}.");
            }

            Button materialButton = materialObject.GetComponent<Button>();
            if (materialButton == null)
            {
                materialButton = materialObject.AddComponent<Button>();
                materialButton.targetGraphic = materialObject.GetComponent<Graphic>();
                Debug.LogWarning(
                    $"Button was missing on CraftingMaterialPrefab for {entry.itemId}; " +
                    "a runtime Button was added.");
            }

            string selectedItemId = entry.itemId;
            materialButton.onClick = new Button.ButtonClickedEvent();
            materialButton.onClick.AddListener(() => SelectMaterial(selectedItemId));
        }
    }

    public void ClearSelection()
    {
        selectedMaterialIds.Clear();
        UpdateSelectionTexts();
        SetResultText(string.Empty);
    }

    public void CraftSelectedMaterials()
    {
        if (selectedMaterialIds.Count == 0)
        {
            SetResultText("Select materials.");
            return;
        }

        CraftingRecipeData matchingRecipe = FindMatchingRecipe();
        if (matchingRecipe == null)
        {
            SetResultText("No matching recipe.");
            return;
        }

        if (!CraftingManager.CanCraft(matchingRecipe))
        {
            SetResultText("Not enough materials.");
            return;
        }

        if (!CraftingManager.Craft(matchingRecipe))
        {
            SetResultText("Not enough materials.");
            return;
        }

        string craftedResultId = matchingRecipe.resultId;
        selectedMaterialIds.Clear();
        UpdateSelectionTexts();
        SetResultText($"Crafted: {craftedResultId}");
        RefreshInventory();
    }

    public void BackHome()
    {
        SceneManager.LoadScene("GermanyHome");
    }

    private void SelectMaterial(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        int selectedCount = 0;
        foreach (string selectedId in selectedMaterialIds)
        {
            if (string.Equals(selectedId, itemId, StringComparison.Ordinal))
            {
                selectedCount++;
            }
        }

        if (selectedCount >= InventoryManager.GetItemCount(itemId))
        {
            SetResultText("Not enough materials.");
            return;
        }

        selectedMaterialIds.Add(itemId);
        UpdateSelectionTexts();
        SetResultText(string.Empty);
    }

    private CraftingRecipeData FindMatchingRecipe()
    {
        CraftingRecipeData[] recipes = CraftingManager.GetAllRecipes();
        foreach (CraftingRecipeData recipe in recipes)
        {
            if (RecipeInputOrderMatches(recipe))
            {
                return recipe;
            }
        }

        return null;
    }

    private bool RecipeInputOrderMatches(CraftingRecipeData recipe)
    {
        if (recipe == null || recipe.inputs == null)
        {
            return false;
        }

        int selectedIndex = 0;
        foreach (CraftingInputData input in recipe.inputs)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.itemId) || input.count <= 0)
            {
                return false;
            }

            for (int occurrence = 0; occurrence < input.count; occurrence++)
            {
                if (selectedIndex >= selectedMaterialIds.Count ||
                    !string.Equals(
                        selectedMaterialIds[selectedIndex],
                        input.itemId,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                selectedIndex++;
            }
        }

        return selectedIndex == selectedMaterialIds.Count;
    }

    private void UpdateSelectionTexts()
    {
        List<string> selectedDisplayNames = new List<string>(selectedMaterialIds.Count);
        List<string> combinedParts = new List<string>(selectedMaterialIds.Count);

        foreach (string itemId in selectedMaterialIds)
        {
            string displayName = GetDisplayName(itemId);
            selectedDisplayNames.Add(displayName);
            combinedParts.Add(RemoveDisplayHyphens(displayName));
        }

        if (selectedMaterialText != null)
        {
            selectedMaterialText.text = string.Join(" + ", selectedDisplayNames);
        }

        if (combinedWordText != null)
        {
            combinedWordText.text = string.Concat(combinedParts);
        }
    }

    private void LoadDisplayNames()
    {
        displayNames.Clear();

        TextAsset json = Resources.Load<TextAsset>(GachaItemResourcePath);
        if (json == null)
        {
            Debug.LogWarning(
                "gacha_items.json was not found. Crafting UI will display item IDs instead.");
            return;
        }

        GachaItemDatabase database;
        try
        {
            database = JsonUtility.FromJson<GachaItemDatabase>(json.text);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"gacha_items.json could not be parsed. " +
                $"Crafting UI will display item IDs instead. {exception.Message}");
            return;
        }

        if (database == null || database.items == null)
        {
            Debug.LogWarning(
                "gacha_items.json does not contain an items array. " +
                "Crafting UI will display item IDs instead.");
            return;
        }

        foreach (GachaItemData item in database.items)
        {
            if (item == null ||
                string.IsNullOrWhiteSpace(item.id) ||
                string.IsNullOrWhiteSpace(item.displayName))
            {
                continue;
            }

            string itemId = item.id.Trim();
            if (!displayNames.ContainsKey(itemId))
            {
                displayNames.Add(itemId, item.displayName.Trim());
            }
        }
    }

    private string GetDisplayName(string itemId)
    {
        return itemId != null && displayNames.TryGetValue(itemId, out string displayName)
            ? displayName
            : itemId ?? string.Empty;
    }

    private static string RemoveDisplayHyphens(string displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return string.Empty;
        }

        return displayName.Trim().Trim('-', '‐', '‑', '‒', '–', '—', '−');
    }

    private static TMP_Text FindChildText(GameObject parent, string childName)
    {
        TMP_Text[] texts = parent.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text.gameObject.name.Equals(childName, StringComparison.Ordinal))
            {
                return text;
            }
        }

        return null;
    }

    private void ClearSpawnedMaterialObjects()
    {
        foreach (GameObject materialObject in spawnedMaterialObjects)
        {
            if (materialObject != null)
            {
                materialObject.SetActive(false);
                Destroy(materialObject);
            }
        }

        spawnedMaterialObjects.Clear();
    }

    private void SetResultText(string message)
    {
        if (resultText != null)
        {
            resultText.text = message;
        }
    }
}

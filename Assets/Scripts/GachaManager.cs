using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GachaManager : MonoBehaviour
{
    [SerializeField] private TMP_Text resultText;

    private GachaItemData[] items;

    private void Start()
    {
        TextAsset json = Resources.Load<TextAsset>("Data/gacha_items");
        if (json == null)
        {
            Debug.LogError("gacha_items.json not found. Put it in Assets/Resources/Data/gacha_items.json");
            return;
        }

        GachaItemDatabase database;
        try
        {
            database = JsonUtility.FromJson<GachaItemDatabase>(json.text);
        }
        catch (System.ArgumentException exception)
        {
            Debug.LogError($"gacha_items.json could not be parsed: {exception.Message}");
            return;
        }

        if (database == null || database.items == null)
        {
            Debug.LogError("gacha_items.json does not contain an items array.");
            return;
        }

        items = System.Array.FindAll(database.items, IsValidItem);

        if (items.Length == 0)
        {
            Debug.LogError("gacha_items.json does not contain any valid items.");
        }
        else if (items.Length != database.items.Length)
        {
            Debug.LogWarning("Skipped one or more gacha item entries with no id.");
        }
    }

    public void PullOnce()
    {
        GachaItemData item = GetRandomItem();
        if (item == null) return;

        AddItemToInventory(item);

        if (resultText != null)
        {
            resultText.text =
                $"{item.rarity}\n{item.displayName}\n{item.itemType}";
        }
    }

    public void PullTen()
    {
        string result = "10 Pull Result\n\n";

        for (int i = 0; i < 10; i++)
        {
            GachaItemData item = GetRandomItem();
            if (item == null) continue;

            AddItemToInventory(item);
            result += $"{i + 1}. [{item.rarity}] {item.displayName} ({item.itemType})\n";
        }

        if (resultText != null)
        {
            resultText.text = result;
        }
    }

    private GachaItemData GetRandomItem()
    {
        if (items == null || items.Length == 0)
        {
            Debug.LogError("No gacha items loaded.");
            return null;
        }

        GachaItemData[] validItems =
            System.Array.FindAll(items, IsValidItem);

        if (validItems.Length == 0)
        {
            Debug.LogError("No valid gacha items loaded.");
            return null;
        }

        int roll = Random.Range(0, 100);

        string rarity;

        if (roll < 60) rarity = "N";
        else if (roll < 85) rarity = "R";
        else if (roll < 97) rarity = "SR";
        else rarity = "SSR";

        GachaItemData[] candidates =
            System.Array.FindAll(validItems, item => item.rarity == rarity);

        if (candidates.Length == 0)
        {
            return validItems[Random.Range(0, validItems.Length)];
        }

        return candidates[Random.Range(0, candidates.Length)];
    }

    private static bool IsValidItem(GachaItemData item)
    {
        return item != null && !string.IsNullOrWhiteSpace(item.id);
    }

    private static void AddItemToInventory(GachaItemData item)
    {
        if (!InventoryManager.AddItem(item.id, 1))
        {
            Debug.LogError($"Failed to add {item.id} to Inventory.");
            return;
        }

        Debug.Log($"Obtained {item.id}. Owned: {InventoryManager.GetItemCount(item.id)}");
    }

    public void BackHome()
    {
        SceneManager.LoadScene("GermanyHome");
    }
}

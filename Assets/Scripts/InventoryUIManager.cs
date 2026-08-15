using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class InventoryUIManager : MonoBehaviour
{
    private const string HomeSceneName = "GermanyHome";
    private const string ItemNameObjectName = "ItemNameText";
    private const string ItemCountObjectName = "ItemCountText";

    [SerializeField] private Transform content;
    [SerializeField] private GameObject inventoryItemPrefab;

    private void OnEnable()
    {
        RefreshInventory();
    }

    public void RefreshInventory()
    {
        if (content == null)
        {
            Debug.LogError(
                "Inventory content is not assigned. Set it to Scroll View / Viewport / Content in the InventoryUIManager Inspector.",
                this);
            return;
        }

        if (inventoryItemPrefab == null)
        {
            Debug.LogError(
                "Inventory item prefab is not assigned in the InventoryUIManager Inspector.",
                this);
            return;
        }

        ClearDisplayedItems();

        foreach (InventoryEntry entry in InventoryManager.GetAllItems())
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.itemId) || entry.count <= 0)
            {
                Debug.LogWarning("Skipped an invalid inventory entry while refreshing the inventory UI.", this);
                continue;
            }

            GameObject itemObject = Instantiate(inventoryItemPrefab, content);
            itemObject.SetActive(true);
            TMP_Text itemNameText = FindTextByObjectName(itemObject, ItemNameObjectName);
            TMP_Text itemCountText = FindTextByObjectName(itemObject, ItemCountObjectName);

            if (itemNameText == null || itemCountText == null)
            {
                Debug.LogError(
                    $"InventoryItemPrefab must contain TMP_Text children named '{ItemNameObjectName}' and '{ItemCountObjectName}'.",
                    itemObject);
                itemObject.SetActive(false);
                Destroy(itemObject);
                continue;
            }

            itemNameText.text = entry.itemId;
            itemCountText.text = $"×{entry.count}";
        }
    }

    public void BackHome()
    {
        SceneManager.LoadScene(HomeSceneName);
    }

    private void ClearDisplayedItems()
    {
        for (int index = content.childCount - 1; index >= 0; index--)
        {
            GameObject child = content.GetChild(index).gameObject;

            // Destroy is delayed until the end of the frame, so hide entries immediately
            // to keep repeated refreshes in the same frame from displaying duplicates.
            child.SetActive(false);
            Destroy(child);
        }
    }

    private static TMP_Text FindTextByObjectName(GameObject root, string objectName)
    {
        if (root == null || string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        TMP_Text[] textComponents = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text textComponent in textComponents)
        {
            if (textComponent != null && textComponent.gameObject.name == objectName)
            {
                return textComponent;
            }
        }

        return null;
    }
}

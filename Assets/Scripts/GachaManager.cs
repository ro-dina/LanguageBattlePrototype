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
        GachaItemDatabase database =
            JsonUtility.FromJson<GachaItemDatabase>(json.text);

        items = database.items;
    }

    public void PullOnce()
    {
        GachaItemData item = GetRandomItem();
        if (item == null) return;

        if (resultText != null)
        {
            resultText.text =
                $"{item.rarity}\n{item.displayName}\n{item.itemType}";
        }
    }

    public void PullTen()
    {
        if (resultText == null) return;

        string result = "10 Pull Result\n\n";

        for (int i = 0; i < 10; i++)
        {
            GachaItemData item = GetRandomItem();
            if (item == null) continue;
            result += $"{i + 1}. [{item.rarity}] {item.displayName} ({item.itemType})\n";
        }

        resultText.text = result;
    }

    private GachaItemData GetRandomItem()
    {
        if (items == null || items.Length == 0)
        {
            Debug.LogError("No gacha items loaded.");
            return null;
        }
        int roll = Random.Range(0, 100);

        string rarity;

        if (roll < 60) rarity = "N";
        else if (roll < 85) rarity = "R";
        else if (roll < 97) rarity = "SR";
        else rarity = "SSR";

        GachaItemData[] candidates =
            System.Array.FindAll(items, item => item.rarity == rarity);

        if (candidates.Length == 0)
        {
            return items[Random.Range(0, items.Length)];
        }

        return candidates[Random.Range(0, candidates.Length)];
    }

    public void BackHome()
    {
        SceneManager.LoadScene("GermanyHome");
    }
}
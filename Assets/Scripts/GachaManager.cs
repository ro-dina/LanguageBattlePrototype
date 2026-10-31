using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GachaManager : MonoBehaviour
{
    [SerializeField] private TMP_Text resultText;

    [SerializeField] private UnityEngine.UI.Button singleButton;
    [SerializeField] private UnityEngine.UI.Button tenButton;
    [SerializeField] private TMP_Text costText;
    private bool busy;
    private float nextActionTime;
    private const float ClickCooldown = .25f;
    private void OnEnable() => CurrencyService.BalanceChanged += BalanceChanged;
    private void OnDisable() => CurrencyService.BalanceChanged -= BalanceChanged;
    private void BalanceChanged(long _) => RefreshCosts();
    private void Update() { if (nextActionTime > 0 && Time.unscaledTime >= nextActionTime) { nextActionTime = 0; RefreshCosts(); } }
    private GachaItemData[] items;
    private GachaTable table;

    private void Start()
    {
        if (resultText != null) resultText.text = "Choose a pull.";
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
        if (items.Length > 0) table = new GachaTable(items);
        RefreshCosts();

        if (items.Length == 0)
        {
            Debug.LogError("gacha_items.json does not contain any valid items.");
        }
        else if (items.Length != database.items.Length)
        {
            Debug.LogWarning("Skipped one or more gacha item entries with no id.");
        }
    }

    public void PullOnce() => TryPull(false);
    public void PullTen() => TryPull(true);
    public bool TryPull(bool tenPull,PlayerOperation operation = null)
    {
        if (operation?.Committed == true || !CanStart(tenPull)) return false;
        var draws = new GachaItemData[tenPull ? 10 : 1];
        for (int i=0;i<draws.Length;i++) { draws[i] = GetRandomItem(); if (draws[i] == null) return false; }
        return GrantDraws(draws,tenPull,operation);
    }
    private long Cost(bool tenPull) => tenPull ? EconomySettings.Current.gachaTenCost : EconomySettings.Current.gachaSingleCost;
    private bool CanStart(bool tenPull)
    {
        if (busy || Time.unscaledTime < nextActionTime || table == null) return false;
        if (!CurrencyService.CanSpend(Cost(tenPull))) { RefreshCosts(); return false; }
        return true;
    }
    public void RefreshCosts()
    {
        if (singleButton == null || tenButton == null || costText == null) return;
        singleButton.GetComponentInChildren<TMP_Text>().text = "Pull 1 • " + EconomySettings.Price(Cost(false));
        tenButton.GetComponentInChildren<TMP_Text>().text = "Pull 10 • " + EconomySettings.Price(Cost(true));
        bool ready = !busy && table != null && Time.unscaledTime >= nextActionTime;
        singleButton.interactable = ready && CurrencyService.CanSpend(Cost(false));
        tenButton.interactable = ready && CurrencyService.CanSpend(Cost(true));
        costText.text = "Current: " + EconomySettings.Price(CurrencyService.Balance) + "  |  Pull 1: " + EconomySettings.Amount(Cost(false)) +
            "  |  Pull 10: " + EconomySettings.Amount(Cost(true));
        if (!CurrencyService.CanSpend(Cost(false))) costText.text += "\nNot enough " + EconomySettings.Current.currencyDisplayName + ".";
        else if (!CurrencyService.CanSpend(Cost(true))) costText.text += "\nNot enough " + EconomySettings.Current.currencyDisplayName + " for 10 pulls.";
    }
    // Fixed-draw entry point has the same charge, validation and command guard as random pulls.
    public bool GrantDraws(GachaItemData[] draws,bool tenPull,PlayerOperation operation = null)
    {
        if (operation?.Committed == true || !CanStart(tenPull) || draws == null || draws.Length != (tenPull ? 10 : 1)) return false;
        var rewards = new InventoryEntry[draws.Length];
        for (int i=0;i<draws.Length;i++) { if (draws[i] == null) return false; rewards[i] = new InventoryEntry(draws[i].id,1); }
        busy = true;
        try
        {
            if (!PlayerStateTransactions.TryChange(data => CurrencyService.SpendFrom(data,Cost(tenPull)) && RewardGrantService.ApplyTo(data,rewards),out string error,operation))
            { if (resultText != null) resultText.text = "Pull failed; Coins and rewards unchanged. " + error; return false; }
            string result = tenPull ? "10 Pull Result\n\n" : "";
            for (int i=0;i<draws.Length;i++)
            {
                var item = draws[i]; var character = WordCharacterRewards.Character(item.id);
                string kind = character == null ? item.itemType : "Character obtained!";
                result += (tenPull ? (i+1) + ". " : "") + $"[{item.rarity}] {ItemDisplayNames.Get(item.id)} ({kind})\n";
            }
            if (resultText != null) resultText.text = result;
            nextActionTime = Time.unscaledTime + ClickCooldown;
            return true;
        }
        finally { busy = false; RefreshCosts(); }
    }

    private GachaItemData GetRandomItem()
    {
        if (table == null)
        {
            Debug.LogError("No gacha items loaded.");
            return null;
        }
        int roll = Random.Range(0, 100);
        return table.Draw(roll, Random.Range(0, table.CandidateCount(roll)));
    }

    private static bool IsValidItem(GachaItemData item)
    {
        return item != null && !string.IsNullOrWhiteSpace(item.id);
    }

    public void BackHome()
    {
        SceneManager.LoadScene("GermanyHome");
    }
}

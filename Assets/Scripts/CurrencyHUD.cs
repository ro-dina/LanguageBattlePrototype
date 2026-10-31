using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CurrencyHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text balanceText;
    [SerializeField] private TMP_Text coinSymbol;
    [SerializeField] private Button plusButton;
    [SerializeField] private GameObject additionDialog;
    [SerializeField] private TMP_Text dialogBalanceText;
    [SerializeField] private TMP_Text dialogMessage;
    [SerializeField] private Button[] grantButtons;
    [SerializeField] private Button closeButton;
    private bool busy;
    private float nextGrantTime;
    private void OnEnable() { CurrencyService.BalanceChanged += OnBalanceChanged; }
    private void OnDisable() { CurrencyService.BalanceChanged -= OnBalanceChanged; }
    private void Start()
    {
        coinSymbol.text = EconomySettings.Current.coinSymbol;
        plusButton.gameObject.SetActive(DevelopmentCurrencyAccess.Enabled);
        plusButton.onClick.AddListener(OpenAddition);
        closeButton.onClick.AddListener(() => additionDialog.SetActive(false));
        for (int i=0;i<grantButtons.Length;i++)
        {
            int index = i;
            grantButtons[i].gameObject.SetActive(i < EconomySettings.Current.developmentGrants.Length);
            if (i < EconomySettings.Current.developmentGrants.Length)
                grantButtons[i].GetComponentInChildren<TMP_Text>().text = "+" + EconomySettings.Amount(EconomySettings.Current.developmentGrants[i]);
            grantButtons[i].onClick.AddListener(() => AddDevelopmentCoins(index));
        }
        additionDialog.SetActive(false); Refresh();
    }
    private void Update()
    {
        if (nextGrantTime > 0 && Time.unscaledTime >= nextGrantTime) { nextGrantTime = 0; Refresh(); }
    }
    private void OnBalanceChanged(long _) => Refresh();
    public void Refresh()
    {
        balanceText.text = EconomySettings.Price(CurrencyService.Balance);
        dialogBalanceText.text = EconomySettings.Current.currencyDisplayName + "\nCurrent: " + EconomySettings.Amount(CurrencyService.Balance);
        bool enabled = DevelopmentCurrencyAccess.Enabled && !busy && Time.unscaledTime >= nextGrantTime;
        foreach (var button in grantButtons) button.interactable = enabled;
    }
    public void OpenAddition()
    {
        if (!DevelopmentCurrencyAccess.Enabled) return;
        additionDialog.SetActive(true); dialogMessage.text = "Development funds"; Refresh();
    }
    public bool AddDevelopmentCoins(int index)
    {
        if (!DevelopmentCurrencyAccess.Enabled || !additionDialog.activeSelf || busy || Time.unscaledTime < nextGrantTime ||
            index < 0 || index >= EconomySettings.Current.developmentGrants.Length) return false;
        busy = true; bool success = false;
        try
        {
            DevelopmentCurrencyAccess.Run(() => {
                success = CurrencyService.TryAdd(EconomySettings.Current.developmentGrants[index],out string error,new PlayerOperation());
                dialogMessage.text = success ? "Added " + EconomySettings.Price(EconomySettings.Current.developmentGrants[index]) : "No change. " + error;
            });
            if (success) nextGrantTime = Time.unscaledTime + .25f;
            return success;
        }
        finally { busy = false; Refresh(); }
    }
}

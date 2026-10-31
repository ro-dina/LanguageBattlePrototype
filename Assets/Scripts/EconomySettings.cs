using System;
using System.Globalization;
using UnityEngine;

[Serializable]
public sealed class EconomySettings
{
    public string currencyDisplayName;
    public string coinSymbol;
    public long initialCoins;
    public long gachaSingleCost;
    public long gachaTenCost;
    public long craftingCost;
    public long[] developmentGrants;
    private static EconomySettings current;
    public static EconomySettings Current
    {
        get
        {
            if (current != null) return current;
            var json = Resources.Load<TextAsset>("Data/economy_settings");
            var value = json == null ? null : JsonUtility.FromJson<EconomySettings>(json.text);
            if (value == null || string.IsNullOrWhiteSpace(value.currencyDisplayName) || value.initialCoins < 0 ||
                value.gachaSingleCost < 0 || value.gachaTenCost < 0 || value.craftingCost < 0 ||
                value.developmentGrants == null || Array.Exists(value.developmentGrants,n => n <= 0))
                throw new InvalidOperationException("Invalid economy_settings.json.");
            return current = value;
        }
    }
    public static string Amount(long amount) => amount.ToString("N0",CultureInfo.InvariantCulture);
    public static string Price(long amount) => Amount(amount) + " " + Current.currencyDisplayName;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() => current = null;
}

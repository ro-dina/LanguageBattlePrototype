using System;
using UnityEngine;

[Serializable]
public sealed class WalletState { public long balance; }

// A command token represents one intent. Retry failures; never commit a successful token twice.
public sealed class PlayerOperation { internal bool Committed; }

public static class CurrencyService
{
    private static WalletState state;
    private static long? published;
    public static event Action<long> BalanceChanged;
    public static long Balance => state?.balance ?? EconomySettings.Current.initialCoins;
    public static WalletState CaptureState() => new WalletState { balance = Balance };
    public static bool CanSpend(long amount) => amount >= 0 && Balance >= amount;
    public static bool TryAdd(long amount,out string error,PlayerOperation operation = null)
    {
        if (amount < 0) { error = "Amount must not be negative."; return false; }
        return PlayerStateTransactions.TryChange(data => AddTo(data,amount),out error,operation);
    }
    public static bool TrySpend(long amount,out string error,PlayerOperation operation = null)
    {
        if (amount < 0) { error = "Amount must not be negative."; return false; }
        if (!CanSpend(amount)) { error = "Not enough " + EconomySettings.Current.currencyDisplayName + "."; return false; }
        return PlayerStateTransactions.TryChange(data => SpendFrom(data,amount),out error,operation);
    }
    public static bool AddTo(SaveData candidate,long amount)
    {
        if (candidate?.wallet == null || candidate.wallet.balance < 0 || amount < 0 || candidate.wallet.balance > long.MaxValue-amount) return false;
        candidate.wallet.balance += amount; return true;
    }
    public static bool SpendFrom(SaveData candidate,long amount)
    {
        if (candidate?.wallet == null || candidate.wallet.balance < 0 || amount < 0 || candidate.wallet.balance < amount) return false;
        candidate.wallet.balance -= amount; return true;
    }
    internal static void Restore(WalletState value) => state = new WalletState { balance = value.balance };
    internal static void PublishCommittedBalance()
    {
        if (published == Balance) return;
        published = Balance;
        foreach (Action<long> callback in BalanceChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
            try { callback(Balance); } catch (Exception exception) { Debug.LogException(exception); }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { state = null; published = null; BalanceChanged = null; }
}

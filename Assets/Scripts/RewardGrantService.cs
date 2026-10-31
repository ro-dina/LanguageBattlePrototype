using System.Collections.Generic;
using System.Linq;

public static class RewardGrantService
{
    public static bool Grant(string itemId,int count,out string error) => Grant(new[] { new InventoryEntry(itemId,count) },out error);
    public static bool Grant(IEnumerable<InventoryEntry> rewards,out string error)
    {
        var entries = rewards?.ToArray();
        if (entries == null || entries.Length == 0) { error = "No rewards."; return false; }
        return Grant(entries,0,out error);
    }
    public static bool Grant(IEnumerable<InventoryEntry> rewards,long coins,out string error,PlayerOperation operation = null)
    {
        var entries = rewards?.ToArray() ?? System.Array.Empty<InventoryEntry>();
        if (coins < 0 || (coins == 0 && entries.Length == 0)) { error = "No valid rewards."; return false; }
        return PlayerStateTransactions.TryChange(data => ApplyTo(data,entries,coins),out error,operation);
    }
    public static bool ApplyTo(SaveData data,IEnumerable<InventoryEntry> entries,long coins = 0,IEnumerable<string> characterIds = null)
    {
        if (!CurrencyService.AddTo(data,coins)) return false;

        foreach (var entry in entries)
        {
            if (entry == null || entry.count <= 0 || !GameContentIds.IsItem(entry.itemId) || WordCharacterRewards.Ambiguous(entry.itemId)) return false;
            if (DynamicMaterials.IsId(entry.itemId) && !DynamicMaterials.ValidEntry(entry)) return false;
            var character = WordCharacterRewards.Character(entry.itemId);
            if (character != null)
            {
                // Bound allocation by the existing save size budget, before enumerating untrusted quantities.
                if ((long)data.characterInstances.instances.Count + entry.count > SaveFileStore.MaxBytes / 64) return false;
                for (int n=0;n<entry.count;n++) PlayerStateTransactions.AddCharacter(data,character.id);
            }
            else
            {
                var held = data.inventory.items.Find(i => i.itemId == entry.itemId);
                if (held == null) data.inventory.items.Add(new InventoryEntry(entry.itemId,entry.count) { text=entry.text });
                else { if (held.count > int.MaxValue-entry.count) return false; held.count += entry.count; }
            }
        }
        foreach (string id in characterIds ?? System.Array.Empty<string>())
        {
            if (!GameDatabase.EnsureAvailable().ContainsCharacter(id) || data.characterInstances.instances.Count >= SaveFileStore.MaxBytes / 64) return false;
            PlayerStateTransactions.AddCharacter(data,id);
        }
        return true;
    }
}

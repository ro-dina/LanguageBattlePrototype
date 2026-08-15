using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class InventoryEntry
{
    public string itemId;
    public int count;

    public InventoryEntry()
    {
    }

    public InventoryEntry(string itemId, int count)
    {
        this.itemId = itemId;
        this.count = count;
    }
}

[Serializable]
public sealed class InventoryState
{
    public List<InventoryEntry> items = new List<InventoryEntry>();
}

public static class InventoryManager
{
    private static readonly object SyncRoot = new object();
    private static readonly Dictionary<string, int> ItemCounts =
        new Dictionary<string, int>(StringComparer.Ordinal);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        lock (SyncRoot)
        {
            ItemCounts.Clear();
        }
    }

    public static bool AddItem(string itemId, int count)
    {
        if (!IsValidId(itemId) || count <= 0)
        {
            return false;
        }

        lock (SyncRoot)
        {
            ItemCounts.TryGetValue(itemId, out int currentCount);

            if (currentCount > int.MaxValue - count)
            {
                return false;
            }

            ItemCounts[itemId] = currentCount + count;
            return true;
        }
    }

    public static bool RemoveItem(string itemId, int count)
    {
        if (!IsValidId(itemId) || count <= 0)
        {
            return false;
        }

        lock (SyncRoot)
        {
            if (!ItemCounts.TryGetValue(itemId, out int currentCount) ||
                currentCount < count)
            {
                return false;
            }

            int remainingCount = currentCount - count;
            if (remainingCount == 0)
            {
                ItemCounts.Remove(itemId);
            }
            else
            {
                ItemCounts[itemId] = remainingCount;
            }

            return true;
        }
    }

    public static int GetItemCount(string itemId)
    {
        if (!IsValidId(itemId))
        {
            return 0;
        }

        lock (SyncRoot)
        {
            return ItemCounts.TryGetValue(itemId, out int count) ? count : 0;
        }
    }

    public static bool HasItem(string itemId, int count)
    {
        if (!IsValidId(itemId) || count <= 0)
        {
            return false;
        }

        lock (SyncRoot)
        {
            return ItemCounts.TryGetValue(itemId, out int currentCount) &&
                   currentCount >= count;
        }
    }

    public static List<InventoryEntry> GetAllItems()
    {
        List<InventoryEntry> result;

        lock (SyncRoot)
        {
            result = new List<InventoryEntry>(ItemCounts.Count);
            foreach (KeyValuePair<string, int> item in ItemCounts)
            {
                result.Add(new InventoryEntry(item.Key, item.Value));
            }
        }

        result.Sort((left, right) =>
            StringComparer.Ordinal.Compare(left.itemId, right.itemId));
        return result;
    }

    public static bool TryConsumeItems(IEnumerable<InventoryEntry> items)
    {
        if (!TryAggregateEntries(items, out Dictionary<string, int> requiredCounts) ||
            requiredCounts.Count == 0)
        {
            return false;
        }

        lock (SyncRoot)
        {
            foreach (KeyValuePair<string, int> requiredItem in requiredCounts)
            {
                if (!ItemCounts.TryGetValue(requiredItem.Key, out int currentCount) ||
                    currentCount < requiredItem.Value)
                {
                    return false;
                }
            }

            foreach (KeyValuePair<string, int> requiredItem in requiredCounts)
            {
                int remainingCount = ItemCounts[requiredItem.Key] - requiredItem.Value;
                if (remainingCount == 0)
                {
                    ItemCounts.Remove(requiredItem.Key);
                }
                else
                {
                    ItemCounts[requiredItem.Key] = remainingCount;
                }
            }

            return true;
        }
    }

    public static InventoryState CaptureState()
    {
        return new InventoryState
        {
            items = GetAllItems()
        };
    }

    public static bool RestoreState(InventoryState state)
    {
        if (state == null || state.items == null ||
            !TryAggregateEntries(state.items, out Dictionary<string, int> restoredCounts))
        {
            return false;
        }

        lock (SyncRoot)
        {
            ItemCounts.Clear();
            foreach (KeyValuePair<string, int> item in restoredCounts)
            {
                ItemCounts.Add(item.Key, item.Value);
            }
        }

        return true;
    }

    private static bool TryAggregateEntries(
        IEnumerable<InventoryEntry> entries,
        out Dictionary<string, int> aggregatedCounts)
    {
        aggregatedCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        if (entries == null)
        {
            return false;
        }

        foreach (InventoryEntry entry in entries)
        {
            if (entry == null)
            {
                return false;
            }

            string itemId = entry.itemId;
            int count = entry.count;

            if (!IsValidId(itemId) || count <= 0)
            {
                return false;
            }

            aggregatedCounts.TryGetValue(itemId, out int currentCount);
            if (currentCount > int.MaxValue - count)
            {
                return false;
            }

            aggregatedCounts[itemId] = currentCount + count;
        }

        return true;
    }

    private static bool IsValidId(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId);
    }
}

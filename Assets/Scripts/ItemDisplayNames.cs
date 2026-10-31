using System;
using System.Collections.Generic;
using UnityEngine;

// Item IDs are storage keys; labels come from content metadata (letters, words or chunks).
public static class ItemDisplayNames
{
    private static Dictionary<string, string> names;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => names = null;
    public static string Get(string itemId)
    {
        if (itemId == null) return string.Empty;
        if (DynamicMaterials.IsId(itemId)) return InventoryManager.MaterialText(itemId);
        if (names == null)
        {
            names = new Dictionary<string, string>(StringComparer.Ordinal);
            var json = Resources.Load<TextAsset>("Data/gacha_items");
            GachaItemDatabase database = null;
            try { if (json != null) database = JsonUtility.FromJson<GachaItemDatabase>(json.text); }
            catch (ArgumentException exception) { Debug.LogWarning("Item display metadata could not be parsed: " + exception.Message); }
            if (database?.items != null)
                foreach (var item in database.items)
                    if (item != null && !string.IsNullOrWhiteSpace(item.id) && !string.IsNullOrWhiteSpace(item.displayName))
                        names[item.id] = item.displayName;
        }
        return names.TryGetValue(itemId, out var name) ? name : itemId;
    }
}

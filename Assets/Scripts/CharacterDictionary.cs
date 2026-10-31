using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class GermanWordMatching
{
    // NFC joins composed/decomposed umlauts. Invariant lowercase does not transliterate ß or umlauts.
    public static string Normalize(string word) => word?.Normalize(NormalizationForm.FormC).ToLowerInvariant();
}

public sealed class CharacterDictionary
{
    private readonly CharacterData[] characters;
    public CharacterDictionary(IEnumerable<CharacterData> source) => characters = source.ToArray();
    public CharacterData Find(string word, string language = "de")
    {
        if (string.IsNullOrEmpty(word)) return null;
        string key;
        try { key = Normalize(word,language); } catch (ArgumentException) { return null; }
        var matches = characters.Where(c => c != null && c.language == language && Normalize(c.characterName,language) == key).ToArray();
        return matches.Length == 1 ? matches[0] : null; // Never choose an arbitrary homograph.
    }
    public bool IsAmbiguous(string word, string language) => characters.Count(c => c != null && c.language == language &&
        Normalize(c.characterName,language) == Normalize(word,language)) > 1;
    private static string Normalize(string word,string language) => language == "de" ? GermanWordMatching.Normalize(word) : word?.Normalize(NormalizationForm.FormC).ToLowerInvariant();
}

// Only items explicitly classified as words participate in legacy reward conversion.
public static class WordCharacterRewards
{
    public static GachaItemData Item(string id)
    {
        var json = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Data/gacha_items");
        return json == null ? null : UnityEngine.JsonUtility.FromJson<GachaItemDatabase>(json.text).items.FirstOrDefault(i => i?.id == id);
    }
    public static CharacterData Character(string itemId)
    {
        var item = Item(itemId);
        return item?.itemType == "word" ? GameDatabase.EnsureAvailable().CharacterWords.Find(item.displayName,item.language) : null;
    }
    public static bool Ambiguous(string itemId)
    {
        var item = Item(itemId);
        return item?.itemType == "word" && GameDatabase.EnsureAvailable().CharacterWords.IsAmbiguous(item.displayName,item.language);
    }
    public static string Text(string itemId)
    {
        if (DynamicMaterials.IsId(itemId)) return InventoryManager.MaterialText(itemId);
        var item = Item(itemId);
        if (item == null) return null;
        return item.itemType == "suffix" ? item.displayName.Trim('-', '‐', '‑', '‒', '–', '—', '−') : item.displayName;
    }
}

using System;
using System.Globalization;
using System.Text;

// IDs encode NFC + invariant lowercase; the first acquired natural spelling is kept for display.
public static class DynamicMaterials
{
    public const string Prefix = "material-text-v1:";
    public const int MaxTextLength = 128;
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false,true);
    public static bool ValidText(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length > MaxTextLength) return false;
        try
        {
            string normalized = text.Normalize(NormalizationForm.FormC);
            if (normalized.Length > MaxTextLength || !char.IsLetter(normalized,0)) return false;
            for (int i=0;i<normalized.Length;i++)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(normalized,i);
                if (!char.IsLetter(normalized,i) && category != UnicodeCategory.NonSpacingMark && category != UnicodeCategory.SpacingCombiningMark) return false;
                if (char.IsHighSurrogate(normalized[i])) i++;
            }
            return Utf8.GetByteCount(normalized) <= 512;
        }
        catch (ArgumentException) { return false; }
    }
    public static string Id(string text)
    {
        if (!ValidText(text)) return null;
        return Prefix + Convert.ToBase64String(Utf8.GetBytes(GermanWordMatching.Normalize(text))).TrimEnd('=').Replace('+','-').Replace('/','_');
    }
    public static string Text(string id)
    {
        if (id == null || !id.StartsWith(Prefix,StringComparison.Ordinal) || id.Length > Prefix.Length+684) return null;
        try
        {
            string encoded = id.Substring(Prefix.Length).Replace('-','+').Replace('_','/');
            string value = Utf8.GetString(Convert.FromBase64String(encoded.PadRight((encoded.Length+3)/4*4,'=')));
            return Id(value) == id ? value : null;
        }
        catch (ArgumentException) { return null; }
        catch (FormatException) { return null; }
    }
    public static bool IsId(string id) => Text(id) != null;
    public static InventoryEntry Entry(string text,int count = 1) => new InventoryEntry(Id(text),count) { text = ValidText(text) ? text.Normalize(NormalizationForm.FormC) : null };
    public static bool ValidEntry(InventoryEntry entry) => entry != null && entry.count > 0 &&
        IsId(entry.itemId) && ValidText(entry.text) && entry.text.IsNormalized(NormalizationForm.FormC) && Id(entry.text) == entry.itemId;
}

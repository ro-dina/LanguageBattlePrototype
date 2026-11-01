using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable] public sealed class TranslationEntry { public string key, en, ja; }
[Serializable] public sealed class TranslationTable { public TranslationEntry[] entries; }

// UI-only table. Learning-language masters and sentence generation never call this service.
public static class Localization
{
    private static Dictionary<string, TranslationEntry> entries;
    private static Dictionary<string, string> sourceKeys;
    private static Regex sourcePattern;
    private static readonly HashSet<string> missing = new HashSet<string>();
    public static IReadOnlyCollection<string> MissingKeys => missing;
    private static void Ensure()
    {
        if (entries != null) return;
        var table = JsonUtility.FromJson<TranslationTable>(Resources.Load<TextAsset>("Data/ui_translations").text);
        var errors = Validate(table);
        if(table?.entries==null || table.entries.Any(e=>e==null || string.IsNullOrWhiteSpace(e.key) || string.IsNullOrEmpty(e.en)) || table.entries.Select(e=>e.key).Distinct().Count()!=table.entries.Length)
            throw new InvalidOperationException(string.Join("\n",errors));
        foreach(string warning in errors)Debug.LogWarning(warning);
        entries = table.entries.ToDictionary(e => e.key, StringComparer.Ordinal);
        sourceKeys = table.entries.GroupBy(e => e.en).ToDictionary(g => g.Key, g => g.First().key, StringComparer.Ordinal);
        // Legacy Scene labels/composed status strings resolve to the same stable keys.
        // One pass prevents substitutions from running again on German/numeric parameters or translated output.
        sourcePattern = new Regex(string.Join("|", sourceKeys.Keys.Where(k => !k.Contains("{0}"))
            .OrderByDescending(k => k.Length).Select(k => "(?<![\\p{L}\\p{N}_])" + Regex.Escape(k) + "(?![\\p{L}\\p{N}_])")), RegexOptions.CultureInvariant);
    }
    public static string Text(string key, params object[] args) => TextFor(UserSettings.Language, key, args);
    public static string Source(string key, params object[] args) => TextFor("en", key, args);
    public static string TextFor(string language, string key, params object[] args)
    {
        Ensure();
        if (!entries.TryGetValue(key ?? "", out var entry)) { missing.Add(key ?? "<null>"); return language == "ja" ? "表示できません" : "Unavailable"; }
        return FormatEntry(entry,language,args);
    }
    public static string FormatEntry(TranslationEntry entry,string language,params object[] args)
    {
        string key=entry.key;
        string value = language == "ja" && !string.IsNullOrEmpty(entry.ja) ? entry.ja : entry.en;
        try { return args.Length == 0 ? value : string.Format(CultureInfo.InvariantCulture, value, args); }
        catch (FormatException) { missing.Add(key + ":parameters"); return entry.en; }
    }
    public static string Present(string source)
    {
        if (string.IsNullOrEmpty(source) || UserSettings.Language == "en") return source ?? "";
        Ensure();
        if (sourceKeys.TryGetValue(source, out var key)) return Text(key);
        return sourcePattern.Replace(source, m => Text(sourceKeys[m.Value]));
    }
    public static string Error(string error)
    {
        Ensure();
        return sourceKeys.ContainsKey(error ?? "") ? Present(error) : Text("ui.operation.failed");
    }
    public static string[] Validate(TranslationTable table)
    {
        var errors = new List<string>(); var seen = new HashSet<string>();
        if (table?.entries == null) return new[] { "Missing translation table." };
        foreach (var e in table.entries)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.key) || !seen.Add(e.key)) { errors.Add("Duplicate/missing translation key."); continue; }
            if (string.IsNullOrEmpty(e.en) || string.IsNullOrEmpty(e.ja)) { errors.Add("Missing language: " + e.key); continue; }
            string Placeholders(string s) => string.Join(",", Regex.Matches(s, "\\{[0-9]+\\}").Cast<Match>().Select(m => m.Value).OrderBy(v => v));
            if (Placeholders(e.en) != Placeholders(e.ja)) errors.Add("Placeholder mismatch: " + e.key);
        }
        return errors.ToArray();
    }
    public static TranslationEntry[] All { get { Ensure(); return entries.Values.ToArray(); } }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { entries = null; sourceKeys = null; sourcePattern = null; missing.Clear(); }
}

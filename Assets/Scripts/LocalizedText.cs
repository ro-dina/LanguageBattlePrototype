using System;
using TMPro;
using UnityEngine;

// Retains the original presentation text. A language refresh never reruns gameplay or resets drafts/dialogue cursors.
[DisallowMultipleComponent]
public sealed class LocalizedText : MonoBehaviour
{
    private TMP_Text label;
    private string source, rendered, key;
    private object[] arguments;
    private bool preserve;
    private Func<string> provider;
    public string Source => source;
    public void Preserve() { preserve = true; key = null; provider = null; Refresh(); }
    public static void Learning(TMP_Text text) { if (text != null) Attach(text).Preserve(); }
    public static LocalizedText Attach(TMP_Text text)
    {
        var view = text.GetComponent<LocalizedText>() ?? text.gameObject.AddComponent<LocalizedText>();
        view.label = text; return view;
    }
    public static void Set(TMP_Text text, string translationKey, params object[] args)
    { if (text == null) return; var view = Attach(text); view.rendered = text.text; view.provider = null; view.preserve = false; view.key = translationKey; view.arguments = args; view.Refresh(); }
    // Providers only format captured UI values. They must never run commands, roll rewards or rebuild gameplay.
    public static void SetProvider(TMP_Text text, Func<string> format)
    { if (text == null) return; var view = Attach(text); view.rendered = text.text; view.key = null; view.preserve = false; view.provider = format; view.Refresh(); }
    private void Awake() { label = GetComponent<TMP_Text>(); source = rendered = label.text; LocalizationUI.ApplyFont(label); }
    private void LateUpdate() => Refresh();
    public void Refresh()
    {
        if (label == null) return;
        // A legacy writer can replace a previous keyed message (for example, an error replacing a preview).
        if (label.text != rendered) { source = label.text; key = null; provider = null; }
        rendered = provider != null ? provider() : preserve ? source : key == null ? Localization.Present(source) : Localization.Text(key, arguments);
        if (label.text != rendered) label.text = rendered;
    }
}

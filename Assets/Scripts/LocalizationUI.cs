using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LocalizationUI : MonoBehaviour
{
    private static TMP_FontAsset japanese;
    private static LocalizationUI instance;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        instance = new GameObject("LocalizationUI").AddComponent<LocalizationUI>(); DontDestroyOnLoad(instance.gameObject);
        SceneManager.sceneLoaded += instance.Loaded;
        UserSettings.Changed += RefreshAll;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(instance.TextChanged);
    }
    private void Loaded(Scene scene, LoadSceneMode mode) => RefreshAll();
    private void TextChanged(Object value)
    {
        if (value is TMP_Text label && label.gameObject.scene.IsValid() && label.GetComponent<LocalizedText>() == null)
            Attach(label);
    }
    private static void Attach(TMP_Text label)
    {
        var view = LocalizedText.Attach(label);
        var input = label.GetComponentInParent<TMP_InputField>();
        if (input != null && input.textComponent == label) view.Preserve();
        view.Refresh();
    }
    public static void RefreshAll()
    {
        foreach (var label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include)) Attach(label);
    }
    public static void ApplyFont(TMP_Text label)
    {
        if (japanese == null) japanese = Resources.Load<TMP_FontAsset>("Fonts/NotoSansCJKjp UI");
        // Shared runtime fallback list, not a serialized modification of the original Liberation asset.
        if (japanese != null && label.font != null && label.font != japanese && !label.font.fallbackFontAssetTable.Contains(japanese))
            label.font.fallbackFontAssetTable.Add(japanese);
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= Loaded; UserSettings.Changed -= RefreshAll;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(TextChanged); if (instance == this) instance = null;
    }
}

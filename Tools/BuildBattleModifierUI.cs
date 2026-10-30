// Run only in the disposable project, from Assets/Editor; preserve the existing scene objects.
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BuildBattleModifierUI
{
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Battle.unity");
        var manager = Object.FindAnyObjectByType<BattleManager>();
        var template = Object.FindAnyObjectByType<TMP_Text>();
        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (GameObject.Find("BattleModifierPanel") != null) throw new System.Exception("Modifier UI already exists.");
        var panel = new GameObject("BattleModifierPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(BattleModifierUI));
        var rect = (RectTransform)panel.transform;
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, 48);
        rect.sizeDelta = new Vector2(1080, 280);
        panel.GetComponent<Image>().color = new Color(0.025f, 0.065f, 0.12f, 0.88f);
        var ui = new SerializedObject(panel.GetComponent<BattleModifierUI>());
        AddText("SelectionSummary", "Optional modifiers", 16, 12, 1048, 64, 24, panel.transform, template, ui, "selectionText");
        AddButton("FutureButton", "Futur I • next turn ×1.5", 16, 88, 312, panel.transform, template, ui, "futureButton");
        AddButton("NoTenseButton", "Default tense", 336, 88, 192, panel.transform, template, ui, "noTenseButton");
        AddButton("MuessenButton", "müssen • ×2 once", 552, 88, 312, panel.transform, template, ui, "muessenButton");
        AddButton("NoModalButton", "No modal", 872, 88, 192, panel.transform, template, ui, "noModalButton");
        AddText("ModifierStatus", "Future: no attack reserved\nmüssen: available once per battle", 16, 180, 772, 80, 23, panel.transform, template, ui, "statusText");
        AddButton("ClearModifiersButton", "Clear modifiers", 808, 184, 256, panel.transform, template, ui, "clearButton");
        ui.ApplyModifiedPropertiesWithoutUndo();
        var serializedManager = new SerializedObject(manager);
        serializedManager.FindProperty("modifierUI").objectReferenceValue = panel.GetComponent<BattleModifierUI>();
        serializedManager.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new System.Exception("Scene save failed.");
        AssetDatabase.SaveAssets();
        Debug.Log("BATTLE_MODIFIER_UI_BUILT");
        EditorApplication.Exit(0);
    }
    private static RectTransform Rect(string name, float x, float y, float width, float height, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    private static TMP_Text Text(RectTransform rect, string label, float size, TMP_Text template)
    {
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = template.font; text.fontSharedMaterial = template.fontSharedMaterial;
        text.text = label; text.fontSize = size; text.enableAutoSizing = true;
        text.fontSizeMin = size - 3; text.fontSizeMax = size; text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false;
        return text;
    }
    private static void AddText(string name, string label, float x, float y, float width, float height, float size,
        Transform parent, TMP_Text template, SerializedObject ui, string field)
    { ui.FindProperty(field).objectReferenceValue = Text(Rect(name, x, y, width, height, parent), label, size, template); }
    private static void AddButton(string name, string label, float x, float y, float width,
        Transform parent, TMP_Text template, SerializedObject ui, string field)
    {
        var rect = Rect(name, x, y, width, 72, parent);
        var image = rect.gameObject.AddComponent<Image>();
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.85f); button.colors = colors;
        var textRect = Rect("Label", 8, 4, width - 16, 64, rect);
        var text = Text(textRect, label, 25, template); text.alignment = TextAlignmentOptions.Center; text.color = new Color(0.1f, 0.15f, 0.2f);
        ui.FindProperty(field).objectReferenceValue = button;
    }
}

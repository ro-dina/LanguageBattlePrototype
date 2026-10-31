// Run only in the disposable project. Existing scene objects and callbacks are retained.
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class BuildPhase91UI
{
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/CharacterSelect.unity");
        if (GameObject.Find("IndividualSelector") != null) throw new System.InvalidOperationException("Individual selector already exists; use an unmodified disposable copy.");
        var manager = Object.FindAnyObjectByType<CharacterSelectManager>();
        var serialized = new SerializedObject(manager);
        var preview = (TMP_Text)serialized.FindProperty("selectedCharacterText").objectReferenceValue;
        var canvas = Object.FindAnyObjectByType<Canvas>();
        var panel = new GameObject("IndividualSelector", typeof(RectTransform));
        var rect = (RectTransform)panel.transform; rect.SetParent(canvas.transform, false);
        rect.anchorMin = new Vector2(.44f,.16f); rect.anchorMax = new Vector2(.64f,.23f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var template = GameObject.Find("CharacterListButton").GetComponent<Button>();
        var previous = CloneButton(template, "PreviousInstanceButton", "<", rect, false);
        var next = CloneButton(template, "NextInstanceButton", ">", rect, true);
        var text = new GameObject("InstancePositionText", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        var textRect = (RectTransform)text.transform; textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(76,0); textRect.offsetMax = new Vector2(-76,0);
        text.font = preview.font; text.fontSharedMaterial = preview.fontSharedMaterial;
        text.text = "Individual 1 / 1"; text.fontSize = 25; text.enableAutoSizing = true;
        text.fontSizeMin = 20; text.fontSizeMax = 25; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        serialized.FindProperty("previousInstanceButton").objectReferenceValue = previous;
        serialized.FindProperty("nextInstanceButton").objectReferenceValue = next;
        serialized.FindProperty("instancePositionText").objectReferenceValue = text;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        preview.fontSize = preview.fontSizeMax = 28; preview.fontSizeMin = 22;
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);

        scene = EditorSceneManager.OpenScene("Assets/Scenes/Battle.unity");
        GameObject.Find("NoTenseButton").SetActive(false);
        GameObject.Find("NoModalButton").SetActive(false);
        var future = (RectTransform)GameObject.Find("FutureButton").transform;
        future.sizeDelta = new Vector2(512,72);
        var modal = (RectTransform)GameObject.Find("MuessenButton").transform;
        modal.sizeDelta = new Vector2(512,72);
        foreach (var button in new[] { future, modal })
        {
            var label = (RectTransform)button.GetComponentInChildren<TMP_Text>().transform;
            label.sizeDelta = new Vector2(496,64);
        }
        future.GetComponentInChildren<TMP_Text>().text = "Futur I • OFF • ×1.5";
        modal.GetComponentInChildren<TMP_Text>().text = "müssen • OFF • ×2";
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PHASE91_UI_BUILT"); EditorApplication.Exit(0);
    }
    private static Button CloneButton(Button template, string name, string label, Transform parent, bool right)
    {
        var button = Object.Instantiate(template, parent); button.name = name;
        button.onClick = new Button.ButtonClickedEvent();
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(right ? 1 : 0,.5f);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(72,72);
        button.GetComponentInChildren<TMP_Text>().text = label;
        return button;
    }
}

// Use only in a disposable copy. Original serialized objects and callbacks are retained.
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
public static class BuildPhase92UI
{
    private static TMP_Text fontTemplate;
    private static Button buttonTemplate;
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Crafting.unity");
        if (GameObject.Find("CraftingCharacterDialog") != null) throw new System.Exception("Crafting dialog already exists.");
        var manager = Object.FindAnyObjectByType<CraftingUIManager>(); var so = new SerializedObject(manager);
        fontTemplate = (TMP_Text)so.FindProperty("selectedMaterialText").objectReferenceValue;
        buttonTemplate = GameObject.Find("CraftButton").GetComponent<Button>();
        var canvas = Object.FindAnyObjectByType<Canvas>();
        var undo = Button("UndoSelectionButton","Undo last",canvas.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,48),new Vector2(240,72));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(undo.onClick,manager.UndoLast);
        var overlay = Rect("CraftingCharacterDialog",canvas.transform);
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one; overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        overlay.gameObject.AddComponent<Image>().color = new Color(0,0,0,.75f);
        var panel = Rect("DialogPanel",overlay); panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f,.5f); panel.sizeDelta = new Vector2(1000,720);
        panel.gameObject.AddComponent<Image>().color = new Color(.07f,.12f,.2f,1);
        var title = Text("CraftDialogTitle",panel,new Vector2(32,-24),new Vector2(936,64),32);
        var scrollRect = Rect("DialogScrollView",panel); Top(scrollRect,new Vector2(32,-108),new Vector2(936,420));
        var scroll = scrollRect.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.scrollSensitivity = 36;
        var viewport = Rect("Viewport",scrollRect); viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = viewport.offsetMax = Vector2.zero;
        viewport.gameObject.AddComponent<Image>().color = new Color(0,0,0,.01f); viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var details = Text("CraftDialogDetails",viewport,Vector2.zero,new Vector2(936,0),26);
        var dr = (RectTransform)details.transform; dr.anchorMin = new Vector2(0,1); dr.anchorMax = new Vector2(1,1); dr.sizeDelta = Vector2.zero;
        details.enableAutoSizing = false; details.alignment = TextAlignmentOptions.TopLeft;
        details.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = dr;
        var prev = Button("CraftPreviousIndividual","<",panel,new Vector2(0,0),new Vector2(0,0),new Vector2(32,104),new Vector2(88,72));
        var next = Button("CraftNextIndividual",">",panel,new Vector2(0,0),new Vector2(0,0),new Vector2(136,104),new Vector2(88,72));
        var cancel = Button("CraftDialogCancel","Cancel",panel,new Vector2(0,0),new Vector2(0,0),new Vector2(32,24),new Vector2(280,72));
        var accept = Button("CraftDialogAccept","Add individual",panel,new Vector2(1,0),new Vector2(1,0),new Vector2(-32,24),new Vector2(448,72));
        Set(so,"characterDialog",overlay.gameObject); Set(so,"dialogTitle",title); Set(so,"dialogDetails",details);
        Set(so,"dialogPrevious",prev); Set(so,"dialogNext",next); Set(so,"dialogAccept",accept); Set(so,"dialogCancel",cancel);
        so.ApplyModifiedPropertiesWithoutUndo(); overlay.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("PHASE92_UI_BUILT"); EditorApplication.Exit(0);
    }
    private static void Set(SerializedObject so,string field,Object value) => so.FindProperty(field).objectReferenceValue = value;
    private static RectTransform Rect(string name,Transform parent) { var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform; r.SetParent(parent,false); return r; }
    private static void Top(RectTransform r,Vector2 pos,Vector2 size) { r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1); r.anchoredPosition=pos; r.sizeDelta=size; }
    private static TMP_Text Text(string name,Transform parent,Vector2 pos,Vector2 size,float fontSize)
    {
        var r=Rect(name,parent); Top(r,pos,size); var t=r.gameObject.AddComponent<TextMeshProUGUI>(); t.font=fontTemplate.font; t.fontSharedMaterial=fontTemplate.fontSharedMaterial;
        t.fontSize=fontSize; t.color=Color.white; t.raycastTarget=false; t.text=""; return t;
    }
    private static Button Button(string name,string label,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size)
    {
        var b=Object.Instantiate(buttonTemplate,parent); b.name=name; b.onClick=new Button.ButtonClickedEvent();
        var r=(RectTransform)b.transform; r.anchorMin=r.anchorMax=anchor; r.pivot=pivot; r.anchoredPosition=pos; r.sizeDelta=size;
        b.GetComponentInChildren<TMP_Text>().text=label; return b;
    }
}

// Execute in a disposable project. Retain all existing objects, IDs, button events and backgrounds.
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class BuildBattleRewardUI
{
    private static TMP_Text template;
    private static Button buttonTemplate;
    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Result.unity");
        if(GameObject.Find("BattleRewardScroll")!=null || AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/BattleRewardRetryUI.prefab")!=null)
            throw new System.InvalidOperationException("Reward UI already exists; use an unmodified disposable copy.");
        var manager=new SerializedObject(Object.FindAnyObjectByType<ResultManager>());
        template=(TMP_Text)manager.FindProperty("resultText").objectReferenceValue;
        buttonTemplate=Object.FindAnyObjectByType<Button>();
        var canvas=Object.FindAnyObjectByType<Canvas>();
        var title=template.rectTransform;title.anchorMin=new Vector2(.12f,.80f);title.anchorMax=new Vector2(.88f,.95f);title.offsetMin=title.offsetMax=Vector2.zero;
        var panel=Rect("BattleRewardScroll",canvas.transform);panel.anchorMin=new Vector2(.12f,.20f);panel.anchorMax=new Vector2(.88f,.78f);panel.offsetMin=panel.offsetMax=Vector2.zero;
        panel.gameObject.AddComponent<Image>().color=new Color(.025f,.045f,.08f,.84f);
        var scroll=panel.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=48;
        var viewport=Rect("Viewport",panel);viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;viewport.offsetMin=new Vector2(28,24);viewport.offsetMax=new Vector2(-28,-24);
        viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
        var body=Text("BattleRewardText",viewport,34);var content=body.rectTransform;content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
        body.alignment=TextAlignmentOptions.TopLeft;body.enableAutoSizing=false;body.overflowMode=TextOverflowModes.Overflow;body.textWrappingMode=TextWrappingModes.Normal;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=viewport;scroll.content=content;
        Set(manager,"rewardPanel",panel.gameObject);Set(manager,"rewardText",body);manager.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var root=new GameObject("BattleRewardRetryUI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(BattleRewardRetryUI));
        root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=300;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var overlay=Rect("SaveFailureOverlay",root.transform);overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.offsetMin=overlay.offsetMax=Vector2.zero;overlay.gameObject.AddComponent<Image>().color=new Color(0,0,0,.7f);
        var box=Rect("SaveFailurePanel",overlay);box.anchorMin=box.anchorMax=box.pivot=new Vector2(.5f,.5f);box.sizeDelta=new Vector2(960,420);box.gameObject.AddComponent<Image>().color=new Color(.05f,.09f,.16f,1);
        var message=Text("RewardSaveError",box,30);Top(message.rectTransform,new Vector2(32,-24),new Vector2(896,260));message.overflowMode=TextOverflowModes.Ellipsis;message.enableAutoSizing=true;message.fontSizeMin=22;message.fontSizeMax=30;
        var retry=Object.Instantiate(buttonTemplate,box);retry.name="RetryRewardSaveButton";retry.onClick=new Button.ButtonClickedEvent();Top((RectTransform)retry.transform,new Vector2(32,-316),new Vector2(896,80));retry.GetComponentInChildren<TMP_Text>().text="Retry save";
        var so=new SerializedObject(root.GetComponent<BattleRewardRetryUI>());Set(so,"message",message);Set(so,"retry",retry);so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/UI/BattleRewardRetryUI.prefab");Object.DestroyImmediate(root);AssetDatabase.SaveAssets();Debug.Log("BATTLE_REWARD_UI_BUILT");EditorApplication.Exit(0);
    }
    private static void Set(SerializedObject so,string name,Object value)=>so.FindProperty(name).objectReferenceValue=value;
    private static RectTransform Rect(string name,Transform parent){var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);return r;}
    private static void Top(RectTransform r,Vector2 pos,Vector2 size){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=pos;r.sizeDelta=size;}
    private static TMP_Text Text(string name,Transform parent,float font){var r=Rect(name,parent);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=template.font;t.fontSharedMaterial=template.fontSharedMaterial;t.fontSize=font;t.color=Color.white;t.raycastTarget=false;t.text="";return t;}
}

// Generate new shared resources only, in a disposable validation project.
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class BuildProgressResetUI
{
    private static TMP_FontAsset font;
    private static Button template;
    public static void Run()
    {
        if(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/ProgressResetUI.prefab")!=null)throw new InvalidOperationException("Reset UI already exists.");
        font=Resources.Load<TMP_FontAsset>("Fonts/NotoSansCJKjp UI");template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/SettingsUI.prefab").GetComponentsInChildren<Button>(true).Single(b=>b.name=="SettingsClose");
        var root=Rect("ProgressResetDataUI",null);Stretch(root);var script=root.gameObject.AddComponent<ProgressResetDataUI>();var so=new SerializedObject(script);
        Set(so,"title",Text("ResetDataTitle","reset.data.title",root,0,0,1200,56,36));Set(so,"explanation",Text("ResetDataExplanation","reset.data.explanation",root,0,70,1200,90,29));
        var normal=Button("ResetNewGameButton","reset.new.game",root,0,174,1200,80);normal.image.color=new Color(1,.78f,.7f);Set(so,"newGameButton",normal);
        var development=Rect("ResetDevelopmentGroup",root);Top(development,0,284,1200,364);Set(so,"developmentGroup",development.gameObject);
        Text("ResetDevelopmentTitle","reset.development.title",development,0,0,1200,44,28);Text("ResetDevelopmentHelp","reset.development.explanation",development,0,54,1200,84,25);
        Set(so,"tutorialButton",Button("ResetTutorialsButton","reset.tutorials",development,0,150,1200,64));
        Set(so,"stageButton",Button("ResetStagesButton","reset.stages",development,0,224,1200,64));
        Set(so,"developmentNewGameButton",Button("ResetDevelopmentNewGameButton","reset.development.new.game",development,0,298,1200,64));
        so.ApplyModifiedPropertiesWithoutUndo();Save(root,"ProgressResetDataUI");
        root=Rect("ProgressResetUI",null);var canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=295;root.gameObject.AddComponent<GraphicRaycaster>();var scaler=root.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var shade=Rect("ResetShade",root);Stretch(shade);shade.gameObject.AddComponent<Image>().color=new Color(0,0,0,.8f);
        var panel=Rect("ResetPanel",root);panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);panel.sizeDelta=new Vector2(1280,900);panel.gameObject.AddComponent<Image>().color=new Color(.045f,.065f,.11f,1);
        var ui=root.gameObject.AddComponent<ProgressResetUI>();so=new SerializedObject(ui);
        Set(so,"title",Text("ResetTitle",null,panel,40,30,1200,80,40));Set(so,"description",Text("ResetDescription",null,panel,40,128,1200,260,32));
        var ack=Rect("ResetAcknowledge",panel);Top(ack,40,420,1200,106);var toggle=ack.gameObject.AddComponent<Toggle>();var hit=ack.gameObject.AddComponent<Image>();hit.color=new Color(.12f,.17f,.26f);toggle.targetGraphic=hit;
        var box=Rect("Checkbox",ack);Top(box,24,27,52,52);box.gameObject.AddComponent<Image>().color=Color.white;var mark=Rect("Checkmark",box);Stretch(mark);mark.offsetMin=new Vector2(9,9);mark.offsetMax=new Vector2(-9,-9);var image=mark.gameObject.AddComponent<Image>();image.color=new Color(.18f,.48f,.8f);image.raycastTarget=false;toggle.graphic=image;
        Set(so,"acknowledgement",Text("ResetAcknowledgeText","reset.acknowledge",ack,104,10,1072,86,30));Set(so,"acknowledge",toggle);toggle.isOn=false;
        Set(so,"message",Text("ResetMessage",null,panel,40,558,1200,120,28));
        Set(so,"cancel",Button("ResetCancelButton","ui.cancel",panel,40,730,560,108));var execute=Button("ResetExecuteButton","reset.new.game",panel,640,730,600,108);execute.image.color=new Color(1,.78f,.7f);Set(so,"execute",execute);
        so.ApplyModifiedPropertiesWithoutUndo();Save(root,"ProgressResetUI");AssetDatabase.SaveAssets();Debug.Log("PROGRESS_RESET_UI_BUILT");EditorApplication.Exit(0);
    }
    private static RectTransform Rect(string name,Transform parent){var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);return r;}
    private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    private static void Top(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    private static TMP_Text Text(string name,string key,Transform parent,float x,float y,float w,float h,float size){var r=Rect(name,parent);Top(r,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=key==null?"":Localization.Source(key);t.fontSize=size;t.color=Color.white;t.textWrappingMode=TextWrappingModes.Normal;t.alignment=TextAlignmentOptions.MidlineLeft;t.raycastTarget=false;return t;}
    private static Button Button(string name,string key,Transform parent,float x,float y,float w,float h){var b=Object.Instantiate(template,parent);b.name=name;b.onClick=new Button.ButtonClickedEvent();Top((RectTransform)b.transform,x,y,w,h);b.GetComponentInChildren<TMP_Text>().text=Localization.Source(key);return b;}
    private static void Set(SerializedObject so,string field,Object value)=>so.FindProperty(field).objectReferenceValue=value;
    private static void Save(RectTransform r,string name){PrefabUtility.SaveAsPrefabAsset(r.gameObject,"Assets/Resources/UI/"+name+".prefab");Object.DestroyImmediate(r.gameObject);}
}

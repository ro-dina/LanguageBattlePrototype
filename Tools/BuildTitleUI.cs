// Run in the disposable copy. Creates the Title scene and updates only the shared build scene list.
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class BuildTitleUI
{
    private static TMP_FontAsset font;
    public static void Run()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Title.unity") != null) throw new InvalidOperationException("Title already exists.");
        EditorSceneManager.OpenScene("Assets/Scenes/GermanyHome.unity");
        var background = Object.FindObjectsByType<Image>().Where(i=>i.sprite!=null).OrderByDescending(i=>i.rectTransform.rect.width*i.rectTransform.rect.height).First();
        Sprite sprite = background.sprite;
        font = Resources.Load<TMP_FontAsset>("Fonts/NotoSansCJKjp UI");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=Rect("TitleCanvas",null);var canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        root.gameObject.AddComponent<GraphicRaycaster>();var scaler=root.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var script=root.gameObject.AddComponent<TitleScreenController>();var so=new SerializedObject(script);Set(so,"scaler",scaler);
        var bg=Rect("TownBackground",root);Stretch(bg);var image=bg.gameObject.AddComponent<Image>();image.sprite=sprite;image.color=Color.white;image.raycastTarget=false;
        var veil=Rect("DuskVeil",root);Stretch(veil);var tint=veil.gameObject.AddComponent<Image>();tint.color=new Color(.055f,.035f,.02f,.37f);tint.raycastTarget=false;
        var viewport=Rect("TitleMenuViewport",root);Stretch(viewport);viewport.gameObject.AddComponent<RectMask2D>();
        var panel=Rect("TitleMenu",viewport);panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,1);panel.sizeDelta=new Vector2(816,1008);panel.anchoredPosition=new Vector2(0,-36);
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=panel;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=40;Set(so,"menuScroll",scroll);
        var panelImage=panel.gameObject.AddComponent<Image>();panelImage.color=new Color(.105f,.073f,.045f,.96f);var outline=panel.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.65f,.47f,.23f);outline.effectDistance=new Vector2(3,-3);
        Set(so,"menu",panel.gameObject.AddComponent<CanvasGroup>());
        Set(so,"gameTitle",Text("TitleLogo",Localization.Source("title.game.name"),panel,40,28,736,112,72,TextAlignmentOptions.Center));
        Rule(panel,48,151,720);
        Set(so,"languageLabel",Text("TitleLearningLanguage",Localization.Source("title.language"),panel,68,174,680,48,30,TextAlignmentOptions.Center));
        Set(so,"languageButton",Button("TitleDeutschButton","Deutsch",panel,68,232,680,96));
        Set(so,"continueButton",Button("TitleContinueButton",Localization.Source("title.continue"),panel,68,344,680,96));
        Set(so,"newGameButton",Button("TitleNewGameButton",Localization.Source("title.new"),panel,68,456,680,96));
        Set(so,"settingsButton",Button("TitleSettingsButton",Localization.Source("ui.settings"),panel,68,568,680,96));
        Set(so,"accountButton",Button("TitleAccountButton",Localization.Source("title.account"),panel,68,672,680,96));
        Set(so,"progressMessage",Text("TitleProgressMessage","",panel,68,792,680,128,28,TextAlignmentOptions.Center));
        Set(so,"versionLabel",Text("TitleVersion","",panel,68,938,680,42,24,TextAlignmentOptions.Center));
        var modal=Rect("TitleModal",root);Stretch(modal);modal.gameObject.AddComponent<Image>().color=new Color(0,0,0,.8f);Set(so,"modal",modal.gameObject);
        var dialog=Rect("TitleDialog",modal);dialog.anchorMin=dialog.anchorMax=dialog.pivot=new Vector2(.5f,1);dialog.sizeDelta=new Vector2(816,720);dialog.anchoredPosition=new Vector2(0,-180);dialog.gameObject.AddComponent<Image>().color=new Color(.13f,.09f,.055f,1);
        modal.gameObject.AddComponent<RectMask2D>();var dialogScroll=modal.gameObject.AddComponent<ScrollRect>();dialogScroll.viewport=modal;dialogScroll.content=dialog;dialogScroll.horizontal=false;dialogScroll.vertical=true;dialogScroll.movementType=ScrollRect.MovementType.Clamped;dialogScroll.scrollSensitivity=40;Set(so,"dialogScroll",dialogScroll);
        Set(so,"modalTitle",Text("TitleDialogHeading","",dialog,48,32,720,96,42,TextAlignmentOptions.Center));
        Set(so,"modalDescription",Text("TitleDialogDescription","",dialog,56,160,704,340,32,TextAlignmentOptions.MidlineLeft));
        Set(so,"confirmButton",Button("TitleStartButton",Localization.Source("title.start"),dialog,48,560,344,96));
        Set(so,"cancelButton",Button("TitleCancelButton",Localization.Source("ui.cancel"),dialog,424,560,344,96));
        so.ApplyModifiedPropertiesWithoutUndo();modal.gameObject.SetActive(false);
        new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        var camera = new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";
        camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;camera.GetComponent<Camera>().backgroundColor=new Color(.06f,.04f,.02f);camera.transform.position=new Vector3(0,0,-10);
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/Title.unity");
        var scenes=EditorBuildSettings.scenes.ToList();scenes.Insert(0,new EditorBuildSettingsScene("Assets/Scenes/Title.unity",true));EditorBuildSettings.scenes=scenes.ToArray();
        AssetDatabase.SaveAssets();Debug.Log("TITLE_UI_BUILT background="+AssetDatabase.GetAssetPath(sprite));EditorApplication.Exit(0);
    }
    private static RectTransform Rect(string name,Transform parent){var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);return r;}
    private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    private static void Top(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    private static void Rule(Transform parent,float x,float y,float width){var r=Rect("GoldRule",parent);Top(r,x,y,width,2);var i=r.gameObject.AddComponent<Image>();i.color=new Color(.7f,.52f,.28f);i.raycastTarget=false;}
    private static TMP_Text Text(string name,string text,Transform parent,float x,float y,float w,float h,float size,TextAlignmentOptions align)
    {
        var r=Rect(name,parent);Top(r,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.color=new Color(.97f,.89f,.72f);t.enableAutoSizing=true;t.fontSizeMin=size-4;t.fontSizeMax=size;t.fontSize=size;t.alignment=align;t.textWrappingMode=TextWrappingModes.Normal;t.richText=false;t.raycastTarget=false;return t;
    }
    private static Button Button(string name,string text,Transform parent,float x,float y,float w,float h)
    {
        var r=Rect(name,parent);Top(r,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.32f,.23f,.13f);var outline=r.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.64f,.47f,.25f);outline.effectDistance=new Vector2(1,-1);
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;var c=b.colors;c.normalColor=Color.white;c.highlightedColor=new Color(1,.91f,.7f);c.pressedColor=new Color(.72f,.6f,.38f);c.selectedColor=c.highlightedColor;c.disabledColor=new Color(.43f,.43f,.43f,.65f);b.colors=c;
        Text(name+"Label",text,r,16,8,w-32,h-16,34,TextAlignmentOptions.Center);return b;
    }
    private static void Set(SerializedObject so,string field,Object value)=>so.FindProperty(field).objectReferenceValue=value;
}

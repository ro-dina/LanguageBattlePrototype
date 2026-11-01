// Execute only in a disposable copy. Existing StageSelect objects and Inspector events are retained.
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class BuildStageTutorialUI
{
    private static TMP_Text fontTemplate;
    private static Button buttonTemplate;
    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/StageSelect.unity");
        if(Object.FindAnyObjectByType<StageSelectManager>()!=null || AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/DialogueUI.prefab")!=null)
            throw new System.InvalidOperationException("Stage tutorial UI already exists. Use an unmodified disposable copy.");
        buttonTemplate=GameObject.Find("Button").GetComponent<Button>();fontTemplate=buttonTemplate.GetComponentInChildren<TMP_Text>();
        var canvas=Object.FindAnyObjectByType<Canvas>();
        var stageRow=Rect("StageRow",null);stageRow.sizeDelta=new Vector2(1700,176);stageRow.gameObject.AddComponent<Image>().color=new Color(.05f,.09f,.16f,.94f);
        var layout=stageRow.gameObject.AddComponent<LayoutElement>();layout.minHeight=layout.preferredHeight=176;layout.flexibleWidth=1;
        var rowScript=stageRow.gameObject.AddComponent<StageRowUI>();var info=Text("StageInformation",stageRow,28);Stretch(info.rectTransform,new Vector2(0,0),new Vector2(.63f,1),new Vector2(24,12),new Vector2(-16,-12));info.enableAutoSizing=true;info.fontSizeMin=24;info.fontSizeMax=28;
        var challenge=Button("StageChallenge","Challenge",stageRow);Stretch((RectTransform)challenge.transform,new Vector2(.65f,.28f),new Vector2(.80f,.72f),Vector2.zero,Vector2.zero);
        var review=Button("StageReview","Tutorial",stageRow);Stretch((RectTransform)review.transform,new Vector2(.82f,.28f),new Vector2(.98f,.72f),Vector2.zero,Vector2.zero);
        var so=new SerializedObject(rowScript);Set(so,"information",info);Set(so,"challenge",challenge);Set(so,"review",review);so.ApplyModifiedPropertiesWithoutUndo();
        var rowAsset=PrefabUtility.SaveAsPrefabAsset(stageRow.gameObject,"Assets/Resources/UI/StageRow.prefab");Object.DestroyImmediate(stageRow.gameObject);
        var scroll=Rect("StageList",canvas.transform);Stretch(scroll,Vector2.zero,Vector2.one,new Vector2(48,184),new Vector2(-48,-180));scroll.gameObject.AddComponent<Image>().color=new Color(.025f,.045f,.08f,.3f);
        var sr=scroll.gameObject.AddComponent<ScrollRect>();sr.horizontal=false;sr.movementType=ScrollRect.MovementType.Clamped;sr.scrollSensitivity=48;
        var viewport=Rect("Viewport",scroll);Stretch(viewport,Vector2.zero,Vector2.one,new Vector2(8,8),new Vector2(-8,-8));viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
        var content=Rect("Content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
        var group=content.gameObject.AddComponent<VerticalLayoutGroup>();group.spacing=12;group.childControlHeight=group.childControlWidth=true;group.childForceExpandHeight=false;group.childForceExpandWidth=true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;sr.viewport=viewport;sr.content=content;AddScrollbar(sr);
        var title=Text("StagesTitle",canvas.transform,44);Top(title.rectTransform,new Vector2(48,-32),new Vector2(1100,72));title.text="Stages";
        var status=Text("TutorialSettingsStatus",canvas.transform,27);Top(status.rectTransform,new Vector2(48,-112),new Vector2(1600,52));
        fontTemplate.text="Feuer Practice";
        var settings=Button("TutorialSettingsButton","Tutorial settings",canvas.transform);Bottom((RectTransform)settings.transform,new Vector2(416,48),new Vector2(600,80));
        var home=Button("StageHomeButton","Home",canvas.transform);var hr=(RectTransform)home.transform;hr.anchorMin=hr.anchorMax=hr.pivot=new Vector2(1,0);hr.anchoredPosition=new Vector2(-48,48);hr.sizeDelta=new Vector2(320,80);
        var manager=GameObject.Find("SceneManager").AddComponent<StageSelectManager>();so=new SerializedObject(manager);Set(so,"content",content);Set(so,"rowPrefab",rowAsset);Set(so,"tutorialStatus",status);Set(so,"settingsButton",settings);Set(so,"homeButton",home);so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        BuildChoice();BuildDialogue();BuildFocus();AssetDatabase.SaveAssets();Debug.Log("STAGE_TUTORIAL_UI_BUILT");EditorApplication.Exit(0);
    }
    public static void UpdateStageScrollbar()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/StageSelect.unity");var scroll=GameObject.Find("StageList").GetComponent<ScrollRect>();
        if(scroll.verticalScrollbar==null)AddScrollbar(scroll);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorApplication.Exit(0);
    }
    private static void AddScrollbar(ScrollRect scroll)
    {
        var rect=Rect("StageScrollbar",scroll.transform);rect.anchorMin=new Vector2(1,0);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(1,.5f);rect.anchoredPosition=new Vector2(-8,0);rect.sizeDelta=new Vector2(20,-16);
        rect.gameObject.AddComponent<Image>().color=new Color(.08f,.12f,.18f,1);var bar=rect.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;
        var area=Rect("SlidingArea",rect);Stretch(area,Vector2.zero,Vector2.one,new Vector2(2,2),new Vector2(-2,-2));var handle=Rect("Handle",area);Stretch(handle,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);var image=handle.gameObject.AddComponent<Image>();image.color=new Color(.75f,.8f,.87f,1);bar.handleRect=handle;bar.targetGraphic=image;
        scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;scroll.viewport.offsetMax=new Vector2(-36,-8);
    }
    private static void BuildChoice()
    {
        var root=CanvasRoot("TutorialChoiceUI",260);var script=root.gameObject.AddComponent<TutorialChoiceUI>();Overlay(root,.72f);
        var panel=Rect("TutorialChoicePanel",root);panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);panel.sizeDelta=new Vector2(1240,900);panel.gameObject.AddComponent<Image>().color=new Color(.05f,.09f,.16f,1);
        var title=Text("TutorialChoiceTitle",panel,40);Top(title.rectTransform,new Vector2(32,-24),new Vector2(1176,72));
        var details=Text("TutorialChoiceDetails",panel,32);Top(details.rectTransform,new Vector2(32,-112),new Vector2(1176,200));details.enableAutoSizing=true;details.fontSizeMin=26;details.fontSizeMax=32;
        var buttons=new Button[3];for(int i=0;i<3;i++){buttons[i]=Button("TutorialOption"+i,"Option",panel);Top((RectTransform)buttons[i].transform,new Vector2(32,-336-i*100),new Vector2(1176,88));}
        var message=Text("TutorialChoiceMessage",panel,28);Top(message.rectTransform,new Vector2(32,-644),new Vector2(1176,88));
        var close=Button("TutorialChoiceClose","Close",panel);Top((RectTransform)close.transform,new Vector2(32,-780),new Vector2(1176,88));
        var so=new SerializedObject(script);Set(so,"title",title);Set(so,"details",details);Set(so,"message",message);Set(so,"close",close);Array(so,"options",buttons);so.ApplyModifiedPropertiesWithoutUndo();Save(root,"TutorialChoiceUI");
    }
    private static void BuildDialogue()
    {
        var root=CanvasRoot("DialogueUI",280);var script=root.gameObject.AddComponent<DialogueUI>();Overlay(root,.44f);
        var left=Portrait("LeftPortrait",root,false);var right=Portrait("RightPortrait",root,true);
        var lp=Text("LeftPortraitPlaceholder",left.transform,44);Stretch(lp.rectTransform,Vector2.zero,Vector2.one,new Vector2(24,24),new Vector2(-24,-24));lp.alignment=TextAlignmentOptions.Center;
        var rp=Text("RightPortraitPlaceholder",right.transform,44);Stretch(rp.rectTransform,Vector2.zero,Vector2.one,new Vector2(24,24),new Vector2(-24,-24));rp.alignment=TextAlignmentOptions.Center;
        var box=Rect("DialogueTextBox",root);box.anchorMin=new Vector2(0,0);box.anchorMax=new Vector2(1,0);box.pivot=new Vector2(.5f,0);box.sizeDelta=new Vector2(-64,300);box.anchoredPosition=new Vector2(0,24);
        box.gameObject.AddComponent<Image>().color=new Color(.025f,.045f,.08f,.98f);var boxButton=box.gameObject.AddComponent<Button>();boxButton.transition=Selectable.Transition.None;
        var speaker=Text("DialogueSpeaker",box,36);Top(speaker.rectTransform,new Vector2(40,-12),new Vector2(1450,60));speaker.color=new Color(1,.8f,.3f);
        var body=Text("DialogueText",box,32);Stretch(body.rectTransform,new Vector2(0,0),Vector2.one,new Vector2(40,92),new Vector2(-40,-84));body.alignment=TextAlignmentOptions.TopLeft;body.overflowMode=TextOverflowModes.Page;
        var pages=Text("DialoguePage",box,26);Bottom(pages.rectTransform,new Vector2(40,20),new Vector2(400,48));
        var next=Button("DialogueNext","Next",box);var nr=(RectTransform)next.transform;nr.anchorMin=nr.anchorMax=nr.pivot=new Vector2(1,0);nr.anchoredPosition=new Vector2(-32,16);nr.sizeDelta=new Vector2(240,64);
        var so=new SerializedObject(script);Set(so,"leftPortrait",left);Set(so,"rightPortrait",right);Set(so,"leftPlaceholder",lp);Set(so,"rightPlaceholder",rp);Set(so,"speaker",speaker);Set(so,"body",body);Set(so,"pageLabel",pages);Set(so,"next",next);Set(so,"textBox",boxButton);so.ApplyModifiedPropertiesWithoutUndo();Save(root,"DialogueUI");
    }
    private static Image Portrait(string name,Transform root,bool right)
    {
        var r=Rect(name,root);r.anchorMin=r.anchorMax=r.pivot=new Vector2(right?1:0,1);r.anchoredPosition=new Vector2(right?-96:96,-140);r.sizeDelta=new Vector2(440,520);var image=r.gameObject.AddComponent<Image>();image.raycastTarget=false;return image;
    }
    private static void BuildFocus()
    {
        var root=CanvasRoot("TutorialFocusUI",250);var script=root.gameObject.AddComponent<TutorialFocusUI>();var shades=new RectTransform[4];var borders=new RectTransform[4];
        for(int i=0;i<4;i++){shades[i]=Rect("TutorialShade"+i,root);shades[i].gameObject.AddComponent<Image>().color=new Color(0,0,0,.7f);}
        for(int i=0;i<4;i++){borders[i]=Rect("TutorialBorder"+i,root);var image=borders[i].gameObject.AddComponent<Image>();image.color=new Color(1,.8f,.15f,1);image.raycastTarget=false;}
        var hintPanel=Rect("TutorialHintPanel",root);hintPanel.anchorMin=hintPanel.anchorMax=hintPanel.pivot=new Vector2(.5f,1);hintPanel.anchoredPosition=new Vector2(0,-20);hintPanel.sizeDelta=new Vector2(1120,120);var bg=hintPanel.gameObject.AddComponent<Image>();bg.color=new Color(.025f,.045f,.08f,.98f);bg.raycastTarget=false;
        var hint=Text("TutorialHint",hintPanel,30);Stretch(hint.rectTransform,Vector2.zero,Vector2.one,new Vector2(24,12),new Vector2(-24,-12));hint.alignment=TextAlignmentOptions.Center;
        var so=new SerializedObject(script);Set(so,"hint",hint);Array(so,"shades",shades);Array(so,"borders",borders);so.ApplyModifiedPropertiesWithoutUndo();Save(root,"TutorialFocusUI");
    }
    private static RectTransform CanvasRoot(string name,int order)
    {
        var root=Rect(name,null);var c=root.gameObject.AddComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=order;root.gameObject.AddComponent<GraphicRaycaster>();var s=root.gameObject.AddComponent<CanvasScaler>();s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;s.referenceResolution=new Vector2(1920,1080);s.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;return root;
    }
    private static void Overlay(RectTransform root,float alpha){var r=Rect("Overlay",root);Stretch(r,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);r.gameObject.AddComponent<Image>().color=new Color(0,0,0,alpha);}
    private static void Save(RectTransform root,string name){PrefabUtility.SaveAsPrefabAsset(root.gameObject,"Assets/Resources/UI/"+name+".prefab");Object.DestroyImmediate(root.gameObject);}
    private static void Set(SerializedObject so,string name,Object value)=>so.FindProperty(name).objectReferenceValue=value;
    private static void Array<T>(SerializedObject so,string name,T[] values) where T:Object{var p=so.FindProperty(name);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
    private static RectTransform Rect(string name,Transform parent){var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);return r;}
    private static void Stretch(RectTransform r,Vector2 min,Vector2 max,Vector2 low,Vector2 high){r.anchorMin=min;r.anchorMax=max;r.offsetMin=low;r.offsetMax=high;}
    private static void Top(RectTransform r,Vector2 pos,Vector2 size){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=pos;r.sizeDelta=size;}
    private static void Bottom(RectTransform r,Vector2 pos,Vector2 size){r.anchorMin=r.anchorMax=r.pivot=Vector2.zero;r.anchoredPosition=pos;r.sizeDelta=size;}
    private static TMP_Text Text(string name,Transform parent,float font){var r=Rect(name,parent);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=fontTemplate.font;t.fontSharedMaterial=fontTemplate.fontSharedMaterial;t.fontSize=font;t.color=Color.white;t.text="";t.richText=false;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;t.alignment=TextAlignmentOptions.MidlineLeft;return t;}
    private static Button Button(string name,string label,Transform parent){var b=Object.Instantiate(buttonTemplate,parent);b.name=name;b.onClick=new Button.ButtonClickedEvent();var t=b.GetComponentInChildren<TMP_Text>();t.text=label;t.enableAutoSizing=true;t.fontSizeMin=24;t.fontSizeMax=30;return b;}
}

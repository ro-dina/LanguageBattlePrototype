// Run in a disposable copy. Creates only new resource assets; never saves existing Scenes/Prefabs.
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class BuildSettingsUI
{
    private static TMP_FontAsset font;
    private static Button template;
    public static void Run()
    {
        if(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/SettingsUI.prefab")!=null)throw new InvalidOperationException("Settings UI already exists.");
        EditorSceneManager.OpenScene("Assets/Scenes/Gacha.unity");template=GameObject.Find("PullButton").GetComponent<Button>();
        var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/NotoSansCJKjp-Regular.otf");
        font=TMP_FontAsset.CreateFontAsset(source,48,5,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);font.name="NotoSansCJKjp UI";
        AssetDatabase.CreateAsset(font,"Assets/Resources/Fonts/NotoSansCJKjp UI.asset");
        AssetDatabase.AddObjectToAsset(font.material,font);foreach(var texture in font.atlasTextures)AssetDatabase.AddObjectToAsset(texture,font);
        string characters=string.Concat(Localization.All.Select(e=>e.en+e.ja))+"日本語EnglishäöüÄÖÜß0123456789%";
        if(!font.TryAddCharacters(characters,out string missing))
        {missing=new string(missing.Where(c=>!char.IsControl(c)).Distinct().ToArray());if(missing.Length>0)throw new InvalidOperationException("Missing font glyphs: "+missing);}
        EditorUtility.SetDirty(font);
        var root=Canvas("SettingsUI",270);var script=root.gameObject.AddComponent<SettingsUI>();var so=new SerializedObject(script);
        var shade=Rect("SettingsShade",root);Stretch(shade);shade.gameObject.AddComponent<Image>().color=new Color(0,0,0,.76f);
        var panel=Rect("SettingsPanel",root);panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);panel.sizeDelta=new Vector2(1320,960);panel.gameObject.AddComponent<Image>().color=new Color(.035f,.065f,.115f,1);
        Text("SettingsTitle","Settings",panel,32,20,1100,72,44);
        var close=Button("SettingsClose","Close",panel,1080,24,208,72);Set(so,"close",close);
        string[] tabs={"General","Sound","Data"};string[] fields={"generalTab","soundTab","dataTab"};
        for(int i=0;i<3;i++)Set(so,fields[i],Button("SettingsTab"+i,tabs[i],panel,32+i*426,116,404,76));
        var general=Page("SettingsGeneral",panel);var sound=Page("SettingsSound",panel);var data=Page("SettingsData",panel);
        Set(so,"generalPage",general.gameObject);Set(so,"soundPage",sound.gameObject);Set(so,"dataPage",data.gameObject);
        Text("SettingsLanguageLabel",Localization.Source("settings.language"),general,0,0,1200,56,34);
        Set(so,"japaneseButton",Button("SettingsJapanese","日本語",general,0,80,570,88));Set(so,"englishButton",Button("SettingsEnglish","English",general,630,80,570,88));
        Text("SettingsLanguageHelp",Localization.Source("settings.language.help"),general,0,200,1200,140,31);
        Text("SettingsLearningLanguage",Localization.Source("settings.learning"),general,0,364,1200,76,30);
        Set(so,"tutorialButton",Button("SettingsTutorial",Localization.Source("ui.tut.settings"),general,0,486,1200,88));
        string[] volumeKeys={"master","bgm","sfx","voice"};string[] sliderFields={"masterSlider","bgmSlider","sfxSlider","voiceSlider"};string[] valueFields={"masterValue","bgmValue","sfxValue","voiceValue"};
        for(int i=0;i<4;i++)
        {
            float y=i*110;Text("SettingsVolumeLabel"+i,Localization.Source("settings."+volumeKeys[i]),sound,0,y,400,80,30);
            var slider=Slider("SettingsVolume"+i,sound,430,y,600,80);Set(so,sliderFields[i],slider);Set(so,valueFields[i],Text("SettingsVolumeValue"+i,"100%",sound,1060,y,140,80,32));
        }
        Set(so,"previewButton",Button("SettingsPreview",Localization.Source("settings.preview"),sound,0,470,1200,80));
        Text("SettingsPreviewHelp",Localization.Source("settings.preview.help"),sound,0,566,1200,92,27);
        Text("SettingsDataSoon",Localization.Source("settings.data.soon"),data,0,0,1200,100,36);
        Text("SettingsDataHelp",Localization.Source("settings.data.help"),data,0,132,1200,300,32);
        var message=Text("SettingsMessage","",panel,48,872,1224,64,27);message.color=new Color(1,.8f,.35f);Set(so,"message",message);
        so.ApplyModifiedPropertiesWithoutUndo();Save(root,"SettingsUI");
        root=Canvas("SettingsLauncher",210);var button=Button("SettingsOpen","Settings",root,0,0,190,64);var r=(RectTransform)button.transform;r.anchorMin=r.anchorMax=r.pivot=Vector2.one;r.anchoredPosition=new Vector2(-568,-16);
        // A simple native UI gear avoids depending on an OS emoji font.
        var label=button.GetComponentInChildren<TMP_Text>();label.rectTransform.offsetMin=new Vector2(46,0);label.rectTransform.offsetMax=new Vector2(-8,0);label.fontSizeMin=22;label.fontSizeMax=26;
        var icon=Rect("Gear",button.transform);icon.anchorMin=icon.anchorMax=new Vector2(0,.5f);icon.anchoredPosition=new Vector2(25,0);icon.sizeDelta=new Vector2(30,30);
        for(int i=0;i<4;i++){var spoke=Rect("GearSpoke"+i,icon);spoke.anchorMin=spoke.anchorMax=new Vector2(.5f,.5f);spoke.sizeDelta=new Vector2(10,32);spoke.localRotation=Quaternion.Euler(0,0,i*45);var image=spoke.gameObject.AddComponent<Image>();image.color=new Color(.07f,.12f,.2f);image.raycastTarget=false;}
        var center=Rect("GearCenter",icon);center.anchorMin=center.anchorMax=new Vector2(.5f,.5f);center.sizeDelta=new Vector2(8,8);var centerImage=center.gameObject.AddComponent<Image>();centerImage.color=Color.white;centerImage.raycastTarget=false;
        Save(root,"SettingsLauncher");AssetDatabase.SaveAssets();Debug.Log("SETTINGS_UI_BUILT");EditorApplication.Exit(0);
    }
    private static RectTransform Canvas(string name,int order){var r=Rect(name,null);var c=r.gameObject.AddComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=order;r.gameObject.AddComponent<GraphicRaycaster>();var s=r.gameObject.AddComponent<CanvasScaler>();s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;s.referenceResolution=new Vector2(1920,1080);s.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;return r;}
    private static RectTransform Rect(string name,Transform parent){var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);return r;}
    private static void Top(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    private static RectTransform Page(string name,Transform panel){var r=Rect(name,panel);Top(r,48,224,1224,664);return r;}
    private static TMP_Text Text(string name,string value,Transform parent,float x,float y,float w,float h,float size){var r=Rect(name,parent);Top(r,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.text=value;t.richText=false;t.textWrappingMode=TextWrappingModes.Normal;t.alignment=TextAlignmentOptions.MidlineLeft;t.color=Color.white;t.raycastTarget=false;return t;}
    private static Button Button(string name,string value,Transform parent,float x,float y,float w,float h){var b=Object.Instantiate(template,parent);b.name=name;b.onClick=new Button.ButtonClickedEvent();Top((RectTransform)b.transform,x,y,w,h);var text=b.GetComponentInChildren<TMP_Text>();text.font=font;text.fontSharedMaterial=font.material;text.text=value;text.enableAutoSizing=true;text.fontSizeMin=24;text.fontSizeMax=32;text.raycastTarget=false;return b;}
    private static Slider Slider(string name,Transform parent,float x,float y,float w,float h)
    {
        var r=Rect(name,parent);Top(r,x,y,w,h);var slider=r.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=100;slider.wholeNumbers=true;
        var hit=r.gameObject.AddComponent<Image>();hit.color=new Color(.12f,.18f,.28f,1);slider.targetGraphic=hit;
        var fillArea=Rect("FillArea",r);Stretch(fillArea);fillArea.offsetMin=new Vector2(20,27);fillArea.offsetMax=new Vector2(-20,-27);var fill=Rect("Fill",fillArea);Stretch(fill);fill.gameObject.AddComponent<Image>().color=new Color(.4f,.68f,.95f);slider.fillRect=fill;
        var area=Rect("HandleArea",r);Stretch(area);area.offsetMin=new Vector2(20,0);area.offsetMax=new Vector2(-20,0);var handle=Rect("Handle",area);handle.sizeDelta=new Vector2(32,0);var image=handle.gameObject.AddComponent<Image>();image.color=Color.white;slider.handleRect=handle;slider.targetGraphic=image;slider.value=100;return slider;
    }
    private static void Set(SerializedObject so,string field,Object value)=>so.FindProperty(field).objectReferenceValue=value;
    private static void Save(RectTransform r,string name){PrefabUtility.SaveAsPrefabAsset(r.gameObject,"Assets/Resources/UI/"+name+".prefab");Object.DestroyImmediate(r.gameObject);}
}

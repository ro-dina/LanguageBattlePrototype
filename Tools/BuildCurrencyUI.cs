// Run only in a disposable copy. Shared UI is a new resource prefab; only Gacha gets new cost references.
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
public static class BuildCurrencyUI
{
    private static TMP_Text template;
    private static Button buttonTemplate;
    private static SerializedObject hud;
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Gacha.unity");
        if (GameObject.Find("GachaCostText") != null || AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/CurrencyHUD.prefab") != null)
            throw new System.InvalidOperationException("Currency UI already exists; use an unmodified disposable copy.");
        template = Object.FindAnyObjectByType<TMP_Text>(); buttonTemplate = GameObject.Find("PullButton").GetComponent<Button>();
        var manager = new SerializedObject(Object.FindAnyObjectByType<GachaManager>());
        var canvas = Object.FindAnyObjectByType<Canvas>();
        var cost = Text("GachaCostText",canvas.transform,Vector2.zero,new Vector2(0,0),25);
        var cr = (RectTransform)cost.transform; cr.anchorMin = new Vector2(.52f,.13f); cr.anchorMax = new Vector2(.975f,.19f); cr.offsetMin = cr.offsetMax = Vector2.zero;
        Set(manager,"singleButton",buttonTemplate); Set(manager,"tenButton",GameObject.Find("PullTenButton").GetComponent<Button>());Set(manager,"costText",cost);manager.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);

        var root = new GameObject("CurrencyHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(CurrencyHUD));
        var sharedCanvas = root.GetComponent<Canvas>();sharedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;sharedCanvas.sortingOrder = 200;
        var scaler = root.GetComponent<CanvasScaler>();scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution = new Vector2(1920,1080);scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        hud = new SerializedObject(root.GetComponent<CurrencyHUD>());
        var bar = Rect("CoinBar",root.transform);bar.anchorMin = bar.anchorMax = bar.pivot = Vector2.one;bar.anchoredPosition = new Vector2(-32,-16);bar.sizeDelta = new Vector2(520,64);
        bar.gameObject.AddComponent<Image>().color = new Color(.025f,.055f,.1f,.94f);
        var symbol = Text("CoinSymbol",bar,new Vector2(12,-2),new Vector2(48,60),28);symbol.color = new Color(1,.78f,.25f);symbol.alignment = TextAlignmentOptions.Center;
        var value = Text("CoinsBalanceText",bar,new Vector2(64,-2),new Vector2(364,60),28);value.enableAutoSizing = true;value.fontSizeMin = 18;value.fontSizeMax = 28;value.alignment = TextAlignmentOptions.MidlineRight;
        var plus = Button("CoinsPlusButton","+",bar,new Vector2(440,-0),new Vector2(80,64));
        var overlay = Rect("CoinAdditionDialog",root.transform);overlay.anchorMin = Vector2.zero;overlay.anchorMax = Vector2.one;overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        overlay.gameObject.AddComponent<Image>().color = new Color(0,0,0,.7f);
        var panel = Rect("CoinAdditionPanel",overlay);panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f,.5f);panel.sizeDelta = new Vector2(600,600);
        panel.gameObject.AddComponent<Image>().color = new Color(.05f,.09f,.16f,1);
        var current = Text("CoinDialogBalance",panel,new Vector2(32,-24),new Vector2(536,100),30);
        var buttons = new Button[3];for (int i=0;i<buttons.Length;i++) buttons[i] = Button("AddCoins"+i,"+",panel,new Vector2(32,-(148+i*92)),new Vector2(536,72));
        var message = Text("CoinDialogMessage",panel,new Vector2(32,-424),new Vector2(536,64),23);
        var close = Button("CloseCoinDialog","Close",panel,new Vector2(32,-504),new Vector2(536,72));
        Set(hud,"balanceText",value);Set(hud,"coinSymbol",symbol);Set(hud,"plusButton",plus);Set(hud,"additionDialog",overlay.gameObject);Set(hud,"dialogBalanceText",current);Set(hud,"dialogMessage",message);Set(hud,"closeButton",close);
        var grants=hud.FindProperty("grantButtons");grants.arraySize=buttons.Length;for(int i=0;i<buttons.Length;i++)grants.GetArrayElementAtIndex(i).objectReferenceValue=buttons[i];hud.ApplyModifiedPropertiesWithoutUndo();overlay.gameObject.SetActive(false);
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI")) AssetDatabase.CreateFolder("Assets/Resources","UI");
        PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/UI/CurrencyHUD.prefab");Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
        Debug.Log("CURRENCY_UI_BUILT");EditorApplication.Exit(0);
    }
    private static void Set(SerializedObject so,string name,Object value) => so.FindProperty(name).objectReferenceValue=value;
    private static RectTransform Rect(string name,Transform parent) {var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);return r;}
    private static void Top(RectTransform r,Vector2 pos,Vector2 size){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=pos;r.sizeDelta=size;}
    private static TMP_Text Text(string name,Transform parent,Vector2 pos,Vector2 size,float font)
    {var r=Rect(name,parent);Top(r,pos,size);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=template.font;t.fontSharedMaterial=template.fontSharedMaterial;t.fontSize=font;t.text="";t.color=Color.white;t.raycastTarget=false;t.alignment=TextAlignmentOptions.MidlineLeft;return t;}
    private static Button Button(string name,string label,Transform parent,Vector2 pos,Vector2 size)
    {var b=Object.Instantiate(buttonTemplate,parent);b.name=name;b.onClick=new Button.ButtonClickedEvent();Top((RectTransform)b.transform,pos,size);b.GetComponentInChildren<TMP_Text>().text=label;return b;}
}

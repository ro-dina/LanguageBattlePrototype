using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Four raycast shades leave a real hole above the allowed original button.
public sealed class TutorialFocusUI:MonoBehaviour
{
    [SerializeField] private RectTransform[] shades;
    [SerializeField] private RectTransform[] borders;
    [SerializeField] private TMP_Text hint;
    private RectTransform target;
    private TMP_Text featureNotice;
    private TutorialController guide;
    public void RefreshFeatures(TutorialController value)
    {
        guide=value;
        bool visible=value!=null && value.RestrictsFeatures && System.Enum.GetValues(typeof(TutorialFeature)).Cast<TutorialFeature>().Any(f=>!value.FeatureAvailable(f));
        if(visible && featureNotice==null)
        {
            featureNotice=Instantiate(hint,hint.transform.parent);featureNotice.name="TutorialFeatureNotice";featureNotice.raycastTarget=false;
            featureNotice.enableAutoSizing=false;featureNotice.fontSize=22;featureNotice.fontStyle=FontStyles.Normal;featureNotice.overflowMode=TextOverflowModes.Overflow;
            var rect=featureNotice.rectTransform;rect.anchorMin=new Vector2(0,0);rect.anchorMax=new Vector2(1,0);rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,8);rect.sizeDelta=new Vector2(-48,56);
            LocalizedText.SetProvider(featureNotice,()=>Localization.Text("tutorial.features.later",string.Join(" / ",System.Enum.GetValues(typeof(TutorialFeature)).Cast<TutorialFeature>()
                .Where(f=>guide!=null && !guide.FeatureAvailable(f)).Select(f=>Localization.Text("tutorial.feature."+f.ToString().ToLowerInvariant())))));
        }
        if(featureNotice!=null)featureNotice.gameObject.SetActive(visible);
        var panel=(RectTransform)hint.transform.parent;panel.sizeDelta=new Vector2(panel.sizeDelta.x,visible?156:120);
        var h=hint.rectTransform;
        h.anchorMin=new Vector2(0,visible?1:0);h.anchorMax=Vector2.one;h.pivot=new Vector2(.5f,visible?1:.5f);
        h.anchoredPosition=new Vector2(0,visible?-12:0);h.sizeDelta=new Vector2(-48,visible?76:-24);
    }
    public void OfferExit(Action stop)
    {
        var template=Resources.Load<GameObject>("UI/SettingsUI").GetComponentsInChildren<Button>(true).First(b=>b.name=="SettingsClose");
        var button=Instantiate(template,transform);button.name="TutorialStopButton";button.onClick=new Button.ButtonClickedEvent();button.onClick.AddListener(()=>stop());
        var r=(RectTransform)button.transform;r.anchorMin=r.anchorMax=r.pivot=Vector2.one;r.anchoredPosition=new Vector2(-32,-208);r.sizeDelta=new Vector2(320,72);
        LocalizedText.Set(button.GetComponentInChildren<TMP_Text>(),"tutorial.stop");
    }
    public void Focus(RectTransform value,string text){target=value;hint.text=text;gameObject.SetActive(true);Reposition();}
    public void Hint(string text){target=null;hint.text=text;foreach(var s in shades)s.gameObject.SetActive(false);foreach(var b in borders)b.gameObject.SetActive(false);gameObject.SetActive(!string.IsNullOrEmpty(text));}
    private void LateUpdate(){if(guide!=null)RefreshFeatures(guide);if(target!=null)Reposition();}
    private static void Place(RectTransform rect,Vector2 min,Vector2 max)
    {rect.gameObject.SetActive(true);rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;rect.anchoredPosition=min;rect.sizeDelta=Vector2.Max(Vector2.zero,max-min);}
    private void Reposition()
    {
        if(target==null)return;var root=(RectTransform)transform;var corners=new Vector3[4];target.GetWorldCorners(corners);
        var canvas=target.GetComponentInParent<Canvas>().rootCanvas;var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        var ownCanvas=GetComponent<Canvas>();var ownCamera=ownCanvas.renderMode==RenderMode.ScreenSpaceOverlay?null:ownCanvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root,RectTransformUtility.WorldToScreenPoint(camera,corners[0]),ownCamera,out var low);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root,RectTransformUtility.WorldToScreenPoint(camera,corners[2]),ownCamera,out var high);
        low-=root.rect.min+new Vector2(6,6);high-=root.rect.min-new Vector2(6,6);float w=root.rect.width,h=root.rect.height;
        low=Vector2.Max(Vector2.zero,low);high=Vector2.Min(new Vector2(w,h),high);
        Place(shades[0],new Vector2(0,high.y),new Vector2(w,h));Place(shades[1],Vector2.zero,new Vector2(w,low.y));
        Place(shades[2],new Vector2(0,low.y),new Vector2(low.x,high.y));Place(shades[3],new Vector2(high.x,low.y),new Vector2(w,high.y));
        Place(borders[0],low,new Vector2(high.x,low.y+4));Place(borders[1],new Vector2(low.x,high.y-4),high);
        Place(borders[2],low,new Vector2(low.x+4,high.y));Place(borders[3],new Vector2(high.x-4,low.y),high);
    }
}

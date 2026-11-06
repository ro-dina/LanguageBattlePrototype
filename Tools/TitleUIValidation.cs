#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public sealed class TitleUIValidation:MonoBehaviour
{
    private readonly List<string> passes=new List<string>(),failures=new List<string>();
    private string output;
    private static readonly Vector2Int[] sizes={new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1600,1200),new Vector2Int(2560,1080),new Vector2Int(390,844),new Vector2Int(844,390),new Vector2Int(800,600)};
    private static Button B(string name)=>Object.FindObjectsByType<Button>().Single(b=>b.name==name);
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);
    private void Awake(){DontDestroyOnLoad(gameObject);Application.runInBackground=true;var args=Environment.GetCommandLineArgs();output=args[Array.IndexOf(args,"-languageBattleUIOutput")+1];Directory.CreateDirectory(output);}
    private void Start()=>StartCoroutine(Safe());
    private void Check(bool condition,string message){(condition?passes:failures).Add(message);}
    private IEnumerator Safe()
    {
        var stack=new Stack<IEnumerator>();stack.Push(Run());while(stack.Count>0)
        {
            bool more=false;object value=null;try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){failures.Add(e.ToString());break;}
            if(!more){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;
        }
        File.WriteAllText(Path.Combine(output,"validation.txt"),"PASSES\n"+string.Join("\n",passes)+"\nFAILURES\n"+string.Join("\n",failures));Debug.Log("TITLE_UI_COMPLETE "+passes.Count+" passed, "+failures.Count+" failed");EditorApplication.Exit(failures.Count==0?0:1);
    }
    private void Click(string name)
    {
        var button=B(name);Canvas.ForceUpdateCanvases();var r=(RectTransform)button.transform;var canvas=button.GetComponentInParent<Canvas>().rootCanvas;
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,r.TransformPoint(r.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Pointer reaches "+name);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    private IEnumerator Load(string scene){SceneManager.LoadScene(scene);yield return null;yield return null;}
    private static string Fingerprint(string path)=>Directory.Exists(path)?string.Join(";",Directory.GetFiles(path,"*",SearchOption.AllDirectories).OrderBy(p=>p).Select(p=>p+File.GetLastWriteTimeUtc(p).Ticks+Convert.ToBase64String(File.ReadAllBytes(p)))):"missing";
    private IEnumerator Run()
    {
        yield return Load("Title");
        if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        string directory=PlayerSaveService.DefaultDirectory();if(Directory.Exists(directory))Directory.Delete(directory,true);
        foreach(string language in new[]{"ja","en"})
        {
            UserSettings.Initialize(Path.Combine(directory,"../TitleUI-Settings-"+language),language=="ja"?SystemLanguage.Japanese:SystemLanguage.English);
            yield return Load("Title");string before=Fingerprint(directory);yield return RenderSizes(language+"-Title");Check(Fingerprint(directory)==before,language+" title-only renders leave progress untouched");
            Click("TitleAccountButton");yield return null;yield return RenderSizes(language+"-Account");Click("TitleCancelButton");yield return null;
            Click("TitleNewGameButton");yield return null;yield return RenderSizes(language+"-NewGameConfirmation");Click("TitleCancelButton");yield return null;Check(Fingerprint(directory)==before,"Cancel leaves progress untouched");
            Click("TitleSettingsButton");yield return null;yield return RenderSizes(language+"-Settings",false);Click("SettingsTab1");yield return null;yield return RenderSizes(language+"-Sound",false);Click("SettingsClose");yield return null;
            Click("TitleNewGameButton");yield return null;Click("TitleStartButton");yield return null;yield return null;
            Check(SceneManager.GetActiveScene().name=="GermanyHome",language+" Start opens Home");
            string state=JsonUtility.ToJson(PlayerSaveService.Instance.Capture());
            Click("SettingsOpen");yield return null;Click("SettingsClose");yield return null;Check(SceneManager.GetActiveScene().name=="GermanyHome","Home Settings returns to Home");
            Click("HomeTitleButton");yield return null;yield return null;before=Fingerprint(directory);yield return RenderSizes(language+"-ContinueReady");Check(Fingerprint(directory)==before,"Returning to title does not save");
            Click("TitleContinueButton");yield return null;yield return null;Check(JsonUtility.ToJson(PlayerSaveService.Instance.Capture())==state,"Continue preserves progress");
            Click("HomeTitleButton");yield return null;yield return null;
        }
        string main=PlayerSaveService.Instance.Files.MainPath;Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);File.WriteAllText(main,"broken");File.WriteAllText(main+".bak","broken");
        yield return Load("Title");Check(!B("TitleContinueButton").interactable,"Corrupt progress has no Continue");yield return RenderSizes("en-Corrupt");
        File.WriteAllText(main,"{\"version\":99}");yield return Load("Title");Check(!B("TitleContinueButton").interactable&&!B("TitleNewGameButton").interactable,"Future version protects both actions");yield return RenderSizes("en-Unsupported");
    }
    private IEnumerator RenderSizes(string label,bool narrow=true)
    {
        foreach(var size in sizes.Where(s=>narrow||s.x>=1280))
        {
            var owner=TitleScreenController.Active;owner.enabled=false;var camera=Camera.main;
            var target=new RenderTexture(size.x,size.y,24);target.Create();camera.targetTexture=target;
            var canvases=Object.FindObjectsByType<Canvas>().Where(c=>c.isRootCanvas).ToArray();foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
            yield return null;Canvas.ForceUpdateCanvases();
            typeof(TitleScreenController).GetMethod("RefreshLayout",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,new object[]{new Vector2(size.x,size.y)});
            Canvas.ForceUpdateCanvases();yield return null;
            foreach(var text in Object.FindObjectsByType<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextOverflowing,label+" "+size+" text fits "+text.name);}
            var scroll=Field<ScrollRect>(owner,TitleScreenController.IsModalOpen?"dialogScroll":"menuScroll");
            foreach(var button in Object.FindObjectsByType<Button>().Where(b=>b.IsInteractable()&&b.transform.IsChildOf(owner.transform)))
            {
                var r=(RectTransform)button.transform;Vector3[] corners=new Vector3[4];r.GetWorldCorners(corners);var p=corners.Select(camera.WorldToScreenPoint).ToArray();
                Check(p.All(v=>v.x>=-1&&v.x<=size.x+1),label+" "+size+" button within width "+button.name);
                if(size.x<960)Check(p[1].y-p[0].y>=43.9f,label+" "+size+" touch height >=44 "+button.name);
            }
            Save(camera,target,label+"-"+size.x+"x"+size.y);
            if(scroll.content.rect.height>scroll.viewport.rect.height)
            {
                scroll.verticalNormalizedPosition=0;yield return null;Canvas.ForceUpdateCanvases();Save(camera,target,label+"-Scrolled-"+size.x+"x"+size.y);
                if(!SettingsUI.IsOpen)
                {
                    var button=TitleScreenController.IsModalOpen?B("TitleCancelButton"):B("TitleAccountButton");var r=(RectTransform)button.transform;var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,r.TransformPoint(r.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,label+" narrow scroll reaches "+button.name);
                }
            }
            camera.targetTexture=null;foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;}target.Release();Destroy(target);owner.enabled=true;typeof(TitleScreenController).GetField("lastScreen",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,Vector2.zero);yield return null;yield return null;
        }
    }
    private void Save(Camera camera,RenderTexture target,string label)
    {
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});var old=RenderTexture.active;RenderTexture.active=target;var png=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,target.width,target.height),0,0);png.Apply();File.WriteAllBytes(Path.Combine(output,label+".png"),png.EncodeToPNG());RenderTexture.active=old;Destroy(png);
    }
}
#endif

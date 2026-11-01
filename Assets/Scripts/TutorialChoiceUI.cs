using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TutorialChoiceUI:MonoBehaviour
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text details;
    [SerializeField] private TMP_Text message;
    [SerializeField] private Button[] options;
    [SerializeField] private Button close;
    private static TutorialChoiceUI active;
    private bool busy;
    private static TutorialChoiceUI Create()
    {
        if(active!=null)return null;
        var asset=Resources.Load<GameObject>("UI/TutorialChoiceUI");if(asset==null)return null;
        active=Instantiate(asset).GetComponent<TutorialChoiceUI>();active.close.onClick.AddListener(active.Close);return active;
    }
    public static void Request(string stageId,bool review)
    {
        if(!TutorialController.NavigationAllowed)return;
        var stage=StageCatalog.Get(stageId);
        if(stage==null || !StageProgress.IsUnlocked(stageId))return;
        if(string.IsNullOrEmpty(stage.tutorialId)){Launch(stage,false);return;}
        if(!review && !TutorialPreferences.NeedsChoice(stage.tutorialId)){Launch(stage,TutorialPreferences.DefaultGuided(stage.tutorialId));return;}
        var ui=Create();if(ui==null)return;
        var prior=TutorialPreferences.Get(stage.tutorialId);
        bool replay=review && (TutorialPreferences.SkipAll || prior?.lastChoice is not (null or TutorialChoice.None) || StageProgress.IsCleared(stage.stageId));
        ui.title.text=replay?"Replay tutorial • "+stage.displayName:"Tutorial • "+stage.displayName;
        ui.details.text=stage.stageDescription+"\n\n"+(TutorialCatalog.IsImplemented(stage.tutorialId)?"Choose guided practice or a normal battle. Rewards are the same.":"Detailed guidance is not available yet. All choices currently start a normal battle; this tutorial stays incomplete.");
        ui.Set(0,replay?"Take the tutorial again":"Take this tutorial",()=>ui.Choose(stage,TutorialChoice.Take));
        ui.Set(1,replay?"Normal battle":"Skip this tutorial",()=>ui.Choose(stage,TutorialChoice.SkipThis));
        ui.Set(2,"Skip all future tutorials",()=>ui.Choose(stage,TutorialChoice.SkipAll));ui.options[2].gameObject.SetActive(!replay);
    }
    private void Choose(StageDefinition stage,TutorialChoice choice)
    {
        if(busy)return;busy=true;
        if(!StageProgress.IsUnlocked(stage.stageId) || !TutorialPreferences.Choose(stage.tutorialId,choice,out _))
        {message.text="Unable to save your choice. Please try again.";busy=false;return;}
        Close();Launch(stage,choice==TutorialChoice.Take);
    }
    private static void Launch(StageDefinition stage,bool guided)
    {
        if(!StageSession.Select(stage.stageId,guided,out _))return;
        SceneManager.LoadScene("CharacterSelect");
    }
    public static void ShowSettings()
    {
        if(!TutorialController.NavigationAllowed)return;
        var ui=Create();if(ui==null)return;ui.title.text="Tutorial settings";ui.details.text="Change automatic tutorial prompts. You can always choose Tutorial on an unlocked stage to replay it.";
        ui.Set(0,"Show future tutorial prompts",()=>ui.ChangeSetting(false));ui.Set(1,"Skip all future tutorial prompts",()=>ui.ChangeSetting(true));ui.options[2].gameObject.SetActive(false);ui.RefreshSettings();
    }
    public static void ShowStageError(string error)
    {
        var ui=Create();if(ui==null)return;ui.title.text="Stage unavailable";ui.details.text=error;
        ui.Set(0,"Back to stages",()=>{StageSession.Clear();ui.Close();SceneManager.LoadScene("StageSelect");});ui.options[1].gameObject.SetActive(false);ui.options[2].gameObject.SetActive(false);
    }
    private void ChangeSetting(bool skip)
    {if(busy)return;busy=true;if(!TutorialPreferences.SetSkipAll(skip,out _))message.text="Unable to save settings. Please try again.";else RefreshSettings();busy=false;}
    private void RefreshSettings()=>message.text=TutorialPreferences.SkipAll?"Automatic prompts are skipped.":"Automatic prompts are enabled.";
    private void Set(int index,string label,UnityEngine.Events.UnityAction action){options[index].GetComponentInChildren<TMP_Text>().text=label;options[index].onClick.AddListener(action);}
    private void Close(){gameObject.SetActive(false);Destroy(gameObject);if(active==this)active=null;}
    private void OnDestroy(){if(active==this)active=null;}
}

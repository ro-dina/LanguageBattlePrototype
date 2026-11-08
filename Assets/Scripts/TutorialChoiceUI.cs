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
        active=Instantiate(asset).GetComponent<TutorialChoiceUI>();active.close.onClick.AddListener(active.Close);LocalizedText.Set(active.close.GetComponentInChildren<TMP_Text>(),"ui.close");return active;
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
        LocalizedText.Set(ui.title,replay?"ui.tut.replay.named":"ui.tut.choice.title",stage.displayName);
        LocalizedText.SetProvider(ui.details,()=>Localization.Text("stage.description."+stage.stageId)+"\n\n"+Localization.Text(TutorialCatalog.IsImplemented(stage.tutorialId)?"ui.tut.choice.description":"ui.tut.not.ready"));
        ui.Set(0,replay?"ui.tut.take.again":"ui.tut.take",()=>ui.Choose(stage,TutorialChoice.Take));
        ui.Set(1,replay?"ui.normal.battle":"ui.tut.skip.one",()=>ui.Choose(stage,TutorialChoice.SkipThis));
        ui.Set(2,"ui.tut.skip.all",()=>ui.Choose(stage,TutorialChoice.SkipAll));ui.options[2].gameObject.SetActive(!replay);
    }
    private void Choose(StageDefinition stage,TutorialChoice choice)
    {
        if(busy)return;busy=true;
        if(!StageProgress.IsUnlocked(stage.stageId) || !TutorialPreferences.Choose(stage.tutorialId,choice,out _))
        {LocalizedText.Set(message,"ui.tut.choice.failed");busy=false;return;}
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
        var ui=Create();if(ui==null)return;LocalizedText.Set(ui.title,"ui.tut.settings");LocalizedText.Set(ui.details,"ui.tut.setting.description");
        ui.Set(0,"ui.tut.prompts.show",()=>ui.ChangeSetting(false));ui.Set(1,"ui.tut.prompts.skip",()=>ui.ChangeSetting(true));ui.options[2].gameObject.SetActive(false);ui.RefreshSettings();
    }
    public static void ShowStageError(string error)
    {
        var ui=Create();if(ui==null)return;LocalizedText.Set(ui.title,"ui.stage.unavailable");LocalizedText.SetProvider(ui.details,()=>Localization.Error(error));
        ui.Set(0,"ui.back.stages",()=>{StageSession.Clear();ui.Close();SceneManager.LoadScene("StageSelect");});ui.options[1].gameObject.SetActive(false);ui.options[2].gameObject.SetActive(false);
    }
    private void ChangeSetting(bool skip)
    {if(busy)return;busy=true;if(!TutorialPreferences.SetSkipAll(skip,out _))LocalizedText.Set(message,"ui.tut.setting.failed");else RefreshSettings();busy=false;}
    private void RefreshSettings()=>LocalizedText.Set(message,TutorialPreferences.SkipAll?"ui.tut.prompts.off":"ui.tut.prompts.on");
    private void Set(int index,string key,UnityEngine.Events.UnityAction action){LocalizedText.Set(options[index].GetComponentInChildren<TMP_Text>(),key);options[index].onClick.AddListener(action);}
    private void Close(){gameObject.SetActive(false);Destroy(gameObject);if(active==this)active=null;}
    private void OnDestroy(){if(active==this)active=null;}
}

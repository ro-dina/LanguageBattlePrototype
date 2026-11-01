using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public enum TutorialInput { Skill, Adverb, Modifier, OpenSkills, CloseSkills, Navigate }
public sealed class TutorialController:MonoBehaviour
{
    public static TutorialController Active { get; private set; }
    public static bool NavigationAllowed=>Active==null || Active.Allows(TutorialInput.Navigate);
    public TutorialStepType CurrentStep=>definition!=null && cursor<definition.steps.Length?definition.steps[cursor].Kind:TutorialStepType.Complete;
    public bool ReadyForCommit { get; private set; }
    public bool Failed { get; private set; }
    public bool PassiveEnemy=>!Failed && !ReadyForCommit && stage.enemyBehaviorProfile=="guided_passive";
    public string SkillId { get; private set; }
    private BattleManager battle;
    private StageDefinition stage;
    private TutorialDefinition definition;
    private TutorialFocusUI view;
    private int cursor;
    private bool waitingDialogue;
    private bool exiting;
    private Dictionary<Button,bool> buttons=new Dictionary<Button,bool>();
    public static TutorialController Attach(BattleManager owner,StageDefinition selected)
    {
        if(!StageSession.Guided || selected==null || !TutorialCatalog.IsImplemented(selected.tutorialId))return null;
        var runner=owner.gameObject.AddComponent<TutorialController>();runner.battle=owner;runner.stage=selected;Active=runner;
        runner.definition=TutorialCatalog.Get(selected.tutorialId);owner.Tutorial=runner;
        if(!TutorialCatalog.Supported(runner.definition,out string error)){runner.Abort(error);return runner;}
        runner.SkillId=owner.Context.Player.Character.skillIds.FirstOrDefault(id=>GameDatabase.Instance.GetSkill(id)!=null);
        if(runner.SkillId==null){runner.Abort("No usable equipped skill.");return runner;}
        StageSession.TutorialSkillId=runner.SkillId;runner.cursor=StageSession.TutorialCursor;
        if(runner.cursor<0 || runner.cursor>=runner.definition.steps.Length || runner.CurrentStep==TutorialStepType.WaitForAttackResolution)runner.cursor=0;
        foreach(var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))runner.buttons[button]=button.interactable;
        var asset=Resources.Load<GameObject>("UI/TutorialFocusUI");
        if(asset==null){runner.Abort("Tutorial focus UI unavailable.");return runner;}
        runner.view=Instantiate(asset).GetComponent<TutorialFocusUI>();runner.Enter();return runner;
    }
    public bool Allows(TutorialInput input,string id=null)
    {
        if(Failed || ReadyForCommit)return true;
        if(CurrentStep==TutorialStepType.WaitForBattleVictory)return true;
        return (CurrentStep==TutorialStepType.Highlight || CurrentStep==TutorialStepType.WaitForSkillSelection) && input==TutorialInput.Skill && id==SkillId;
    }
    private void Enter()
    {
        if(Failed || exiting)return;
        try
        {
            if(cursor>=definition.steps.Length){Abort("Tutorial ended without completion.");return;}
            StageSession.TutorialCursor=cursor;var step=definition.steps[cursor];
            if(step.Kind==TutorialStepType.Dialogue)
            {
                waitingDialogue=true;view.Hint(null);RefreshButtons();
                if(!DialogueUI.Play(step.dialogueId,_=>{waitingDialogue=false;Advance();},()=>{if(!exiting)Abort("Dialogue interrupted.");},out string error))Abort(error);
            }
            else if(step.Kind==TutorialStepType.Highlight || step.Kind==TutorialStepType.WaitForSkillSelection)
            {
                battle.ShowTutorialSkills();var target=battle.SkillButton(SkillId);
                if(target==null){Abort("Required skill button unavailable.");return;}
                view.Focus((RectTransform)target.transform,step.hint);RefreshButtons();
                if(UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(target.gameObject);
                if(step.Kind==TutorialStepType.Highlight)StartCoroutine(HighlightFrame());
            }
            else if(step.Kind==TutorialStepType.WaitForAttackResolution){view.Hint(null);RefreshButtons();}
            else if(step.Kind==TutorialStepType.WaitForBattleVictory){RestoreButtons();view.Hint(step.hint);battle.RefreshTutorialInput();}
            else if(step.Kind==TutorialStepType.Complete){ReadyForCommit=true;Release();}
            else Abort("Unsupported tutorial step: "+step.type);
        }
        catch(Exception error){Abort(error.Message);}
    }
    private IEnumerator HighlightFrame(){yield return null;if(CurrentStep==TutorialStepType.Highlight)Advance();}
    private void Advance(){cursor++;Enter();}
    public void SkillAccepted(string id)
    {if(!Failed && !ReadyForCommit && id==SkillId && (CurrentStep==TutorialStepType.WaitForSkillSelection || CurrentStep==TutorialStepType.Highlight)){if(CurrentStep==TutorialStepType.Highlight)cursor++;Advance();}}
    public IEnumerator AttackResolved()
    {
        if(!Failed && CurrentStep==TutorialStepType.WaitForAttackResolution)Advance();
        while(!Failed && waitingDialogue)yield return null;
    }
    public bool PrepareVictory()
    {
        if(Failed)return true;
        if(CurrentStep==TutorialStepType.WaitForBattleVictory)Advance();
        if(!ReadyForCommit){Abort("Victory arrived before the required tutorial steps.");return true;}
        return true;
    }
    private void RefreshButtons()
    {
        foreach(var pair in buttons)
            if(pair.Key!=null)pair.Key.interactable=Allows(TutorialInput.Skill,pair.Key==battle.SkillButton(SkillId)?SkillId:null);
    }
    private void RestoreButtons(){foreach(var pair in buttons)if(pair.Key!=null)pair.Key.interactable=pair.Value;}
    private void Release()
    {
        RestoreButtons();if(view!=null){Destroy(view.gameObject);view=null;}
        if(battle!=null)battle.RefreshTutorialInput();
    }
    public void Abort(string reason)
    {
        if(Failed || exiting)return;Failed=true;waitingDialogue=false;Release();
        if(DialogueUI.Active!=null)DialogueUI.Active.Cancel();
        if(battle!=null)battle.ShowTutorialNotice("Guidance stopped. You can continue this battle normally.");
        Debug.LogWarning("Tutorial stopped: "+reason);
    }
    private void OnDestroy()
    {
        exiting=true;waitingDialogue=false;RestoreButtons();if(view!=null)Destroy(view.gameObject);
        if(DialogueUI.Active!=null)DialogueUI.Active.Cancel();if(Active==this)Active=null;
        if(battle!=null)battle.Tutorial=null;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>Active=null;
}

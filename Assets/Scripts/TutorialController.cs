using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public enum TutorialInput { Skill, Adverb, Modifier, OpenSkills, CloseSkills, Navigate, EquipmentOpen, EquipmentSelect, EquipmentConfirm }
public sealed class TutorialController:MonoBehaviour
{
    public static TutorialController Active { get; private set; }
    public static bool NavigationAllowed=>Active==null || Active.Allows(TutorialInput.Navigate);
    public TutorialStepType CurrentStep=>definition!=null && cursor<definition.steps.Length?definition.steps[cursor].Kind:TutorialStepType.Complete;
    public bool ReadyForCommit { get; private set; }
    public bool Failed { get; private set; }
    public bool IsPreparation=>preparation;
    public int StageNumber=>stage?.stageNumber ?? 0;
    public bool RestrictsFeatures=>Active==this && StageSession.Guided && StageSession.StageId==stage?.stageId && isActiveAndEnabled && !exiting && !Failed && !ReadyForCommit && definition!=null &&
        CurrentStep!=TutorialStepType.WaitForBattleVictory && CurrentStep!=TutorialStepType.Complete &&
        (IsPreparation ? !StageSession.EquipmentConfirmed : battle?.Context.Outcome==BattleOutcome.Ongoing);
    public bool FeatureAvailable(TutorialFeature feature)=>TutorialFeaturePolicy.Available(this,feature);
    public bool AllowsFeature(TutorialInput input,string id=null)
    {
        switch(input)
        {
            case TutorialInput.Skill:case TutorialInput.OpenSkills:case TutorialInput.CloseSkills:return FeatureAvailable(TutorialFeature.Skill);
            case TutorialInput.Adverb:return FeatureAvailable(TutorialFeature.Adverb);
            case TutorialInput.EquipmentOpen:case TutorialInput.EquipmentSelect:case TutorialInput.EquipmentConfirm:return FeatureAvailable(TutorialFeature.Adjective);
            case TutorialInput.Modifier:
                if(id=="futur1" || id=="noTense")return FeatureAvailable(TutorialFeature.Future);
                if(id=="muessen" || id=="noModal")return FeatureAvailable(TutorialFeature.Muessen);
                return FeatureAvailable(TutorialFeature.Adverb) || FeatureAvailable(TutorialFeature.Muessen) || FeatureAvailable(TutorialFeature.Future);
            default:return true;
        }
    }
    public bool PassiveEnemy=>!Failed && !ReadyForCommit && stage.enemyBehaviorProfile=="guided_passive" &&
        !(stage.battleModifiers.passiveUntilPractice && CurrentStep==TutorialStepType.WaitForBattleVictory);
    public string SkillId { get; private set; }
    public BattleActionResult LastResult { get; private set; }
    public Button FocusedButton { get; private set; }
    private TutorialStep Step=>definition.steps[cursor];
    private BattleManager battle;
    private CharacterDetailManager detail;
    private StageDefinition stage;
    private TutorialDefinition definition;
    private TutorialFocusUI view;
    private int cursor;
    private bool waitingDialogue,exiting;
    private bool preparation;
    private readonly Dictionary<Button,bool> buttons=new Dictionary<Button,bool>();
    public static TutorialController Attach(BattleManager owner,StageDefinition selected)
    {
        if(!StageSession.Guided || selected==null || !TutorialCatalog.IsImplemented(selected.tutorialId))return null;
        var runner=owner.gameObject.AddComponent<TutorialController>();runner.battle=owner;runner.stage=selected;owner.Tutorial=runner;
        runner.SkillId=owner.Context.Player.Character.skillIds.FirstOrDefault(id=>GameDatabase.Instance.GetSkill(id)!=null);
        runner.cursor=selected.battleModifiers.tutorialAdjectiveId?.Length>0?StageSession.TutorialCursor:0;
        // Retain the original Technik selection/detail route. Other battle lessons restart their combat observation.
        if(selected.stageId=="stage_1")runner.cursor=StageSession.TutorialCursor;
        runner.Initialize();return runner;
    }
    public static TutorialController AttachPreparation(CharacterDetailManager owner)
    {
        if(!StageSession.EquipmentLesson || StageSession.EquipmentConfirmed || StageSession.EquipmentInstanceId!=owner.ViewedInstanceId)return null;
        var runner=owner.gameObject.AddComponent<TutorialController>();runner.detail=owner;runner.preparation=true;runner.stage=StageSession.Stage;runner.cursor=0;
        runner.SkillId=CharacterLoadoutStore.GetLoadout(owner.ViewedInstanceId)?.skillSlots.FirstOrDefault()?.skillId;
        runner.Initialize();return runner;
    }
    private void Initialize()
    {
        Active=this;definition=TutorialCatalog.Get(stage.tutorialId);StageSession.TutorialSkillId=SkillId;
        if(!TutorialCatalog.Supported(definition,out string error)){Abort(error);return;}
        if(SkillId==null){Abort("No usable equipped skill.");return;}
        if(!IsPreparation && !string.IsNullOrEmpty(stage.battleModifiers.tutorialAdjectiveId))
            cursor=Array.FindLastIndex(definition.steps,s=>s.Kind==TutorialStepType.WaitForEquipmentChange)+1;
        if(!IsPreparation && battle.Context.Player.Character.language!="de" && definition.steps.Any(s=>s.Kind==TutorialStepType.WaitForModalVerbSelection || s.Kind==TutorialStepType.WaitForFutureSelection))
        {Abort("This lesson requires a German character.");return;}
        if(cursor<0 || cursor>=definition.steps.Length || CurrentStep==TutorialStepType.WaitForAttackResolution)cursor=0;
        if(IsPreparation)StageSession.EquipmentChosen=false;
        var asset=Resources.Load<GameObject>("UI/TutorialFocusUI");if(asset==null){Abort("Tutorial focus UI unavailable.");return;}
        view=Instantiate(asset).GetComponent<TutorialFocusUI>();view.OfferExit(()=>Abort("Player stopped guidance."));CaptureButtons();Enter();
    }
    public static bool DetailAllows(TutorialInput input,string id=null)=>Active==null || !Active.IsPreparation || Active.Allows(input,id);
    public bool Allows(TutorialInput input,string id=null)
    {
        if(!RestrictsFeatures)return true;
        if(!AllowsFeature(input,id))return false;
        if(waitingDialogue || DialogueUI.IsOpen)return false;
        if(CurrentStep==TutorialStepType.WaitForBattleVictory)return true;
        if(CurrentStep==TutorialStepType.WaitForEquipmentChange)
            return Step.target=="openAdjective" && input==TutorialInput.EquipmentOpen && id=="adjective" ||
                Step.target=="chooseAdjective" && input==TutorialInput.EquipmentSelect && id==stage.battleModifiers.tutorialAdjectiveId ||
                Step.target=="confirmEquipment" && input==TutorialInput.EquipmentConfirm && StageSession.EquipmentChosen;
        if(CurrentStep==TutorialStepType.WaitForAdverbSelection)return input==TutorialInput.Adverb && (id==null || id==battle.FirstAdverbId);
        if(CurrentStep==TutorialStepType.WaitForModalVerbSelection)return input==TutorialInput.Modifier && id=="muessen";
        if(CurrentStep==TutorialStepType.WaitForFutureSelection)return input==TutorialInput.Modifier && id=="futur1";
        return (CurrentStep==TutorialStepType.Highlight || CurrentStep==TutorialStepType.WaitForSkillSelection) && input==TutorialInput.Skill && id==SkillId && Prerequisite();
    }
    private bool Prerequisite()
    {
        if(battle==null)return false;
        switch(Step.requirement)
        {
            case "adverb":return !string.IsNullOrEmpty(StageSession.TutorialAdverbId) && battle.Selection.Get(BattleElementKind.Adverb)==StageSession.TutorialAdverbId;
            case "adjective":return StageSession.EquipmentConfirmed && battle.Context.Player.Character.equippedAdjectiveId==stage.battleModifiers.tutorialAdjectiveId;
            case "muessen":return battle.Selection.Modal==BattleModal.Muessen;
            case "future":return battle.Selection.Tense==BattleTense.Future;
            case "normal":return battle.Selection.Tense==BattleTense.Present && battle.Selection.Modal==BattleModal.None && battle.Context.PendingAttack==null;
            case null:case "":return true;
            default:return false;
        }
    }
    private void Enter()
    {
        if(Failed || exiting)return;
        try
        {
            if(cursor>=definition.steps.Length){Abort("Tutorial ended without completion.");return;}
            StageSession.TutorialCursor=cursor;CaptureButtons();view.RefreshFeatures(this);FocusedButton=null;
            if(CurrentStep==TutorialStepType.Dialogue)
            {
                waitingDialogue=true;view.Hint(null);RefreshButtons();
                if(!DialogueUI.Play(Step.dialogueId,_=>{waitingDialogue=false;Advance();},()=>{if(!exiting)Abort("Dialogue interrupted.");},out string error))Abort(error);
                return;
            }
            if(CurrentStep==TutorialStepType.WaitForBattleVictory){RestoreButtons();view.Hint(Step.hint);battle.RefreshTutorialInput();return;}
            if(CurrentStep==TutorialStepType.Complete){ReadyForCommit=true;Release();return;}
            if(CurrentStep==TutorialStepType.WaitForAttackResolution || CurrentStep==TutorialStepType.WaitForFutureResolution)
            {view.Hint(Step.hint);RefreshButtons();return;}
            if(CurrentStep==TutorialStepType.WaitForEquipmentChange)
                FocusedButton=Step.target=="openAdjective"?detail.AdjectiveButton:Step.target=="chooseAdjective"?detail.EquipmentButton(stage.battleModifiers.tutorialAdjectiveId):detail.BattleButton;
            else if(CurrentStep==TutorialStepType.WaitForAdverbSelection)FocusedButton=battle.FirstAdverbButton;
            else if(CurrentStep==TutorialStepType.WaitForModalVerbSelection)FocusedButton=battle.ModifierButton("muessen");
            else if(CurrentStep==TutorialStepType.WaitForFutureSelection)FocusedButton=battle.ModifierButton("futur1");
            else if(CurrentStep==TutorialStepType.Highlight || CurrentStep==TutorialStepType.WaitForSkillSelection)
            {battle.ShowTutorialSkills();FocusedButton=battle.SkillButton(SkillId);}
            if(FocusedButton==null || !RequiredFeatureAvailable()){Abort("Required tutorial control unavailable or not introduced in this stage.");return;}
            Canvas.ForceUpdateCanvases();view.Focus((RectTransform)FocusedButton.transform,Step.hint);RefreshButtons();
            if(UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(FocusedButton.gameObject);
            if(CurrentStep==TutorialStepType.Highlight)StartCoroutine(HighlightFrame());
        }
        catch(Exception error){Abort(error.Message);}
    }
    private bool RequiredFeatureAvailable()
    {
        switch(CurrentStep)
        {
            case TutorialStepType.WaitForAdverbSelection:return FeatureAvailable(TutorialFeature.Adverb);
            case TutorialStepType.WaitForEquipmentChange:return FeatureAvailable(TutorialFeature.Adjective);
            case TutorialStepType.WaitForModalVerbSelection:return FeatureAvailable(TutorialFeature.Muessen);
            case TutorialStepType.WaitForFutureSelection:return FeatureAvailable(TutorialFeature.Future);
            default:return FeatureAvailable(TutorialFeature.Skill);
        }
    }
    private IEnumerator HighlightFrame(){yield return null;if(CurrentStep==TutorialStepType.Highlight)Advance();}
    private void Advance(){cursor++;Enter();}
    public void EquipmentOpened(){if(!Failed && IsPreparation && CurrentStep==TutorialStepType.WaitForEquipmentChange && Step.target=="openAdjective" && detail.IsAdjectiveListOpen)Advance();}
    public void EquipmentSelected(string id)
    {if(!Failed && IsPreparation && CurrentStep==TutorialStepType.WaitForEquipmentChange && Step.target=="chooseAdjective" && id==stage.battleModifiers.tutorialAdjectiveId && detail.DraftAdjectiveId==id){StageSession.EquipmentChosen=true;Advance();}}
    public void EquipmentSaveFailed()
    {view.Focus((RectTransform)detail.BattleButton.transform,Localization.Text("tutorial.equipment.save.failed"));RefreshButtons();}
    public bool EquipmentConfirmed(string instanceId)
    {
        if(Failed)return true;
        if(!IsPreparation || !Allows(TutorialInput.EquipmentConfirm) || instanceId!=StageSession.EquipmentInstanceId ||
            CharacterLoadoutStore.GetLoadout(instanceId)?.adjectiveId!=stage.battleModifiers.tutorialAdjectiveId)return false;
        if(PlayerSaveService.Instance==null || PlayerSaveService.Instance.Files.LastError!=null)
        {EquipmentSaveFailed();return false;}
        StageSession.EquipmentConfirmed=true;StageSession.TutorialCursor=++cursor;Release();return true;
    }
    public void SelectionChanged()
    {
        if(Failed || IsPreparation || waitingDialogue)return;
        if(CurrentStep==TutorialStepType.WaitForAdverbSelection && !string.IsNullOrEmpty(battle.Selection.Get(BattleElementKind.Adverb)))
        {StageSession.TutorialAdverbId=battle.Selection.Get(BattleElementKind.Adverb);Advance();}
        else if(CurrentStep==TutorialStepType.WaitForModalVerbSelection && battle.Selection.Modal==BattleModal.Muessen)Advance();
        else if(CurrentStep==TutorialStepType.WaitForFutureSelection && battle.Selection.Tense==BattleTense.Future)Advance();
    }
    public void SkillAccepted(string id)
    {if(!Failed && !ReadyForCommit && id==SkillId && (CurrentStep==TutorialStepType.WaitForSkillSelection || CurrentStep==TutorialStepType.Highlight) && Prerequisite()){if(CurrentStep==TutorialStepType.Highlight)cursor++;Advance();}}
    public IEnumerator AttackResolved(BattleActionResult result=null)
    {
        if(!Failed && result?.Applied==true && result.Action.Side==BattleSide.Player)
        {
            LastResult=result;
            if(CurrentStep==TutorialStepType.WaitForFutureResolution && result.IsPendingAttack && battle.Context.PendingAttack==null)Advance();
            else if(CurrentStep==TutorialStepType.WaitForAttackResolution)
            {
                bool observed=string.IsNullOrEmpty(Step.target) || Step.target=="adverb" && result.Action.SkillEquipment?.id==StageSession.TutorialAdverbId && result.Sentence.SpeechWords.Contains(GameDatabase.Instance.GetEquipment(StageSession.TutorialAdverbId).equipmentName) ||
                    Step.target=="adjective" && battle.Context.Player.Character.equippedAdjectiveId==stage.battleModifiers.tutorialAdjectiveId ||
                    Step.target=="muessen" && result.Action.Modal==BattleModal.Muessen && !result.IsReservation && battle.Context.MuessenUsed ||
                    Step.target=="reservation" && result.IsReservation && result.Damage==0 && battle.Context.PendingAttack!=null ||
                    Step.target=="normalAfterFuture" && !result.IsReservation && !result.IsPendingAttack && result.Action.Tense==BattleTense.Present && result.Action.Modal==BattleModal.None;
                if(observed)Advance();else Abort("Required battle effect was not observed.");
            }
        }
        while(!Failed && waitingDialogue)yield return null;
    }
    public bool PrepareVictory()
    {
        if(Failed)return true;
        if(CurrentStep==TutorialStepType.WaitForBattleVictory)Advance();
        if(!ReadyForCommit){Abort("Victory arrived before the required tutorial steps.");return true;}
        return true;
    }
    private void CaptureButtons()
    {foreach(var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include))if(!buttons.ContainsKey(button) && !button.transform.IsChildOf(view.transform) && button.GetComponentInParent<DialogueUI>()==null)buttons[button]=button.interactable;}
    public void RefreshButtons()
    {
        if(view!=null)view.RefreshFeatures(this);
        if(!RestrictsFeatures)return;
        foreach(var pair in buttons)if(pair.Key!=null)pair.Key.interactable=pair.Key==FocusedButton && !waitingDialogue && (battle==null || battle.CanChoose);
    }
    private void RestoreButtons(){foreach(var pair in buttons)if(pair.Key!=null)pair.Key.interactable=pair.Value;}
    private void Release(){RestoreButtons();if(view!=null){Destroy(view.gameObject);view=null;}if(battle!=null)battle.RefreshTutorialInput();}
    public void Abort(string reason)
    {
        if(Failed || exiting)return;Failed=true;waitingDialogue=false;Release();
        if(DialogueUI.Active!=null)DialogueUI.Active.Cancel();
        if(IsPreparation){StageSession.StopPreparation();if(Active==this)Active=null;}
        if(battle!=null)battle.ShowTutorialNotice("Guidance stopped. You can continue this battle normally.");
        Debug.LogWarning("Tutorial stopped: "+reason);
    }
    private void OnDestroy()
    {
        exiting=true;waitingDialogue=false;RestoreButtons();if(view!=null)Destroy(view.gameObject);
        if(DialogueUI.Active!=null)DialogueUI.Active.Cancel();if(Active==this)Active=null;
        if(IsPreparation && !StageSession.EquipmentConfirmed)StageSession.StopPreparation();
        if(battle!=null)battle.Tutorial=null;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>Active=null;
}

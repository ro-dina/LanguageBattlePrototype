using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;
[PrebuildSetup(typeof(TestSaveIsolation))]
[PostBuildCleanup(typeof(TestSaveIsolation))]
public class StageTutorialSceneTests
{
    private string directory;
    private float oldScale;
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    private static Button Button(string name)=>Object.FindObjectsByType<Button>().Single(b=>b.name==name);
    private static void HP(BattleCombatant c,int hp)=>typeof(BattleCombatant).GetProperty("HP").SetValue(c,hp);
    private IEnumerator Load(string name){SceneManager.LoadScene(name);yield return null;yield return null;}
    [UnitySetUp] public IEnumerator Setup()
    {
        oldScale=Time.timeScale;Time.timeScale=8;yield return Load("GermanyHome");if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        directory=Path.Combine(Path.GetTempPath(),"LanguageBattleTutorialScenes",Guid.NewGuid().ToString("N"));PlayerSaveService.Initialize(directory);GameManager.SelectedCharacterId="nacht";GameManager.EnemyCharacterId="feuer";StageSession.Clear();yield return null;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        Time.timeScale=oldScale;yield return Load("GermanyHome");if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        if(Directory.Exists(directory))Directory.Delete(directory,true);else if(File.Exists(directory))File.Delete(directory);
    }
    private IEnumerator Choice(int index=0,bool review=false,string stage="stage_1")
    {yield return Load("StageSelect");TutorialChoiceUI.Request(stage,review);yield return null;Button("TutorialOption"+index).onClick.Invoke();yield return null;yield return null;Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("CharacterSelect"));}
    private IEnumerator StartBattle(){Object.FindAnyObjectByType<CharacterSelectManager>().StartBattle();yield return null;yield return null;Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("Battle"));}
    private IEnumerator AdvanceDialogue()
    {yield return new WaitForSecondsRealtime(.22f);DialogueUI.Active.Advance();yield return null;}
    private IEnumerator ReachSkill()
    {
        float end=Time.realtimeSinceStartup+10;
        while(DialogueUI.IsOpen && Time.realtimeSinceStartup<end)yield return AdvanceDialogue();
        yield return null;Assert.That(TutorialController.Active.CurrentStep,Is.EqualTo(TutorialStepType.WaitForSkillSelection));
    }
    private IEnumerator AutoFinish()
    {
        float end=Time.realtimeSinceStartup+25;
        while(SceneManager.GetActiveScene().name=="Battle" && Time.realtimeSinceStartup<end)
        {
            if(DialogueUI.IsOpen){yield return AdvanceDialogue();continue;}
            var battle=Object.FindAnyObjectByType<BattleManager>();
            if(battle!=null && battle.CanChoose && battle.AllowsInput(TutorialInput.Skill,battle.Context.Player.Character.skillIds[0]))battle.UseSkill1();
            yield return null;
        }
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("Result"));yield return null;yield return null;
    }
    [UnityTest] public IEnumerator TechnikRunsDialogueRestrictedRealAttackExplanationVictoryAndAtomicFirstReward()
    {
        yield return Load("StageSelect");Assert.That(Object.FindObjectsByType<StageRowUI>().Length,Is.EqualTo(6));
        Assert.That(GameObject.Find("StageRow_stage_2").GetComponentsInChildren<Button>().Single(b=>b.name=="StageChallenge").interactable,Is.False);
        TutorialChoiceUI.Request("stage_1",true);yield return null;Assert.That(Button("TutorialOption2").gameObject.activeSelf,Is.True,"First visit offers all three choices even via Tutorial.");Button("TutorialChoiceClose").onClick.Invoke();yield return null;
        yield return Choice();Assert.That(StageSession.Guided,Is.True);yield return StartBattle();var battle=Object.FindAnyObjectByType<BattleManager>();
        Assert.That(GameObject.Find("PortraitPlaceholder_technik"),Is.Not.Null);
        Assert.That(DialogueUI.IsOpen,Is.True);Assert.That(DialogueUI.Active.Speaker,Is.EqualTo("Guide"));Assert.That(battle.SelectFuture(true),Is.False);battle.UseSkill1();Assert.That(battle.LastCommittedAction,Is.Null);
        yield return ReachSkill();var controller=TutorialController.Active;Assert.That(controller.SkillId,Is.EqualTo(battle.Context.Player.Character.skillIds[0]));
        battle.UseSkill2();battle.CloseSkillPanel();Assert.That(battle.LastCommittedAction,Is.Null);Assert.That(battle.SelectMuessen(true),Is.False);
        var helper=new GameObject("navigation attempt").AddComponent<SceneLoader>();helper.LoadStageSelect();helper.LoadClear();Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("Battle"));
        var target=battle.SkillButton(controller.SkillId);var pointer=new PointerEventData(EventSystem.current){position=((RectTransform)target.transform).TransformPoint(((RectTransform)target.transform).rect.center)};var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Assert.That(hits[0].gameObject.GetComponentInParent<Button>(),Is.EqualTo(target));
        int hp=battle.Context.Player.HP;target.onClick.Invoke();Assert.That(battle.LastCommittedAction,Is.Not.Null);Assert.That(controller.CurrentStep,Is.EqualTo(TutorialStepType.WaitForAttackResolution));
        float end=Time.realtimeSinceStartup+10;while(!DialogueUI.IsOpen && Time.realtimeSinceStartup<end)yield return null;
        Assert.That(DialogueUI.Active.Current.dialogueId,Is.EqualTo("technik_after_attack"));Assert.That(battle.Context.Player.HP,Is.EqualTo(hp));yield return AutoFinish();
        Assert.That(StageProgress.IsUnlocked("stage_2"),Is.True);Assert.That(StageProgress.ClearCount("stage_1"),Is.EqualTo(1));Assert.That(TutorialPreferences.Get("technik_basics").completed,Is.True);Assert.That(CurrencyService.Balance,Is.EqualTo(3400));Assert.That(BattleRewardService.Latest.tutorialCompleted,Is.True);Assert.That(Object.FindAnyObjectByType<ResultManager>().RewardSummary,Does.Contain("First clear: +300"));
    }
    [UnityTest] public IEnumerator IndividualSkipUsesNormalBattlePreservesRewardsAndPromptsNextStage()
    {
        yield return Choice(1);Assert.That(StageSession.Guided,Is.False);yield return StartBattle();var battle=Object.FindAnyObjectByType<BattleManager>();Assert.That(TutorialController.Active,Is.Null);Assert.That(battle.SelectMuessen(true),Is.True);battle.ClearModifiers();HP(battle.Context.Enemy,1);yield return AutoFinish();
        Assert.That(CurrencyService.Balance,Is.EqualTo(3400));Assert.That(TutorialPreferences.Get("technik_basics").completed,Is.False);Assert.That(TutorialPreferences.Get("technik_basics").individuallySkipped,Is.True);Assert.That(TutorialPreferences.NeedsChoice("adverb_basics"),Is.True);
    }
    [UnityTest] public IEnumerator GlobalSkipSurvivesReloadReviewOverridesItAndSettingsCanEnablePrompts()
    {
        yield return Choice(2);yield return StartBattle();var battle=Object.FindAnyObjectByType<BattleManager>();HP(battle.Context.Enemy,1);yield return AutoFinish();PlayerSaveService.Instance.Reload();Assert.That(TutorialPreferences.SkipAll,Is.True);
        yield return Load("StageSelect");TutorialChoiceUI.Request("stage_2",false);yield return null;yield return null;Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("CharacterSelect"));Assert.That(Object.FindAnyObjectByType<TutorialChoiceUI>(),Is.Null);
        yield return Choice(0,true);Assert.That(StageSession.Guided,Is.True);yield return StartBattle();Assert.That(DialogueUI.IsOpen,Is.True);HP(Object.FindAnyObjectByType<BattleManager>().Context.Enemy,1);yield return AutoFinish();Assert.That(CurrencyService.Balance,Is.EqualTo(3500));Assert.That(TutorialPreferences.SkipAll,Is.True);
        yield return Load("GermanyHome");Object.FindAnyObjectByType<GermanyHomeManager>().GoSettings();yield return null;Button("SettingsTutorial").onClick.Invoke();yield return null;Button("TutorialOption0").onClick.Invoke();Assert.That(TutorialPreferences.SkipAll,Is.False);PlayerSaveService.Instance.Reload();Assert.That(TutorialPreferences.SkipAll,Is.False);
    }
    [UnityTest] public IEnumerator RemainingFourStagesHaveChoicesNormalBattlesFirstRewardsAndRetryWithoutFalseCompletion()
    {
        BattleRewardService.Begin("technik",out string first,out _,"stage_1");BattleRewardService.Complete(first,true,out _,()=>.9);
        for(int n=2;n<=5;n++)
        {
            yield return Load("StageSelect");TutorialChoiceUI.Request("stage_"+n,false);yield return null;var popup=Object.FindAnyObjectByType<TutorialChoiceUI>();Assert.That(Field<TMP_Text>(popup,"details").text,Does.Contain("not available yet"));Button("TutorialOption0").onClick.Invoke();yield return null;yield return null;
            Assert.That(StageSession.Guided,Is.False);yield return StartBattle();var battle=Object.FindAnyObjectByType<BattleManager>();Assert.That(battle.SelectFuture(true),Is.True);battle.ClearModifiers();HP(battle.Context.Enemy,1);yield return AutoFinish();
            Assert.That(StageProgress.IsCleared("stage_"+n),Is.True);Assert.That(TutorialPreferences.Get(StageCatalog.Get("stage_"+n).tutorialId).completed,Is.False);
        }
        Assert.That(CurrencyService.Balance,Is.EqualTo(5000));yield return Load("StageSelect");TutorialChoiceUI.Request("stage_5",false);yield return null;yield return null;yield return StartBattle();HP(Object.FindAnyObjectByType<BattleManager>().Context.Enemy,1);yield return AutoFinish();Assert.That(CurrencyService.Balance,Is.EqualTo(5100));
    }
    [UnityTest] public IEnumerator LockedStageCannotBeEnteredByBattleSceneOrChangedEnemyAndDefeatDoesNotUnlock()
    {
        GameManager.EnemyCharacterId="adverb";yield return Load("Battle");Assert.That(Object.FindAnyObjectByType<BattleManager>().enabled,Is.False);Assert.That(Object.FindAnyObjectByType<TutorialChoiceUI>(),Is.Not.Null);Assert.That(BattleRewardService.Latest,Is.Null);
        yield return Choice(1);yield return StartBattle();var battle=Object.FindAnyObjectByType<BattleManager>();HP(battle.Context.Player,1);HP(battle.Context.Enemy,100);yield return AutoFinish();Assert.That(CurrencyService.Balance,Is.EqualTo(3000));Assert.That(StageProgress.IsUnlocked("stage_2"),Is.False);Assert.That(StageProgress.ClearCount("stage_1"),Is.Zero);
    }
    [UnityTest] public IEnumerator DialogueSupportsRightSpeakerPlaceholdersPagingCompletionAndClickDebounce()
    {
        Assert.That(StageSession.Select("stage_1",true,out _),Is.True);int done=0;int cancelled=0;var line=new DialogueLine{dialogueId="npc",speakerName="Technik",speakerSide="Right",leftPortraitId="guide",rightPortraitId="feuer",text=string.Join(" ",Enumerable.Repeat("A longer dialogue can wrap and continue across pages.",30)),eventOnComplete="npc_finished"};
        Assert.That(DialogueUI.PlayLine(line,id=>{Assert.That(id,Is.EqualTo("npc_finished"));done++;},()=>cancelled++,out _),Is.True);yield return null;
        var ui=DialogueUI.Active;Assert.That(ui.Speaker,Is.EqualTo("Technik"));Assert.That(ui.Text,Is.EqualTo(line.text));Assert.That(Field<Image>(ui,"rightPortrait").sprite,Is.Not.Null);Assert.That(Field<TMP_Text>(ui,"leftPlaceholder").gameObject.activeSelf,Is.True);
        int page=Field<TMP_Text>(ui,"body").pageToDisplay;ui.Advance();Assert.That(Field<TMP_Text>(ui,"body").pageToDisplay,Is.EqualTo(page));yield return new WaitForSecondsRealtime(.22f);ui.Advance();Assert.That(Field<TMP_Text>(ui,"body").pageToDisplay,Is.EqualTo(page+1));ui.Advance();Assert.That(Field<TMP_Text>(ui,"body").pageToDisplay,Is.EqualTo(page+1));
        while(DialogueUI.IsOpen)yield return AdvanceDialogue();Assert.That(done,Is.EqualTo(1));Assert.That(cancelled,Is.Zero);
    }
    [UnityTest] public IEnumerator SceneChangesRetainRouteAndCursorButReleaseOldInputGuardAndErrorsFailOpen()
    {
        yield return Choice();yield return StartBattle();yield return ReachSkill();int cursor=StageSession.TutorialCursor;
        yield return Load("CharacterDetail");Assert.That(TutorialController.Active,Is.Null);Assert.That(DialogueUI.IsOpen,Is.False);Assert.That(StageSession.StageId,Is.EqualTo("stage_1"));Assert.That(StageSession.TutorialCursor,Is.EqualTo(cursor));Assert.That(TutorialController.NavigationAllowed,Is.True);
        Object.FindAnyObjectByType<CharacterDetailManager>().StartBattle();yield return null;yield return null;Assert.That(TutorialController.Active.CurrentStep,Is.EqualTo(TutorialStepType.WaitForSkillSelection));
        var controller=TutorialController.Active;controller.Abort("injected failure");Assert.That(Object.FindAnyObjectByType<BattleManager>().SelectMuessen(true),Is.True);Assert.That(TutorialController.NavigationAllowed,Is.True);Assert.That(TutorialPreferences.Get("technik_basics").completed,Is.False);
        yield return Load("GermanyHome");Assert.That(StageSession.StageId,Is.Null);Assert.That(TutorialController.Active,Is.Null);
    }
    [UnityTest] public IEnumerator TutorialChoiceSaveFailureCannotNavigateOrChangePreferencesAndCanRetry()
    {
        yield return Load("StageSelect");TutorialChoiceUI.Request("stage_1",false);yield return null;string before=JsonUtility.ToJson(PlayerSaveService.Instance.Capture());Directory.Delete(directory,true);File.WriteAllText(directory,"blocked");
        Button("TutorialOption0").onClick.Invoke();Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("StageSelect"));Assert.That(JsonUtility.ToJson(PlayerSaveService.Instance.Capture()),Is.EqualTo(before));Assert.That(Field<TMP_Text>(Object.FindAnyObjectByType<TutorialChoiceUI>(),"message").text,Does.Contain("Unable to save"));
        File.Delete(directory);Directory.CreateDirectory(directory);Button("TutorialOption0").onClick.Invoke();yield return null;yield return null;Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("CharacterSelect"));
    }
    [UnityTest] public IEnumerator OneHitTutorialVictoryStillShowsExplanationAndFailedSaveKeepsCompletionWithFirstReward()
    {
        yield return Choice();yield return StartBattle();yield return ReachSkill();var battle=Object.FindAnyObjectByType<BattleManager>();HP(battle.Context.Enemy,1);battle.UseSkill1();float end=Time.realtimeSinceStartup+10;while(!DialogueUI.IsOpen && Time.realtimeSinceStartup<end)yield return null;
        Assert.That(DialogueUI.Active.Current.dialogueId,Is.EqualTo("technik_after_attack"));Directory.Delete(directory,true);File.WriteAllText(directory,"blocked");yield return AdvanceDialogue();end=Time.realtimeSinceStartup+10;while(Object.FindAnyObjectByType<BattleRewardRetryUI>()==null && Time.realtimeSinceStartup<end)yield return null;
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("Battle"));Assert.That(StageProgress.IsCleared("stage_1"),Is.False);Assert.That(TutorialPreferences.Get("technik_basics").completed,Is.False);Assert.That(CurrencyService.Balance,Is.EqualTo(3000));
        File.Delete(directory);Directory.CreateDirectory(directory);Button("RetryRewardSaveButton").onClick.Invoke();yield return null;yield return null;Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("Result"));Assert.That(CurrencyService.Balance,Is.EqualTo(3400));Assert.That(TutorialPreferences.Get("technik_basics").completed,Is.True);Assert.That(StageProgress.ClearCount("stage_1"),Is.EqualTo(1));
    }
}

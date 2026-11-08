using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;
[PrebuildSetup(typeof(TestSaveIsolation))][PostBuildCleanup(typeof(TestSaveIsolation))]
public class Phase121SceneTests
{
    private string directory;
    private float oldScale;
    private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    private static void Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
    private static Button Button(string name)=>Object.FindObjectsByType<Button>().Single(b=>b.name==name);
    private IEnumerator Load(string name){SceneManager.LoadScene(name);yield return null;yield return null;}
    private void Language(string language){var data=UserSettings.Current;data.uiLanguage=language;Assert.That(UserSettings.TryUpdate(data,out _),Is.True);}
    [UnitySetUp] public IEnumerator Setup()
    {
        oldScale=Time.timeScale;Time.timeScale=8;yield return Load("GermanyHome");if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        directory=Path.Combine(Path.GetTempPath(),"LanguageBattlePhase121",Guid.NewGuid().ToString("N"));PlayerSaveService.Initialize(directory);UserSettings.Initialize(Path.Combine(directory,"Settings"),SystemLanguage.English);GameManager.SelectedCharacterInstanceId=CharacterInstances.GetByCharacterId("nacht").Single().instanceId;GameManager.EnemyCharacterId="feuer";StageSession.Clear();
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        Language("en");Time.timeScale=oldScale;yield return Load("Title");if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);if(Directory.Exists(directory))Directory.Delete(directory,true);
    }
    [UnityTest] public IEnumerator OldSaveAdverbMetadataSurvivesViewingAndAdjectiveSaveButCannotBeEditedOrBiasBattle()
    {
        string id=GameManager.SelectedCharacterInstanceId;var draft=CharacterLoadoutStore.GetLoadout(id);draft.skillSlots[0].equippedAdverbId="sehr";CharacterLoadoutStore.Commit(draft);PlayerSaveService.Instance.Reload();
        yield return Load("CharacterDetail");var detail=Object.FindAnyObjectByType<CharacterDetailManager>();Assert.That(Field<TMP_Text[]>(detail,"skillSlotButtonTexts")[0].text,Is.EqualTo("sein"));
        string before=JsonUtility.ToJson(PlayerSaveService.Instance.Capture());detail.OpenSkillEquipmentList(0);Call(detail,"EquipSkillEquipment","schnell");GameManager.SetSkillEquipment(0,"schnell");Assert.That(JsonUtility.ToJson(PlayerSaveService.Instance.Capture()),Is.EqualTo(before));Assert.That(Field<bool>(detail,"hasUnsavedChanges"),Is.False);
        Assert.That(Field<Transform>(detail,"skillEquipmentButtonParent").GetComponentsInChildren<Button>().All(b=>b.name.StartsWith("SkillChoiceButton_")),Is.True);
        Assert.That(Field<Transform>(detail,"skillEquipmentButtonParent").GetComponentsInChildren<Button>().Any(b=>b.name=="SkillChoiceButton_sehr"),Is.False);detail.CloseSkillEquipmentList();
        Call(detail,"EquipCharacterEquipment","schwarz");detail.ConfirmEquipment();yield return null;yield return null;PlayerSaveService.Instance.Reload();Assert.That(CharacterLoadoutStore.GetLoadout(id).skillSlots[0].equippedAdverbId,Is.EqualTo("sehr"));
        yield return Load("Battle");var battle=Object.FindAnyObjectByType<BattleManager>();Assert.That(battle.Context.Player.Character.skillSlots[0].equippedAdverbId,Is.Empty);Assert.That(Field<EquipmentData[]>(battle,"playerSkillEquipments").All(e=>e==null),Is.True);
        var candidates=(System.Collections.Generic.List<EquipmentData>)typeof(BattleManager).GetMethod("GetBattleAdverbCandidates",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(battle,null);
        Assert.That(candidates.GroupBy(e=>e.id).Select(g=>g.Count()),Is.All.EqualTo(2));Assert.That(candidates.Select(e=>e.id).Distinct().Count(),Is.EqualTo(GameDatabase.Instance.GetAllEquipments().Count(e=>(e.partOfSpeech=="adverb" || e.partOfSpeech=="adjective_adverb") && DamageCalculator.AppliesToSkill(e))));
        battle.UseSkill1();Assert.That(battle.LastCommittedAction.SkillEquipment,Is.Null);yield return null;
    }
    [UnityTest] public IEnumerator SkillPickerSupportsChangeDuplicateRejectionFourSlotsSaveAndIndependentIndividuals()
    {
        string a=GameManager.SelectedCharacterInstanceId;var b=CharacterInstances.Create("nacht");var originalB=JsonUtility.ToJson(CharacterLoadoutStore.GetLoadout(b.instanceId));
        yield return Load("CharacterDetail");var detail=Object.FindAnyObjectByType<CharacterDetailManager>();detail.OpenSkillEquipmentList(0);Button("SkillChoiceButton_brennen").onClick.Invoke();
        Assert.That(CharacterLoadoutStore.GetLoadout(a).skillSlots[0].skillId,Is.EqualTo("sein"));Assert.That(Field<CharacterData>(detail,"character").skillIds[0],Is.EqualTo("brennen"));
        detail.OpenSkillEquipmentList(0);Assert.That(Button("SkillChoiceButton_haben").interactable,Is.False);Call(detail,"EquipSkill","haben");Assert.That(Field<CharacterData>(detail,"character").skillIds[0],Is.EqualTo("brennen"));detail.CloseSkillEquipmentList();
        detail.OpenSkillEquipmentList(3);Button("SkillChoiceButton_laufen").onClick.Invoke();Assert.That(Field<CharacterData>(detail,"character").skillIds.Length,Is.EqualTo(4));
        detail.ConfirmEquipment();yield return null;yield return null;PlayerSaveService.Instance.Reload();Assert.That(CharacterLoadoutStore.GetLoadout(a).skillSlots.Select(s=>s.skillId),Is.EqualTo(new[]{"brennen","haben","werden","laufen"}));Assert.That(JsonUtility.ToJson(CharacterLoadoutStore.GetLoadout(b.instanceId)),Is.EqualTo(originalB));
    }
    [UnityTest] public IEnumerator DynamicStageButtonsAndSettingsLauncherLocalizeOnFirstDrawAndRefreshWithoutChangingProgress()
    {
        Language("ja");yield return Load("StageSelect");string before=JsonUtility.ToJson(PlayerSaveService.Instance.Capture());
        foreach(string language in new[]{"ja","en","ja"})
        {
            Language(language);
            var row=GameObject.Find("StageRow_stage_1").GetComponent<StageRowUI>();
            Assert.That(Field<Button>(row,"challenge").GetComponentInChildren<TMP_Text>().text,Is.EqualTo(Localization.Text("ui.challenge")));
            Assert.That(Field<Button>(row,"review").GetComponentInChildren<TMP_Text>().text,Is.EqualTo(Localization.Text("ui.tutorial")));
            var locked=GameObject.Find("StageRow_stage_2").GetComponent<StageRowUI>();Assert.That(Field<Button>(locked,"challenge").GetComponentInChildren<TMP_Text>().text,Is.EqualTo(Localization.Text("ui.locked")));
            Assert.That(Button("SettingsOpen").GetComponentInChildren<TMP_Text>().text,Does.Contain(Localization.Text("ui.settings")));
            Assert.That(JsonUtility.ToJson(PlayerSaveService.Instance.Capture()),Is.EqualTo(before));
        }
    }
    [UnityTest] public IEnumerator GuidedChoicesDialogueAndHintsSwitchLanguageWithoutChangingProgressOrFeatureLocks()
    {
        Language("ja");yield return Load("StageSelect");TutorialChoiceUI.Request("stage_1",true);
        var close=Button("TutorialChoiceClose").GetComponentInChildren<TMP_Text>();Assert.That(close.text,Is.EqualTo(Localization.Text("ui.close")));Assert.That(Field<string>(close.GetComponent<LocalizedText>(),"key"),Is.EqualTo("ui.close"));
        Assert.That(Button("TutorialOption0").GetComponentInChildren<TMP_Text>().text,Is.EqualTo(Localization.Text("ui.tut.take")));
        Button("TutorialOption0").onClick.Invoke();yield return null;yield return null;Object.FindAnyObjectByType<CharacterSelectManager>().StartBattle();yield return null;yield return null;
        var battle=Object.FindAnyObjectByType<BattleManager>();var next=Button("DialogueNext").GetComponentInChildren<TMP_Text>();Assert.That(next.text,Is.EqualTo(Localization.Text("ui.next")));Assert.That(Field<string>(next.GetComponent<LocalizedText>(),"key"),Is.EqualTo("ui.next"));Assert.That(DialogueUI.Active.Text,Does.Contain("戦闘の基本"));string current=DialogueUI.Active.Current.dialogueId;int cursor=StageSession.TutorialCursor,round=battle.Context.Round,hp=battle.Context.Enemy.HP;
        Language("en");Assert.That(DialogueUI.Active.Text,Does.Contain("basics of battle"));Assert.That(DialogueUI.Active.Speaker,Is.EqualTo("Guide"));Language("ja");Assert.That(DialogueUI.Active.Speaker,Is.EqualTo("ガイド"));
        Assert.That(DialogueUI.Active.Current.dialogueId,Is.EqualTo(current));Assert.That(StageSession.TutorialCursor,Is.EqualTo(cursor));Assert.That(battle.Context.Round,Is.EqualTo(round));Assert.That(battle.Context.Enemy.HP,Is.EqualTo(hp));
        while(DialogueUI.IsOpen){yield return new WaitForSecondsRealtime(.22f);DialogueUI.Active.Advance();yield return null;}yield return null;
        var view=Object.FindAnyObjectByType<TutorialFocusUI>();var hint=Field<TMP_Text>(view,"hint");Assert.That(hint.text,Is.EqualTo(Localization.TextFor("ja","ui.tutorial.hint")));cursor=StageSession.TutorialCursor;
        Language("en");Assert.That(hint.text,Is.EqualTo(Localization.TextFor("en","ui.tutorial.hint")));Language("ja");Assert.That(hint.text,Is.EqualTo(Localization.TextFor("ja","ui.tutorial.hint")));Assert.That(StageSession.TutorialCursor,Is.EqualTo(cursor));Assert.That(battle.FeatureAvailable(TutorialFeature.Adverb),Is.False);Assert.That(battle.LastCommittedAction,Is.Null);
    }
    [UnityTest] public IEnumerator NeutralAndWeakAffinityMatchActualBattleLogsAndHPInBothLanguages()
    {
        foreach(string language in new[]{"ja","en"})foreach(bool weak in new[]{false,true})
        {
            Language(language);yield return Load("Battle");var battle=Object.FindAnyObjectByType<BattleManager>();
            typeof(BattleCombatant).GetProperty("HP").SetValue(battle.Context.Enemy,500);typeof(BattleCombatant).GetProperty("HP").SetValue(battle.Context.Player,500);
            battle.Context.Enemy.Character.verbCollocations=new[]{"haben","werden","laufen","brennen",weak?"sein":"angreifen"}.Select((lemma,index)=>new VerbCollocation{lemma=lemma,partOfSpeech="verb",score=5-index}).ToArray();
            var affinity=weak?BattleAffinity.Weak:BattleAffinity.Normal;
            int expected=DamageCalculator.CalculateDamage(battle.Context.Player.Character,battle.Context.Enemy.Character,GameDatabase.Instance.GetSkill("sein"),battle.Context.Player.Equipment);
            battle.UseSkill1();yield return null;
            Assert.That(Field<TMP_Text>(battle,"battleLogText").text,Does.Contain(expected.ToString()).And.Contain(CollocationAffinity.Label(affinity)));
            float end=Time.realtimeSinceStartup+10;while(!battle.CanChoose && Time.realtimeSinceStartup<end)yield return null;
            Assert.That(battle.Context.Enemy.HP,Is.EqualTo(500-expected));yield return Load("GermanyHome");
        }
    }
    [UnityTest] public IEnumerator ScoredDefenderAffinityMatchesVisibleDamageForBothLanguagesAndFutureSnapshot()
    {
        foreach(string language in new[]{"ja","en"})
        {
            Language(language);GameManager.EnemyCharacterId="feuer";yield return Load("Battle");var battle=Object.FindAnyObjectByType<BattleManager>();
            typeof(BattleCombatant).GetProperty("HP").SetValue(battle.Context.Enemy,500);typeof(BattleCombatant).GetProperty("HP").SetValue(battle.Context.Player,500);
            battle.Context.Enemy.Character.verbCollocations=new[]{new VerbCollocation{lemma="sein",partOfSpeech="verb",score=20},new VerbCollocation{lemma="haben",partOfSpeech="verb",score=10}};
            int expected=DamageCalculator.CalculateDamage(battle.Context.Player.Character,battle.Context.Enemy.Character,GameDatabase.Instance.GetSkill("sein"),battle.Context.Player.Equipment);
            battle.UseSkill1();Assert.That(battle.LastCommittedAction.Skill.id,Is.EqualTo("sein"));yield return null;
            Assert.That(Field<TMP_Text>(battle,"battleLogText").text,Does.Contain(expected.ToString()).And.Contain(Localization.Text("ui.effective")));
            float end=Time.realtimeSinceStartup+10;while(!battle.CanChoose && Time.realtimeSinceStartup<end)yield return null;
            Assert.That(battle.Context.Enemy.HP,Is.EqualTo(500-expected));Assert.That(battle.SelectFuture(true),Is.True);battle.UseSkill1();yield return null;
            var reserved=battle.Context.PendingAttack;end=Time.realtimeSinceStartup+10;while((battle.Context.Round<3 || !battle.CanChoose || battle.Context.PendingAttack!=null) && Time.realtimeSinceStartup<end)yield return null;
            Assert.That(battle.Context.Enemy.HP,Is.EqualTo(500-expected-BattleActionResolver.ScaleDamage(expected,1.5f)));Assert.That(battle.Context.PendingAttack,Is.Null);
            yield return Load("GermanyHome");StageSession.Clear();
        }
    }
}

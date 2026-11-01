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
[PrebuildSetup(typeof(TestSaveIsolation))][PostBuildCleanup(typeof(TestSaveIsolation))]
public class SettingsSceneTests
{
    private string directory;
    private static T Field<T>(object value,string field)=>(T)value.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);
    private static Button Button(string name)=>Object.FindObjectsByType<Button>().Single(b=>b.name==name);
    private IEnumerator Load(string scene){SceneManager.LoadScene(scene);yield return null;yield return null;LocalizationUI.RefreshAll();}
    [UnitySetUp] public IEnumerator Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"LanguageBattleSettingsPlay",Guid.NewGuid().ToString("N"));UserSettings.Initialize(Path.Combine(directory,"Settings"),SystemLanguage.English);
        yield return Load("GermanyHome");if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);PlayerSaveService.Initialize(Path.Combine(directory,"Progress"));GameManager.SelectedCharacterId="nacht";GameManager.EnemyCharacterId="feuer";StageSession.Clear();
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {if(SettingsUI.Active!=null)SettingsUI.Active.Close();yield return Load("GermanyHome");if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);UserSettings.Initialize(Path.Combine(directory,"Cleanup"),SystemLanguage.English);if(Directory.Exists(directory))Directory.Delete(directory,true);}
    [UnityTest] public IEnumerator CommonSettingsOpenFromEveryNonBattleSceneAreModalSingletonAndCloseRestoresOriginalState()
    {
        foreach(string scene in new[]{"GermanyHome","StageSelect","CharacterSelect","CharacterCollection","CharacterDetail","Inventory","Gacha","Crafting","Result"})
        {
            GameManager.SelectedCharacterId="nacht";yield return Load(scene);string before=JsonUtility.ToJson(PlayerSaveService.Instance.Capture());
            Button("SettingsOpen").onClick.Invoke();yield return null;Assert.That(SettingsUI.IsOpen,Is.True,scene);Assert.That(SettingsUI.Show(),Is.False);Assert.That(Object.FindObjectsByType<SettingsUI>().Length,Is.EqualTo(1));
            var underlying=Button("SettingsOpen");Assert.That(underlying.IsInteractable(),Is.False);
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,((RectTransform)underlying.transform).TransformPoint(((RectTransform)underlying.transform).rect.center))};var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Assert.That(hits.First().gameObject.GetComponentInParent<SettingsUI>(),Is.EqualTo(SettingsUI.Active));
            Button("SettingsClose").onClick.Invoke();yield return null;Assert.That(SettingsUI.IsOpen,Is.False);Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo(scene));Assert.That(JsonUtility.ToJson(PlayerSaveService.Instance.Capture()),Is.EqualTo(before));
        }
        yield return Load("Battle");Assert.That(SettingsUI.CanOpen,Is.False);Assert.That(SettingsUI.Show(),Is.False);Assert.That(GameObject.Find("SettingsLauncher"),Is.Null);
    }
    [UnityTest] public IEnumerator LanguageChangesImmediatelyIncludingUnderlyingUiPersistsAcrossSceneAndRestartAndKeepsDraft()
    {
        yield return Load("CharacterDetail");var detail=Object.FindAnyObjectByType<CharacterDetailManager>();typeof(CharacterDetailManager).GetMethod("EquipCharacterEquipment",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(detail,new object[]{"schnell"});
        string draft=Field<string>(detail,"tempCharacterEquipmentId");Assert.That(SettingsUI.Show(),Is.True);Assert.That(SettingsUI.Active.SetLanguage("ja"),Is.True);
        Assert.That(GameObject.Find("SettingsTitle").GetComponent<TMP_Text>().text,Is.EqualTo("設定"));Assert.That(GameObject.Find("SettingsLanguageLabel").GetComponent<TMP_Text>().text,Is.EqualTo("表示言語"));
        Assert.That(Field<string>(detail,"tempCharacterEquipmentId"),Is.EqualTo(draft));Assert.That(Field<TMP_Text>(detail,"characterNameText").text,Is.EqualTo("Nacht"));
        SettingsUI.Active.Close();yield return Load("StageSelect");Assert.That(GameObject.Find("StagesTitle").GetComponent<TMP_Text>().text,Is.EqualTo("ステージ一覧"));Assert.That(GameObject.Find("StageRow_stage_1").GetComponentInChildren<TMP_Text>().text,Does.Contain("Technik"));
        UserSettings.Initialize(Path.Combine(directory,"Settings"),SystemLanguage.English);Assert.That(UserSettings.Language,Is.EqualTo("ja"));SettingsUI.Show();SettingsUI.Active.SetLanguage("en");Assert.That(GameObject.Find("SettingsTitle").GetComponent<TMP_Text>().text,Is.EqualTo("Settings"));SettingsUI.Active.Close();yield return Load("GermanyHome");Assert.That(UserSettings.Language,Is.EqualTo("en"));
    }
    [UnityTest] public IEnumerator DataTabHasOnlyFutureInformationAndSceneTransitionClosesSettings()
    {SettingsUI.Show();Button("SettingsTab2").onClick.Invoke();yield return null;Assert.That(GameObject.Find("SettingsDataSoon").GetComponent<TMP_Text>().text,Does.Contain("future"));Assert.That(Field<GameObject>(SettingsUI.Active,"dataPage").GetComponentsInChildren<Button>().Length,Is.Zero);yield return Load("Inventory");Assert.That(SettingsUI.IsOpen,Is.False);Assert.That(Button("SettingsOpen").IsInteractable(),Is.True);}
    [UnityTest] public IEnumerator SlidersImmediatelyRouteIndependentAudioMutePreviewAndRestorePersistedValues()
    {
        SettingsUI.Show();Button("SettingsTab1").onClick.Invoke();var ui=SettingsUI.Active;
        var owner=new GameObject("Audio test");var source=owner.AddComponent<AudioSource>();var clip=Resources.Load<AudioClip>("Audio/Words/feuer");GameAudio.PlayOneShot(source,clip,GameAudioChannel.Bgm);GameAudio.PlayOneShot(source,clip,GameAudioChannel.Sfx);GameAudio.PlayOneShot(source,clip,GameAudioChannel.Voice);
        ui.ChangeVolume(0,50);ui.ChangeVolume(1,20);ui.ChangeVolume(2,40);ui.ChangeVolume(3,80);
        Assert.That(owner.transform.Find("AudioChannel_Bgm").GetComponent<AudioSource>().volume,Is.EqualTo(.1f).Within(.001));Assert.That(owner.transform.Find("AudioChannel_Sfx").GetComponent<AudioSource>().volume,Is.EqualTo(.2f).Within(.001));Assert.That(owner.transform.Find("AudioChannel_Voice").GetComponent<AudioSource>().volume,Is.EqualTo(.4f).Within(.001));
        Assert.That(ui.PreviewVoice(),Is.True);Assert.That(ui.PreviewVoice(),Is.True);Assert.That(ui.GetComponentsInChildren<AudioSource>().Length,Is.EqualTo(2));ui.ChangeVolume(3,0);Assert.That(ui.GetComponentsInChildren<AudioSource>().Last().volume,Is.Zero);
        ui.ChangeVolume(0,0);Assert.That(owner.GetComponentsInChildren<AudioSource>().Skip(1).All(s=>s.volume==0),Is.True);SettingsUI.Active.Close();yield return null;UserSettings.Initialize(Path.Combine(directory,"Settings"),SystemLanguage.English);Assert.That(UserSettings.Current.bgmVolume,Is.EqualTo(.2f));Assert.That(UserSettings.Current.masterVolume,Is.Zero);Object.Destroy(owner);
    }
    [UnityTest] public IEnumerator MissingPreviewAndSettingsSaveFailureAreSafeAndRetryable()
    {
        SettingsUI.Show();var ui=SettingsUI.Active;typeof(SettingsUI).GetField("sample",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ui,null);Assert.That(ui.PreviewVoice(),Is.False);Assert.That(Field<TMP_Text>(ui,"message").text,Does.Contain("unavailable"));
        string settingsDirectory=Path.Combine(directory,"Blocked");File.WriteAllText(settingsDirectory,"block");UserSettings.Initialize(settingsDirectory,SystemLanguage.English);Assert.That(ui.SetLanguage("ja"),Is.False);Assert.That(UserSettings.Language,Is.EqualTo("en"));Assert.That(Field<TMP_Text>(ui,"message").text,Does.Contain("Unable to save"));File.Delete(settingsDirectory);Assert.That(ui.SetLanguage("ja"),Is.True);yield return null;
    }
    [UnityTest] public IEnumerator DynamicGachaAndPracticeRewardsSwitchLanguageWithoutRerollingOrGrantingAgain()
    {
        yield return Load("Gacha");var gacha=Object.FindAnyObjectByType<GachaManager>();
        var draws=Enumerable.Repeat(new GachaItemData{id="word_nacht",rarity="SSR",itemType="word"},10).ToArray();
        Assert.That(gacha.GrantDraws(draws,true),Is.True);string state=JsonUtility.ToJson(PlayerSaveService.Instance.Capture());
        SettingsUI.Show();SettingsUI.Active.SetLanguage("ja");Assert.That(Field<TMP_Text>(gacha,"resultText").text,Does.Contain("キャラクターを獲得！").And.Contain("Nacht"));
        SettingsUI.Active.SetLanguage("en");Assert.That(Field<TMP_Text>(gacha,"resultText").text,Does.Contain("Character obtained!"));Assert.That(JsonUtility.ToJson(PlayerSaveService.Instance.Capture()),Is.EqualTo(state));SettingsUI.Active.Close();
        Assert.That(BattleRewardService.Begin("feuer",out string battleId,out _,"feuer_practice"),Is.True);Assert.That(BattleRewardService.Complete(battleId,true,out _,()=>.9),Is.True);
        yield return Load("Result");SettingsUI.Show();SettingsUI.Active.SetLanguage("ja");Assert.That(Object.FindAnyObjectByType<ResultManager>().RewardSummary,Does.Contain("Feuer 練習戦").And.Contain("初回クリア報酬：+300"));
        state=JsonUtility.ToJson(PlayerSaveService.Instance.Capture());SettingsUI.Active.SetLanguage("en");Assert.That(Object.FindAnyObjectByType<ResultManager>().RewardSummary,Does.Contain("Feuer Practice"));Assert.That(JsonUtility.ToJson(PlayerSaveService.Instance.Capture()),Is.EqualTo(state));
    }
    private IEnumerator DialogueAdvance(){yield return new WaitForSecondsRealtime(.22f);DialogueUI.Active.Advance();yield return null;}
    [UnityTest] public IEnumerator TechnikJapaneseAndEnglishShareLocksCursorSkipReviewStageProgressAndFirstReward()
    {
        foreach(string language in new[]{"ja","en"})
        {
            var settings=UserSettings.Current;settings.uiLanguage=language;UserSettings.TryUpdate(settings,out _);yield return Load("StageSelect");TutorialChoiceUI.Request("stage_1",true);yield return null;Button("TutorialOption0").onClick.Invoke();yield return null;yield return null;
            Object.FindAnyObjectByType<CharacterSelectManager>().StartBattle();yield return null;yield return null;var battle=Object.FindAnyObjectByType<BattleManager>();Assert.That(DialogueUI.IsOpen,Is.True);Assert.That(DialogueUI.Active.Text,Does.Contain(language=="ja"?"戦闘の基本":"basics of battle"));Assert.That(SettingsUI.Show(),Is.False);Assert.That(battle.SelectFuture(true),Is.False);battle.UseSkill1();Assert.That(battle.LastCommittedAction,Is.Null);
            string dialogueId=DialogueUI.Active.Current.dialogueId;int cursor=StageSession.TutorialCursor;settings.uiLanguage=language=="ja"?"en":"ja";UserSettings.TryUpdate(settings,out _);Assert.That(DialogueUI.Active.Current.dialogueId,Is.EqualTo(dialogueId));Assert.That(StageSession.TutorialCursor,Is.EqualTo(cursor));settings.uiLanguage=language;UserSettings.TryUpdate(settings,out _);
            while(DialogueUI.IsOpen)yield return DialogueAdvance();battle.UseSkill2();Assert.That(battle.LastCommittedAction,Is.Null);typeof(BattleCombatant).GetProperty("HP").SetValue(battle.Context.Enemy,1);battle.UseSkill1();float end=Time.realtimeSinceStartup+20;
            while(SceneManager.GetActiveScene().name=="Battle"&&Time.realtimeSinceStartup<end){if(DialogueUI.IsOpen)yield return DialogueAdvance();else yield return null;}
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("Result"));yield return null;Assert.That(StageProgress.IsUnlocked("stage_2"),Is.True);Assert.That(TutorialPreferences.Get("technik_basics").completed,Is.True);Assert.That(CurrencyService.Balance,Is.EqualTo(language=="ja"?3400:3500));
        }
    }
    [UnityTest] public IEnumerator LearningLabelsPreserveMaterialWordsEvenIfTheyEqualUiSourceAndCollectionFilterRemainsFunctional()
    {
        var go=new GameObject("Learning label");var label=go.AddComponent<TextMeshProUGUI>();label.text="Name";LocalizedText.Learning(label);var data=UserSettings.Current;data.uiLanguage="ja";UserSettings.TryUpdate(data,out _);Assert.That(label.text,Is.EqualTo("Name"));Object.Destroy(go);
        yield return Load("CharacterCollection");var manager=Object.FindAnyObjectByType<CharacterCollectionManager>();var dropdown=Field<TMP_Dropdown>(manager,"genderDropdown");dropdown.value=1;yield return null;Assert.That(dropdown.options[1].text,Is.EqualTo("masculine"));Assert.That(Field<Transform>(manager,"characterButtonParent").GetComponentsInChildren<Button>().Length,Is.EqualTo(GameDatabase.Instance.GetAllCharacters().Count(c=>c.grammaticalGender=="masculine")));
    }
}

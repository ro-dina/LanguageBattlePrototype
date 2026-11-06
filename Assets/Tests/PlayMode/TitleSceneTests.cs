using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;
[PrebuildSetup(typeof(TestSaveIsolation))][PostBuildCleanup(typeof(TestSaveIsolation))]
public class TitleSceneTests
{
    private string directory, settingsDirectory;
    private static Button Button(string name)=>Object.FindObjectsByType<Button>().Single(b=>b.name==name);
    private static string Fingerprint(string path)=>Directory.Exists(path)?string.Join("\n",Directory.GetFiles(path,"*",SearchOption.AllDirectories).OrderBy(p=>p).Select(p=>p+":"+File.GetLastWriteTimeUtc(p).Ticks+":"+Convert.ToBase64String(File.ReadAllBytes(p)))):"missing";
    private IEnumerator Load(string scene){SceneManager.LoadScene(scene);yield return null;yield return null;}
    [UnitySetUp] public IEnumerator Setup()
    {
        SettingsUI.Active?.Close();ProgressResetUI.Active?.Close();yield return Load("Title");
        if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        directory=PlayerSaveService.DefaultDirectory();Assert.That(Path.GetFullPath(directory).StartsWith(Path.GetTempPath()) || Path.GetFullPath(directory).StartsWith("/private/tmp/phase120-"),Is.True);
        if(Directory.Exists(directory))Directory.Delete(directory,true);
        settingsDirectory=directory+"-TitleSettings";UserSettings.Initialize(settingsDirectory,SystemLanguage.English);yield return Load("Title");
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        SettingsUI.Active?.Close();ProgressResetUI.Active?.Close();yield return Load("Title");
        if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        if(Directory.Exists(directory))Directory.Delete(directory,true);if(Directory.Exists(settingsDirectory))Directory.Delete(settingsDirectory,true);
    }
    private void StartNew(){Button("TitleNewGameButton").onClick.Invoke();Button("TitleStartButton").onClick.Invoke();}
    private void Enrich()
    {
        CurrencyService.TryAdd(1234,out _);InventoryManager.AddItem("letter_a",8);CharacterInstances.Create("feuer");
        var draft=CharacterLoadoutStore.GetLoadout(CharacterInstances.GetByCharacterId("nacht").Single().instanceId);draft.adjectiveId="schwarz";CharacterLoadoutStore.Commit(draft);
        TutorialPreferences.Choose("technik_basics",TutorialChoice.SkipAll,out _);
        BattleRewardService.Begin("technik",out string id,out _,"stage_1");BattleRewardService.Complete(id,true,out _,()=>.9);
    }
    [UnityTest] public IEnumerator FirstRunShowsDisabledContinueAndCancelCreatesNoProgress()
    {
        Assert.That(SceneUtility.GetScenePathByBuildIndex(0),Is.EqualTo("Assets/Scenes/Title.unity"));Assert.That(PlayerSaveService.Instance,Is.Null);Assert.That(Button("TitleContinueButton").interactable,Is.False);Assert.That(Button("TitleNewGameButton").interactable,Is.True);
        Assert.That(TitleScreenController.Active.SelectedLearningLanguage,Is.EqualTo("de"));Assert.That(TitleScreenController.Active.SelectLearningLanguage("en"),Is.False);Assert.That(TitleScreenController.Active.SelectLearningLanguage("de"),Is.True);
        string before=Fingerprint(directory);Button("TitleContinueButton").onClick.Invoke();Assert.That(PlayerSaveService.Instance,Is.Null);
        Button("TitleNewGameButton").onClick.Invoke();Assert.That(TitleScreenController.IsModalOpen,Is.True);Assert.That(Button("TitleNewGameButton").IsInteractable(),Is.False);Assert.That(SettingsUI.Show(),Is.False);
        Button("TitleCancelButton").onClick.Invoke();Assert.That(TitleScreenController.IsModalOpen,Is.False);Assert.That(Fingerprint(directory),Is.EqualTo(before));yield return null;
    }
    [UnityTest] public IEnumerator NewGameCreatesNachtDefaultsAndGenerationThenHomeEntryReturnsWithoutSaving()
    {
        StartNew();yield return null;yield return null;Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("GermanyHome"));var data=PlayerSaveService.Instance.Capture();Assert.That(data.wallet.balance,Is.EqualTo(EconomySettings.Current.initialCoins));Assert.That(data.characterInstances.instances.Select(i=>i.characterId),Is.EqualTo(new[]{"nacht"}));Assert.That(data.characterEquipment.characters,Is.Empty);Assert.That(data.version,Is.EqualTo(6));Assert.That(File.Exists(Path.Combine(directory,"save-generation.json")),Is.True);
        string before=Fingerprint(directory);Button("HomeTitleButton").onClick.Invoke();yield return null;yield return null;Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("Title"));Assert.That(Fingerprint(directory),Is.EqualTo(before));Assert.That(Button("TitleContinueButton").interactable,Is.True);
        Button("TitleContinueButton").onClick.Invoke();yield return null;yield return null;Assert.That(JsonUtility.ToJson(PlayerSaveService.Instance.Capture()),Is.EqualTo(JsonUtility.ToJson(data)));
    }
    [UnityTest] public IEnumerator ExistingRichSaveTitleCancelAndContinueKeepIdentityEquipmentCurrencyProgressAndReceipts()
    {
        PlayerSaveService.Initialize(directory);Enrich();string state=JsonUtility.ToJson(PlayerSaveService.Instance.Capture());string before=Fingerprint(directory);yield return Load("Title");Assert.That(Fingerprint(directory),Is.EqualTo(before));
        Button("TitleNewGameButton").onClick.Invoke();Assert.That(Fingerprint(directory),Is.EqualTo(before));Button("TitleCancelButton").onClick.Invoke();Assert.That(Fingerprint(directory),Is.EqualTo(before));
        Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);yield return Load("Title");Assert.That(Fingerprint(directory),Is.EqualTo(before));Assert.That(PlayerSaveService.Instance,Is.Null);
        Button("TitleContinueButton").onClick.Invoke();yield return null;yield return null;Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("GermanyHome"));Assert.That(JsonUtility.ToJson(PlayerSaveService.Instance.Capture()),Is.EqualTo(state));
    }
    [UnityTest] public IEnumerator ConfirmedReplacementKeepsOldGenerationAndSettingsAndRestartUsesOnlyNewProgress()
    {
        PlayerSaveService.Initialize(directory);Enrich();string oldMain=PlayerSaveService.Instance.Files.MainPath;string old=File.ReadAllText(oldMain);string oldId=CharacterInstances.GetByCharacterId("nacht").Single().instanceId;
        var settings=UserSettings.Current;settings.uiLanguage="ja";settings.masterVolume=.42f;settings.voiceVolume=.25f;UserSettings.TryUpdate(settings,out _);string user=Fingerprint(settingsDirectory);
        yield return Load("Title");StartNew();yield return null;yield return null;var data=PlayerSaveService.Instance.Capture();Assert.That(data.characterInstances.instances.Single().instanceId,Is.Not.EqualTo(oldId));Assert.That(data.wallet.balance,Is.EqualTo(3000));Assert.That(data.tutorials.skipAll,Is.False);Assert.That(data.stageProgress.records,Is.Empty);Assert.That(BattleRewardService.Latest,Is.Null);Assert.That(File.ReadAllText(oldMain),Is.EqualTo(old));Assert.That(Fingerprint(settingsDirectory),Is.EqualTo(user));
        string state=JsonUtility.ToJson(data);yield return Load("Title");Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);TitleScreenController.Active.Refresh();Button("TitleContinueButton").onClick.Invoke();yield return null;yield return null;Assert.That(JsonUtility.ToJson(PlayerSaveService.Instance.Capture()),Is.EqualTo(state));Assert.That(Fingerprint(settingsDirectory),Is.EqualTo(user));
    }
    [UnityTest] public IEnumerator TitleSettingsReturnImmediatelyUpdatesJapaneseEnglishWithoutCreatingProgress()
    {
        Button("TitleSettingsButton").onClick.Invoke();Assert.That(SettingsUI.IsOpen,Is.True);Assert.That(Button("TitleNewGameButton").IsInteractable(),Is.False);SettingsUI.Active.SetLanguage("ja");SettingsUI.Active.ChangeVolume(0,37);SettingsUI.Active.Close();yield return null;
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("Title"));Assert.That(Button("TitleNewGameButton").GetComponentInChildren<TMP_Text>().text,Is.EqualTo("はじめから"));Assert.That(Button("TitleDeutschButton").GetComponentInChildren<TMP_Text>().text,Does.Contain("Deutsch"));Assert.That(UserSettings.LearningLanguage,Is.EqualTo("de"));Assert.That(Directory.Exists(directory),Is.False);
        Button("TitleSettingsButton").onClick.Invoke();SettingsUI.Active.SetLanguage("en");SettingsUI.Active.Close();Assert.That(Button("TitleNewGameButton").GetComponentInChildren<TMP_Text>().text,Is.EqualTo("New game"));Assert.That(UserSettings.Current.masterVolume,Is.EqualTo(.37f).Within(.001f));
        StartNew();yield return null;yield return null;SettingsUI.Show();SettingsUI.Active.Close();Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("GermanyHome"));
    }
    [UnityTest] public IEnumerator AccountNoticeIsExplicitlyUnavailableInBothLanguagesAndDoesNotTouchProgress()
    {
        foreach(string language in new[]{"ja","en"})
        {
            var settings=UserSettings.Current;settings.uiLanguage=language;UserSettings.TryUpdate(settings,out _);string before=Fingerprint(directory);
            Button("TitleAccountButton").onClick.Invoke();Assert.That(TitleScreenController.IsModalOpen,Is.True);Assert.That(GameObject.Find("TitleDialogDescription").GetComponent<TMP_Text>().text,Does.Contain(language=="ja"?"今後実装予定":"future update"));Assert.That(Object.FindObjectsByType<Button>().Any(b=>b.name=="TitleStartButton"),Is.False);Button("TitleCancelButton").onClick.Invoke();Assert.That(Fingerprint(directory),Is.EqualTo(before));
        }
        yield return null;
    }
    [UnityTest] public IEnumerator CorruptSaveDoesNotAutoStartAndBackupRecoveryRequiresContinue()
    {
        Directory.CreateDirectory(directory);string path=Path.Combine(directory,"save.json");File.WriteAllText(path,"broken");yield return Load("Title");Assert.That(Button("TitleContinueButton").interactable,Is.False);Assert.That(File.ReadAllText(path),Is.EqualTo("broken"));Assert.That(PlayerSaveService.Instance,Is.Null);
        var data=NewGameDefaults.Create();data.wallet.balance=4567;File.WriteAllText(path+".bak",JsonUtility.ToJson(data));string before=Fingerprint(directory);yield return Load("Title");Assert.That(Button("TitleContinueButton").interactable,Is.True);Assert.That(Fingerprint(directory),Is.EqualTo(before));Button("TitleContinueButton").onClick.Invoke();yield return null;yield return null;
        Assert.That(CurrencyService.Balance,Is.EqualTo(4567));Assert.That(File.ReadAllText(path),Is.EqualTo("broken"));Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("GermanyHome"));
    }
    [UnityTest] public IEnumerator FutureSaveDisablesContinueAndReplacementEvenThroughDirectCallbacks()
    {
        Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"save.json"),"{\"version\":99}");string before=Fingerprint(directory);yield return Load("Title");Assert.That(Button("TitleContinueButton").interactable,Is.False);Assert.That(Button("TitleNewGameButton").interactable,Is.False);
        TitleScreenController.Active.BeginNewGame();TitleScreenController.Active.Confirm();TitleScreenController.Active.Continue();Assert.That(TitleScreenController.IsModalOpen,Is.False);Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("Title"));Assert.That(Fingerprint(directory),Is.EqualTo(before));Assert.That(PlayerSaveService.Instance,Is.Null);
    }
    [UnityTest] public IEnumerator FailedNewGameCommitStaysOnTitleKeepsOriginalAndCanRetry()
    {
        PlayerSaveService.Initialize(directory);Enrich();string original=PlayerSaveService.Instance.Files.MainPath;string before=File.ReadAllText(original);yield return Load("Title");Directory.CreateDirectory(Path.Combine(directory,"save-generation.json"));
        StartNew();Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("Title"));Assert.That(File.ReadAllText(original),Is.EqualTo(before));Assert.That(GameObject.Find("TitleProgressMessage").GetComponent<TMP_Text>().text,Does.Contain("could not be saved"));Directory.Delete(Path.Combine(directory,"save-generation.json"));
        StartNew();yield return null;yield return null;Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("GermanyHome"));Assert.That(CurrencyService.Balance,Is.EqualTo(3000));
    }
    [UnityTest] public IEnumerator TouchscreenCanSelectDeutschOpenCancelAndConfirmStart()
    {
        var inputSettings=UnityEngine.InputSystem.InputSystem.settings;
        var background=inputSettings.backgroundBehavior;
#if UNITY_EDITOR
        var routing=inputSettings.editorInputBehaviorInPlayMode;
        inputSettings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        inputSettings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
        var screen=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Touchscreen>();
        try
        {
            foreach(string name in new[]{"TitleDeutschButton","TitleNewGameButton","TitleCancelButton","TitleNewGameButton","TitleStartButton"})
            {
                Canvas.ForceUpdateCanvases();var rect=(RectTransform)Button(name).transform;var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(screen,new UnityEngine.InputSystem.LowLevel.TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Began,position=point});
                UnityEngine.InputSystem.InputSystem.Update();
                Assert.That(screen.primaryTouch.press.isPressed,Is.True,"Touch press is queued: "+name);
                yield return null;yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(screen,new UnityEngine.InputSystem.LowLevel.TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=point});
                yield return null;yield return null;
                if(name=="TitleNewGameButton")Assert.That(TitleScreenController.IsModalOpen,Is.True,"Touch opens confirmation");
                if(name=="TitleCancelButton")Assert.That(TitleScreenController.IsModalOpen,Is.False);
            }
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("GermanyHome"));Assert.That(CharacterInstances.GetAll().Single().characterId,Is.EqualTo("nacht"));
        }
        finally
        {
            UnityEngine.InputSystem.InputSystem.RemoveDevice(screen);inputSettings.backgroundBehavior=background;
#if UNITY_EDITOR
            inputSettings.editorInputBehaviorInPlayMode=routing;
#endif
        }
    }

    [UnityTest] public IEnumerator FailedFirstStartReleasesUninitializedAutosaveServiceAndRetryDoesNotLoadOrphanGeneration()
    {
        Directory.CreateDirectory(directory);string block=Path.Combine(directory,"save-generation.json");Directory.CreateDirectory(block);
        string before=JsonUtility.ToJson(PlayerStateTransactions.Capture());StartNew();yield return null;
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("Title"));Assert.That(PlayerSaveService.Instance,Is.Null);
        Assert.That(JsonUtility.ToJson(PlayerStateTransactions.Capture()),Is.EqualTo(before));Assert.That(File.Exists(Path.Combine(directory,"save.json")),Is.False);
        var files=new SaveFileStore(directory);Assert.That(files.Inspect(out var missing),Is.EqualTo(SaveLoadStatus.NewGame));Assert.That(missing,Is.Null);
        Directory.Delete(block);StartNew();yield return null;yield return null;
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("GermanyHome"));Assert.That(CurrencyService.Balance,Is.EqualTo(3000));Assert.That(CharacterInstances.GetAll().Single().characterId,Is.EqualTo("nacht"));
    }

}

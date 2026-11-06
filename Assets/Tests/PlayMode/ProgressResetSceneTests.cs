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
public class ProgressResetSceneTests
{
    private string directory;
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);
    private static Button Button(string name)=>Object.FindObjectsByType<Button>().Single(b=>b.name==name);
    private IEnumerator Load(string scene){SceneManager.LoadScene(scene);yield return null;yield return null;LocalizationUI.RefreshAll();}
    [UnitySetUp] public IEnumerator Setup()
    {
        if(ProgressResetUI.Active!=null)ProgressResetUI.Active.Close();if(SettingsUI.Active!=null)SettingsUI.Active.Close();yield return Load("GermanyHome");
        if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        directory=Path.Combine(Path.GetTempPath(),"LanguageBattleResetScenes",Guid.NewGuid().ToString("N"));PlayerSaveService.Initialize(Path.Combine(directory,"Progress"));UserSettings.Initialize(Path.Combine(directory,"Settings"),SystemLanguage.English);StageSession.Clear();GameManager.SelectedCharacterId="nacht";GameManager.EnemyCharacterId="feuer";
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {if(ProgressResetUI.Active!=null)ProgressResetUI.Active.Close();if(SettingsUI.Active!=null)SettingsUI.Active.Close();yield return Load("GermanyHome");if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);UserSettings.Initialize(Path.Combine(directory,"Cleanup"),SystemLanguage.English);if(Directory.Exists(directory))Directory.Delete(directory,true);}
    private void Enrich()
    {
        CurrencyService.TryAdd(9000,out _);InventoryManager.AddItem("letter_a",22);RewardGrantService.Grant(new[]{DynamicMaterials.Entry("Wass",4)},out _);
        var fire=CharacterInstances.Create("feuer");var loadout=CharacterLoadoutStore.GetLoadout(fire.instanceId);loadout.adjectiveId="schnell";CharacterLoadoutStore.Commit(loadout);GameManager.SelectedCharacterInstanceId=fire.instanceId;
        TutorialPreferences.Choose("technik_basics",TutorialChoice.SkipAll,out _);
        BattleRewardService.Begin("technik",out string id,out _,"stage_1",true);Assert.That(BattleRewardService.Complete(id,true,out _,()=>.9,tutorialCompleted:true),Is.True);
    }
    private ProgressResetRequest Request(ProgressResetKind kind){var request=ProgressResetService.Request(kind,out string error);Assert.That(request,Is.Not.Null,error);return request;}
    private IEnumerator OpenData(){SettingsUI.Show();Button("SettingsTab2").onClick.Invoke();yield return null;}
    private void Acknowledge()=>Object.FindAnyObjectByType<ProgressResetUI>().GetComponentInChildren<Toggle>().isOn=true;
    [UnityTest] public IEnumerator FullResetUsesDefaultsClearsAllProgressKeepsSettingsBytesAndRejectsOldTicketsAndRepeatedCommand()
    {
        Enrich();string completedTicket=BattleRewardService.Latest.battleId;string[] previousIds=CharacterInstances.GetAll().Select(i=>i.instanceId).ToArray();BattleRewardService.Begin("feuer",out string oldTicket,out _);
        var settings=UserSettings.Current;settings.uiLanguage="ja";settings.masterVolume=.42f;settings.bgmVolume=.25f;settings.sfxVolume=.61f;settings.voiceVolume=.73f;UserSettings.TryUpdate(settings,out _);byte[] bytes=File.ReadAllBytes(UserSettings.Store.MainPath);DateTime modified=File.GetLastWriteTimeUtc(UserSettings.Store.MainPath);
        var request=Request(ProgressResetKind.NewGame);Assert.That(ProgressResetService.TryExecute(request,out _),Is.True);Assert.That(ProgressResetService.TryExecute(request,out _),Is.False);
        var data=PlayerSaveService.Instance.Capture();Assert.That(data.wallet.balance,Is.EqualTo(EconomySettings.Current.initialCoins));Assert.That(data.characterInstances.instances.Select(i=>i.characterId),Is.EqualTo(NewGameDefaults.StarterCharacterIds));Assert.That(previousIds,Does.Not.Contain(data.characterInstances.instances[0].instanceId));
        Assert.That(data.characterCollection.discoveredCharacterIds,Is.EqualTo(NewGameDefaults.StarterCharacterIds));Assert.That(data.characterEquipment.characters,Is.Empty);Assert.That(data.inventory.items,Is.Empty);Assert.That(data.stageProgress.records,Is.Empty);Assert.That(data.stageProgress.unlockedStageIds,Is.Empty);Assert.That(data.tutorials.skipAll,Is.False);Assert.That(data.tutorials.records,Is.Empty);Assert.That(data.convertedWordItems,Is.Empty);Assert.That(BattleRewardService.Latest,Is.Null);
        Assert.That(BattleRewardService.Complete(oldTicket,true,out _),Is.False);Assert.That(BattleRewardService.Complete(completedTicket,true,out _),Is.False);Assert.That(CharacterInstances.Owns(GameManager.SelectedCharacterInstanceId),Is.True);Assert.That(StageSession.StageId,Is.Null);Assert.That(GameManager.IsWin,Is.False);
        Assert.That(File.ReadAllBytes(UserSettings.Store.MainPath),Is.EqualTo(bytes));Assert.That(File.GetLastWriteTimeUtc(UserSettings.Store.MainPath),Is.EqualTo(modified));Assert.That(JsonUtility.ToJson(UserSettings.Current),Is.EqualTo(JsonUtility.ToJson(settings)));
        string state=JsonUtility.ToJson(data);PlayerSaveService.Instance.Reload();Assert.That(JsonUtility.ToJson(PlayerSaveService.Instance.Capture()),Is.EqualTo(state));
        BattleRewardService.Begin("technik",out string newTicket,out _,"stage_1");Assert.That(BattleRewardService.Complete(newTicket,true,out _,()=>.9),Is.True);long balance=CurrencyService.Balance;Assert.That(balance,Is.EqualTo(EconomySettings.Current.initialCoins+400));Assert.That(BattleRewardService.Complete(newTicket,true,out _,()=>0),Is.True);Assert.That(CurrencyService.Balance,Is.EqualTo(balance));yield return null;
    }
    [UnityTest] public IEnumerator TutorialAndStageResetsOnlyChangeTheirTargetsAndInvalidateReceipts()
    {
        Enrich();var before=PlayerSaveService.Instance.Capture();Assert.That(ProgressResetService.TryExecute(Request(ProgressResetKind.Tutorials),out _),Is.True);var after=PlayerSaveService.Instance.Capture();
        Assert.That(after.tutorials.records,Is.Empty);Assert.That(after.tutorials.skipAll,Is.False);Assert.That(JsonUtility.ToJson(after.stageProgress),Is.EqualTo(JsonUtility.ToJson(before.stageProgress)));Assert.That(BattleRewardService.Latest,Is.Null);
        after.tutorials=before.tutorials;after.battleRewards=before.battleRewards;Assert.That(JsonUtility.ToJson(after),Is.EqualTo(JsonUtility.ToJson(before)));
        TutorialPreferences.Choose("technik_basics",TutorialChoice.SkipAll,out _);before=PlayerSaveService.Instance.Capture();Assert.That(ProgressResetService.TryExecute(Request(ProgressResetKind.Stages),out _),Is.True);after=PlayerSaveService.Instance.Capture();
        Assert.That(after.stageProgress.records,Is.Empty);Assert.That(StageProgress.IsUnlocked("stage_1"),Is.True);Assert.That(StageProgress.IsUnlocked("stage_2"),Is.False);Assert.That(JsonUtility.ToJson(after.tutorials),Is.EqualTo(JsonUtility.ToJson(before.tutorials)));
        after.stageProgress=before.stageProgress;after.battleRewards=before.battleRewards;Assert.That(JsonUtility.ToJson(after),Is.EqualTo(JsonUtility.ToJson(before)));
        BattleRewardService.Begin("technik",out string ticket,out _,"stage_1");Assert.That(BattleRewardService.Complete(ticket,true,out _,()=>.9),Is.True);Assert.That(CurrencyService.Balance,Is.EqualTo(before.wallet.balance+400));yield return null;
    }
    [UnityTest] public IEnumerator CancelAndUncheckedExecuteAreFreeNestedModalBlocksBackgroundAndSuccessReturnsHomeInJapanese()
    {
        Enrich();var settings=UserSettings.Current;settings.uiLanguage="ja";UserSettings.TryUpdate(settings,out _);string before=JsonUtility.ToJson(PlayerSaveService.Instance.Capture());yield return Load("Gacha");yield return OpenData();
        Button("ResetNewGameButton").onClick.Invoke();yield return null;Assert.That(ProgressResetUI.Active,Is.Not.Null);Assert.That(Button("ResetExecuteButton").IsInteractable(),Is.False);Assert.That(Button("SettingsClose").IsInteractable(),Is.False);Assert.That(SettingsUI.Show(),Is.False);
        Button("ResetExecuteButton").onClick.Invoke();Assert.That(JsonUtility.ToJson(PlayerSaveService.Instance.Capture()),Is.EqualTo(before));Button("ResetCancelButton").onClick.Invoke();yield return null;Assert.That(ProgressResetUI.Active,Is.Null);Assert.That(Button("SettingsClose").IsInteractable(),Is.True);
        Button("ResetNewGameButton").onClick.Invoke();yield return null;Acknowledge();Button("ResetExecuteButton").onClick.Invoke();yield return null;yield return null;
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("GermanyHome"));Assert.That(SettingsUI.Active,Is.Null);Assert.That(ProgressResetUI.Active,Is.Not.Null);Assert.That(Button("CoinsPlusButton").IsInteractable(),Is.False);Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(Button("ResetExecuteButton").gameObject));Assert.That(GameObject.Find("ResetTitle").GetComponent<TMP_Text>().text,Is.EqualTo("進行データを初期化しました"));Assert.That(UserSettings.Language,Is.EqualTo("ja"));Button("ResetExecuteButton").onClick.Invoke();yield return null;Assert.That(ProgressResetUI.Active,Is.Null);
    }
    [UnityTest] public IEnumerator FailedPointerCommitKeepsMemoryMainBackupSettingsAndSceneThenRetrySucceeds()
    {
        Enrich();yield return Load("CharacterDetail");yield return OpenData();string before=JsonUtility.ToJson(PlayerSaveService.Instance.Capture());string main=PlayerSaveService.Instance.Files.MainPath;byte[] disk=File.ReadAllBytes(main),backup=File.ReadAllBytes(main+".bak");string block=Path.Combine(Path.GetDirectoryName(main),"save-generation.json");Directory.CreateDirectory(block);
        Button("ResetNewGameButton").onClick.Invoke();yield return null;Acknowledge();Button("ResetExecuteButton").onClick.Invoke();Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("CharacterDetail"));Assert.That(JsonUtility.ToJson(PlayerSaveService.Instance.Capture()),Is.EqualTo(before));Assert.That(File.ReadAllBytes(main),Is.EqualTo(disk));Assert.That(File.ReadAllBytes(main+".bak"),Is.EqualTo(backup));Assert.That(Field<TMP_Text>(ProgressResetUI.Active,"message").text,Does.Contain("could not be saved"));
        Directory.Delete(block);Button("ResetExecuteButton").onClick.Invoke();yield return null;yield return null;Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("GermanyHome"));Assert.That(CurrencyService.Balance,Is.EqualTo(EconomySettings.Current.initialCoins));
    }
    [UnityTest] public IEnumerator InvalidScenesBattleLocksTransactionsAndUnsupportedSavesRejectRequests()
    {
        var command=Request(ProgressResetKind.NewGame);Assert.That(ProgressResetService.Request((ProgressResetKind)999,out _),Is.Null);yield return Load("Battle");Assert.That(ProgressResetService.Request(ProgressResetKind.NewGame,out _),Is.Null);Assert.That(ProgressResetService.TryExecute(command,out _),Is.False);yield return Load("GermanyHome");
        Assert.That(CharacterInstances.TryBeginBattle(GameManager.SelectedCharacterInstanceId),Is.True);Assert.That(ProgressResetService.Request(ProgressResetKind.NewGame,out _),Is.Null);CharacterInstances.EndBattle(GameManager.SelectedCharacterInstanceId);
        bool allowed=true;PlayerStateTransactions.TryChange(data=>{allowed=ProgressResetService.CanReset(ProgressResetKind.NewGame);return false;},out _);Assert.That(allowed,Is.False);
        string main=PlayerSaveService.Instance.Files.MainPath;File.WriteAllText(main,"{\"version\":7}");PlayerSaveService.Instance.Reload();Assert.That(ProgressResetService.Request(ProgressResetKind.NewGame,out _),Is.Null);Assert.That(File.ReadAllText(main),Is.EqualTo("{\"version\":7}"));
    }
    [UnityTest] public IEnumerator ResetThenGachaCraftAndRealBattleUseFreshCharactersAndNormalRewards()
    {
        Enrich();Assert.That(ProgressResetService.TryExecute(Request(ProgressResetKind.DevelopmentNewGame),out _),Is.True);yield return Load("Gacha");Assert.That(Object.FindAnyObjectByType<GachaManager>().TryPull(false),Is.True);
        foreach(string id in new[]{"letter_f","letter_e","letter_u","letter_r"})InventoryManager.AddItem(id,id=="letter_e"?2:1);
        var elements=new[]{"letter_f","letter_e","letter_u","letter_e","letter_r"}.Select(CraftingElement.Material).ToArray();Assert.That(CraftingManager.TryCraft(elements,out var created,out _,new PlayerOperation()),Is.True);Assert.That(created.characterId,Is.EqualTo("feuer"));
        GameManager.SelectedCharacterInstanceId=created.instanceId;GameManager.EnemyCharacterId="feuer";StageSession.Clear();yield return Load("Battle");var battle=Object.FindAnyObjectByType<BattleManager>();Assert.That(battle.Context.Player.Character.id,Is.EqualTo("feuer"));typeof(BattleCombatant).GetProperty("HP").SetValue(battle.Context.Enemy,1);battle.UseSkill1();
        float end=Time.realtimeSinceStartup+15;while(SceneManager.GetActiveScene().name=="Battle"&&Time.realtimeSinceStartup<end)yield return null;Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("Result"));Assert.That(BattleRewardService.Latest.status,Is.EqualTo(BattleRewardStatus.Victory));
    }
}

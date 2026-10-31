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

[PrebuildSetup(typeof(TestSaveIsolation))]
[PostBuildCleanup(typeof(TestSaveIsolation))]
public class CurrencySceneTests
{
    private string directory;
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    private static void Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);
    private static Button Button(string name)=>Object.FindObjectsByType<Button>().Single(b=>b.name==name);
    private string State()=>JsonUtility.ToJson(PlayerSaveService.Instance.Capture());
    private IEnumerator Load(string name){SceneManager.LoadScene(name);yield return null;yield return null;}
    [UnitySetUp] public IEnumerator Setup()
    {
        yield return Load("GermanyHome");if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        directory=Path.Combine(Path.GetTempPath(),"LanguageBattleCurrencySceneTests",Guid.NewGuid().ToString("N"));PlayerSaveService.Initialize(directory);
        yield return null;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        yield return Load("GermanyHome");if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        if(Directory.Exists(directory))Directory.Delete(directory,true);else if(File.Exists(directory))File.Delete(directory);
    }
    private void CheckHUD(long amount)
    {var hud=Object.FindAnyObjectByType<CurrencyHUD>();Assert.That(hud,Is.Not.Null);Assert.That(Field<TMP_Text>(hud,"balanceText").text,Is.EqualTo(EconomySettings.Price(amount)));}
    [UnityTest] public IEnumerator DevelopmentDialogUsesOfficialApiThrottlesDuplicatesAndPersistsAcrossEightScenes()
    {
        var hud=Object.FindAnyObjectByType<CurrencyHUD>();CheckHUD(3000);Assert.That(Button("CoinsPlusButton").gameObject.activeSelf,Is.True);
        Button("CoinsPlusButton").onClick.Invoke();Assert.That(Field<GameObject>(hud,"additionDialog").activeSelf,Is.True);
        Button("AddCoins0").onClick.Invoke();CheckHUD(3100);Assert.That(hud.AddDevelopmentCoins(0),Is.False);
        yield return new WaitForSecondsRealtime(.3f);Button("AddCoins1").onClick.Invoke();CheckHUD(4100);
        yield return new WaitForSecondsRealtime(.3f);Button("AddCoins2").onClick.Invoke();CheckHUD(14100);
        Button("CloseCoinDialog").onClick.Invoke();Assert.That(hud.AddDevelopmentCoins(0),Is.False);
        PlayerSaveService.Instance.Reload();
        foreach(string scene in new[]{"GermanyHome","StageSelect","Gacha","Crafting","Inventory","CharacterSelect","CharacterDetail","CharacterCollection"})
        {yield return Load(scene);Assert.That(Object.FindObjectsByType<CurrencyHUD>().Length,Is.EqualTo(1));CheckHUD(14100);}
    }
    [UnityTest] public IEnumerator GachaChargesOncePerAcceptedCommandAndInsufficiencyDoesNotDraw()
    {
        yield return Load("Gacha");var gacha=Object.FindAnyObjectByType<GachaManager>();int before=InventoryManager.GetAllItems().Sum(i=>i.count)+CharacterInstances.GetAll().Count;
        Button("PullButton").onClick.Invoke();CheckHUD(2900);Assert.That(gacha.TryPull(false),Is.False);
        yield return new WaitForSecondsRealtime(.3f);Button("PullTenButton").onClick.Invoke();CheckHUD(1900);
        Assert.That(InventoryManager.GetAllItems().Sum(i=>i.count)+CharacterInstances.GetAll().Count,Is.EqualTo(before+11));
        CurrencyService.TrySpend(1900,out _);yield return new WaitForSecondsRealtime(.3f);string snapshot=State();var random=UnityEngine.Random.state;
        Assert.That(gacha.TryPull(false),Is.False);Assert.That(gacha.TryPull(true),Is.False);Assert.That(UnityEngine.Random.state,Is.EqualTo(random));Assert.That(State(),Is.EqualTo(snapshot));
        Assert.That(Button("PullButton").interactable,Is.False);Assert.That(Button("PullTenButton").interactable,Is.False);
        Assert.That(Field<TMP_Text>(gacha,"costText").text,Does.Contain("Not enough Coins"));CheckHUD(0);
    }
    [UnityTest] public IEnumerator GachaSaveFailureIsAtomicAndSuccessfulTokenCannotBeCommittedAgain()
    {
        yield return Load("Gacha");var gacha=Object.FindAnyObjectByType<GachaManager>();var item=WordCharacterRewards.Item("word_nacht");var draws=Enumerable.Repeat(item,10).ToArray();var operation=new PlayerOperation();
        string before=State();Directory.Delete(directory,true);File.WriteAllText(directory,"blocked");
        Assert.That(gacha.GrantDraws(draws,true,operation),Is.False);Assert.That(State(),Is.EqualTo(before));CheckHUD(3000);
        File.Delete(directory);Directory.CreateDirectory(directory);Assert.That(gacha.GrantDraws(draws,true,operation),Is.True);CheckHUD(2000);Assert.That(CharacterInstances.Count("nacht"),Is.EqualTo(11));
        yield return new WaitForSecondsRealtime(.3f);before=State();Assert.That(gacha.GrantDraws(draws,true,operation),Is.False);Assert.That(State(),Is.EqualTo(before));
    }
    [UnityTest] public IEnumerator LettersOnlyCraftRequiresCostConfirmationCancelIsFreeAndAcceptanceSpendsOnce()
    {
        string[] ids={"letter_f","letter_e","letter_u","letter_e","letter_r"};foreach(string id in ids)InventoryManager.AddItem(id,1);
        yield return Load("Crafting");var ui=Object.FindAnyObjectByType<CraftingUIManager>();foreach(string id in ids)Call(ui,"SelectMaterial",id);
        string before=State();ui.CraftSelectedMaterials();Assert.That(Field<TMP_Text>(ui,"dialogDetails").text,Does.Contain("Cost: 200 Coins"));
        ui.CancelDialog();Assert.That(State(),Is.EqualTo(before));ui.CraftSelectedMaterials();ui.AcceptDialog();ui.AcceptDialog();yield return null;
        CheckHUD(2800);Assert.That(CharacterInstances.Count("feuer"),Is.EqualTo(1));Assert.That(InventoryManager.GetAllItems(),Is.Empty);
        PlayerSaveService.Instance.Reload();CheckHUD(2800);
    }
    [UnityTest] public IEnumerator ConfirmationRechecksFundsAndUpdatesItsCurrentBalanceBeforeConsumingIndividuals()
    {
        InventoryManager.AddItem("word_wasser",1);InventoryManager.AddItem("word_pistole",1);
        string water=CharacterInstances.GetByCharacterId("wasser")[0].instanceId,pistol=CharacterInstances.GetByCharacterId("pistole")[0].instanceId;
        yield return Load("Crafting");var ui=Object.FindAnyObjectByType<CraftingUIManager>();ui.SelectCharacterInstance(water);ui.SelectCharacterInstance(pistol);ui.CraftSelectedMaterials();
        CurrencyService.TrySpend(2900,out _);CheckHUD(100);Assert.That(Field<TMP_Text>(ui,"dialogDetails").text,Does.Contain("Current: 100 Coins").And.Contain("Not enough Coins"));Assert.That(Field<Button>(ui,"dialogAccept").interactable,Is.False);
        ui.AcceptDialog();Assert.That(CharacterInstances.Owns(water),Is.True);Assert.That(CharacterInstances.Owns(pistol),Is.True);CheckHUD(100);
        CurrencyService.TryAdd(100,out _);ui.CraftSelectedMaterials();ui.AcceptDialog();yield return null;CheckHUD(0);Assert.That(CharacterInstances.Count("wasserpistole"),Is.EqualTo(1));
    }
}

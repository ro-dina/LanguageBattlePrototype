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
public class BattleRewardSceneTests
{
    private string directory;
    private float scale;
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    private static Button Button(string name)=>Object.FindObjectsByType<Button>().Single(b=>b.name==name);
    private static void HP(BattleCombatant target,int value)=>typeof(BattleCombatant).GetProperty("HP").SetValue(target,value);
    private IEnumerator Load(string scene){SceneManager.LoadScene(scene);yield return null;yield return null;}
    [UnitySetUp] public IEnumerator Setup()
    {
        scale=Time.timeScale;Time.timeScale=8;yield return Load("GermanyHome");if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        directory=Path.Combine(Path.GetTempPath(),"LanguageBattleRewardScenes",Guid.NewGuid().ToString("N"));PlayerSaveService.Initialize(directory);GameManager.SelectedCharacterId="nacht";GameManager.EnemyCharacterId="feuer";yield return null;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        Time.timeScale=scale;yield return Load("GermanyHome");if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        if(Directory.Exists(directory))Directory.Delete(directory,true);else if(File.Exists(directory))File.Delete(directory);
    }
    private IEnumerator WaitResult()
    {float end=Time.realtimeSinceStartup+15;while(SceneManager.GetActiveScene().name!="Result" && Time.realtimeSinceStartup<end)yield return null;Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("Result"));yield return null;yield return null;}
    [UnityTest] public IEnumerator ActualVictorySavesOneRewardThenResultReloadAndRestartOnlyDisplayIt()
    {
        yield return Load("Battle");var battle=Object.FindAnyObjectByType<BattleManager>();string id=battle.RewardBattleId;HP(battle.Context.Enemy,1);battle.UseSkill1();yield return WaitResult();
        Assert.That(CurrencyService.Balance,Is.EqualTo(3100));Assert.That(BattleRewardService.Latest.battleId,Is.EqualTo(id));Assert.That(BattleRewardService.Latest.status,Is.EqualTo(BattleRewardStatus.Victory));
        var result=Object.FindAnyObjectByType<ResultManager>();Assert.That(result.RewardSummary,Does.Contain("Coins +100").And.Contain("Feuer defeated"));string saved=JsonUtility.ToJson(PlayerSaveService.Instance.Capture());
        yield return Load("GermanyHome");yield return Load("Result");Assert.That(JsonUtility.ToJson(PlayerSaveService.Instance.Capture()),Is.EqualTo(saved));
        Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);PlayerSaveService.Initialize(directory);yield return Load("Result");Assert.That(Object.FindAnyObjectByType<ResultManager>().RewardSummary,Does.Contain("Coins +100"));Assert.That(JsonUtility.ToJson(PlayerSaveService.Instance.Capture()),Is.EqualTo(saved));
    }
    [UnityTest] public IEnumerator DefeatDisplaysNoRewardsAndNewBattleGetsAnotherUniqueTicket()
    {
        yield return Load("Battle");var battle=Object.FindAnyObjectByType<BattleManager>();string first=battle.RewardBattleId;HP(battle.Context.Player,1);HP(battle.Context.Enemy,100);battle.UseSkill1();yield return WaitResult();
        Assert.That(CurrencyService.Balance,Is.EqualTo(3000));Assert.That(BattleRewardService.Latest.status,Is.EqualTo(BattleRewardStatus.Defeat));Assert.That(Object.FindAnyObjectByType<ResultManager>().RewardSummary,Is.Empty);
        Assert.That(Field<GameObject>(Object.FindAnyObjectByType<ResultManager>(),"rewardPanel").activeSelf,Is.False);
        yield return Load("Battle");Assert.That(Object.FindAnyObjectByType<BattleManager>().RewardBattleId,Is.Not.EqualTo(first));Assert.That(BattleRewardService.Complete(first,true,out _),Is.False);
    }
    [UnityTest] public IEnumerator VictorySaveFailureStaysInBattleAndRetryCommitsExactlyOnce()
    {
        yield return Load("Battle");var battle=Object.FindAnyObjectByType<BattleManager>();string id=battle.RewardBattleId;string before=JsonUtility.ToJson(PlayerSaveService.Instance.Capture());Directory.Delete(directory,true);File.WriteAllText(directory,"blocked");
        HP(battle.Context.Enemy,1);battle.UseSkill1();float end=Time.realtimeSinceStartup+15;while(Object.FindAnyObjectByType<BattleRewardRetryUI>()==null && Time.realtimeSinceStartup<end)yield return null;
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("Battle"));Assert.That(battle.CanChoose,Is.False);Assert.That(JsonUtility.ToJson(PlayerSaveService.Instance.Capture()),Is.EqualTo(before));
        File.Delete(directory);Directory.CreateDirectory(directory);Button("RetryRewardSaveButton").onClick.Invoke();yield return WaitResult();Assert.That(CurrencyService.Balance,Is.EqualTo(3100));Assert.That(BattleRewardService.Complete(id,true,out _),Is.True);Assert.That(CurrencyService.Balance,Is.EqualTo(3100));
    }
    [UnityTest] public IEnumerator InitialTicketSaveFailureBlocksInputsUntilRetryIsSaved()
    {
        Directory.Delete(directory,true);File.WriteAllText(directory,"blocked");yield return Load("Battle");var battle=Object.FindAnyObjectByType<BattleManager>();Assert.That(battle.CanChoose,Is.False);Assert.That(battle.RewardBattleId,Is.Null);Assert.That(Object.FindAnyObjectByType<BattleRewardRetryUI>(),Is.Not.Null);
        File.Delete(directory);Directory.CreateDirectory(directory);Button("RetryRewardSaveButton").onClick.Invoke();yield return null;Assert.That(battle.CanChoose,Is.True);Assert.That(battle.RewardBattleId,Does.StartWith("battle-"));Assert.That(Object.FindAnyObjectByType<BattleRewardRetryUI>(),Is.Null);
    }
    [UnityTest] public IEnumerator DynamicDropsAppearInInventoryAndCraftWithConfirmationAndCancel()
    {
        InventoryManager.AddText("Feu",2,out _);InventoryManager.AddText("er",1,out _);InventoryManager.AddText(new string('ü',128),1,out _);
        yield return Load("Inventory");yield return null;yield return null;Canvas.ForceUpdateCanvases();Assert.That(Object.FindObjectsByType<TMP_Text>().Any(t=>t.text=="Feu"),Is.True);
        Assert.That(Object.FindObjectsByType<TMP_Text>().Where(t=>t.name=="ItemNameText").All(t=>!t.isTextOverflowing),Is.True,"Long material rows grow instead of truncating the name.");
        yield return Load("Crafting");var crafting=Object.FindAnyObjectByType<CraftingUIManager>();Button("Material_"+DynamicMaterials.Id("Feu")).onClick.Invoke();Button("Material_"+DynamicMaterials.Id("er")).onClick.Invoke();
        crafting.CraftSelectedMaterials();Assert.That(Field<TMP_Text>(crafting,"dialogDetails").text,Does.Contain("200 Coins"));crafting.CancelDialog();Assert.That(CurrencyService.Balance,Is.EqualTo(3000));
        crafting.CraftSelectedMaterials();crafting.AcceptDialog();Assert.That(CurrencyService.Balance,Is.EqualTo(2800));Assert.That(CharacterInstances.Count("feuer"),Is.EqualTo(1));Assert.That(InventoryManager.GetItemCount(DynamicMaterials.Id("Feu")),Is.EqualTo(1));
    }
    [UnityTest] public IEnumerator ResultRendersAllSuccessAndManyLongMaterialsWithoutCoveringExistingButtons()
    {
        Assert.That(BattleRewardService.Begin("feuer",out string id,out _),Is.True);BattleRewardService.Complete(id,true,out _,()=>0);yield return Load("Result");Assert.That(Object.FindAnyObjectByType<ResultManager>().RewardSummary,Does.Contain("Character obtained!").And.Contain("Feuer ×1").And.Not.Contain("Materials"));
        Assert.That(PlayerStateTransactions.TryChange(data=>{var receipt=data.battleRewards.latest;receipt.characterId=receipt.characterInstanceId=null;for(int n=0;n<28;n++)receipt.materials.Add(DynamicMaterials.Entry(new string((char)('a'+n%26),110)+(n<26?"a":"b")));return true;},out _),Is.True);
        yield return Load("Result");var scroll=GameObject.Find("BattleRewardScroll").GetComponent<ScrollRect>();
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1600,1200),new Vector2Int(2560,1080)})
        {
            Screen.SetResolution(size.x,size.y,false);yield return null;yield return null;Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);Canvas.ForceUpdateCanvases();Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));scroll.verticalNormalizedPosition=0;yield return null;
            foreach(var button in Object.FindObjectsByType<Button>())
            {
                var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);Assert.That(corners.All(p=>p.x>=-1 && p.x<=Screen.width+1 && p.y>=-1 && p.y<=Screen.height+1),Is.True,size+" "+button.name);
                if(button.name!="SettingsOpen")Assert.That(corners.All(p=>p.y<((RectTransform)scroll.transform).position.y),Is.True,"Result navigation buttons stay below rewards");
            }
        }
    }
}

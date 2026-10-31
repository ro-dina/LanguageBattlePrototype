using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public class CurrencyTests
{
    private string directory;
    [SetUp] public void Setup()
    {
        if (PlayerSaveService.Instance != null) Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        GameDatabase.EnsureAvailable();directory=Path.Combine(Path.GetTempPath(),"LanguageBattleCoinsTests",Guid.NewGuid().ToString("N"));
        PlayerSaveService.Initialize(directory);
    }
    [TearDown] public void Cleanup()
    {
        if (PlayerSaveService.Instance != null) Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        if (GameDatabase.Instance != null) Object.DestroyImmediate(GameDatabase.Instance.gameObject);
        if (Directory.Exists(directory)) Directory.Delete(directory,true); else if(File.Exists(directory)) File.Delete(directory);
    }
    private string State()=>JsonUtility.ToJson(PlayerSaveService.Instance.Capture());
    private CraftingElement[] Letters(string word)
    {
        var items=JsonUtility.FromJson<GachaItemDatabase>(Resources.Load<TextAsset>("Data/gacha_items").text).items;
        return word.Select(c=>{string id=items.Single(i=>i.itemType=="letter" && i.displayName==c.ToString()).id;InventoryManager.AddItem(id,1);return CraftingElement.Material(id);}).ToArray();
    }
    [Test] public void DefaultsAndCostsComeFromSettingsAndAmountsAreFormatted()
    {
        Assert.That(CurrencyService.Balance,Is.EqualTo(3000));Assert.That(EconomySettings.Current.gachaSingleCost,Is.EqualTo(100));
        Assert.That(EconomySettings.Current.gachaTenCost,Is.EqualTo(1000));Assert.That(EconomySettings.Current.craftingCost,Is.EqualTo(200));
        Assert.That(EconomySettings.Price(12500),Is.EqualTo("12,500 Coins"));Assert.That(EconomySettings.Current.developmentGrants,Is.EqualTo(new long[]{100,1000,10000}));
    }
    [Test] public void AddSpendAndOneUseCommandNotifyOnlyAfterSavedStateIsCommitted()
    {
        int changes=0;bool reentered=true;var command=new PlayerOperation();
        Action<long> listener=balance=>{
            changes++;Assert.That(SaveDataValidator.TryDecode(File.ReadAllText(PlayerSaveService.Instance.Files.MainPath),out var saved,out _),Is.True);
            Assert.That(saved.wallet.balance,Is.EqualTo(balance));reentered=CurrencyService.TryAdd(1,out _);
        };
        CurrencyService.BalanceChanged+=listener;
        try
        {
            Assert.That(CurrencyService.TryAdd(100,out _,command),Is.True);Assert.That(CurrencyService.Balance,Is.EqualTo(3100));
            Assert.That(CurrencyService.TryAdd(100,out _,command),Is.False);Assert.That(CurrencyService.TrySpend(1100,out _),Is.True);
            Assert.That(CurrencyService.Balance,Is.EqualTo(2000));Assert.That(changes,Is.EqualTo(2));Assert.That(reentered,Is.False);
        }
        finally{CurrencyService.BalanceChanged-=listener;}
        PlayerSaveService.Instance.Reload();Assert.That(CurrencyService.Balance,Is.EqualTo(2000));
    }
    [Test] public void ZeroBalanceInsufficiencyNegativeAmountsAndOverflowAreProtected()
    {
        Assert.That(CurrencyService.TrySpend(3000,out _),Is.True);Assert.That(CurrencyService.Balance,Is.Zero);
        Assert.That(CurrencyService.TrySpend(1,out _),Is.False);Assert.That(CurrencyService.TryAdd(-1,out _),Is.False);Assert.That(CurrencyService.TrySpend(-1,out _),Is.False);
        Assert.That(CurrencyService.TryAdd(long.MaxValue,out _),Is.True);Assert.That(CurrencyService.Balance,Is.EqualTo(long.MaxValue));
        Assert.That(CurrencyService.TryAdd(1,out _),Is.False);PlayerSaveService.Instance.Reload();Assert.That(CurrencyService.Balance,Is.EqualTo(long.MaxValue));
        Assert.That(CurrencyService.TrySpend(long.MaxValue,out _),Is.True);Assert.That(CurrencyService.CanSpend(0),Is.True);
        PlayerSaveService.Instance.Reload();Assert.That(CurrencyService.Balance,Is.Zero,"A saved zero balance must not receive initial funds again.");
    }
    [Test] public void CurrencySaveFailureLeavesAllStateUntouchedAndFailedCommandCanRetry()
    {
        string before=State();var operation=new PlayerOperation();Directory.Delete(directory,true);File.WriteAllText(directory,"blocked");
        Assert.That(CurrencyService.TryAdd(100,out string error,operation),Is.False);Assert.That(error,Does.Contain("failed"));Assert.That(State(),Is.EqualTo(before));
        File.Delete(directory);Directory.CreateDirectory(directory);
        Assert.That(CurrencyService.TryAdd(100,out _,operation),Is.True);Assert.That(CurrencyService.TryAdd(100,out _,operation),Is.False);Assert.That(CurrencyService.Balance,Is.EqualTo(3100));
    }
    [Test] public void FutureRewardBatchCombinesCoinsMaterialsAndCharactersInOneCommit()
    {
        var operation=new PlayerOperation();int notifications=0;Action count=()=>notifications++;PlayerStateChanges.Changed+=count;
        try{Assert.That(RewardGrantService.Grant(new[]{new InventoryEntry("letter_a",2),new InventoryEntry("word_wasser",1)},100,out _,operation),Is.True);}
        finally{PlayerStateChanges.Changed-=count;}
        Assert.That(notifications,Is.EqualTo(1));Assert.That(CurrencyService.Balance,Is.EqualTo(3100));Assert.That(InventoryManager.GetItemCount("letter_a"),Is.EqualTo(2));Assert.That(CharacterInstances.Count("wasser"),Is.EqualTo(1));
        string before=State();Assert.That(RewardGrantService.Grant(new[]{new InventoryEntry("word_wasser",1),new InventoryEntry("missing",1)},100,out _),Is.False);Assert.That(State(),Is.EqualTo(before));
        Assert.That(RewardGrantService.Grant(null,100,out _,operation),Is.False);Assert.That(State(),Is.EqualTo(before));
    }
    [Test] public void CraftingLettersSpendsTwoHundredAndInvalidOrUnfundedInputsSpendNothing()
    {
        var letters=Letters("feuer");var operation=new PlayerOperation();Assert.That(CraftingManager.TryCraft(letters,out _,out _,operation),Is.True);Assert.That(CurrencyService.Balance,Is.EqualTo(2800));
        string before=State();Assert.That(CraftingManager.TryCraft(letters,out _,out _,operation),Is.False);Assert.That(State(),Is.EqualTo(before));
        var next=Letters("nacht");CurrencyService.TrySpend(CurrencyService.Balance,out _);before=State();
        Assert.That(CraftingManager.CanCraft(next),Is.False);Assert.That(CraftingManager.TryCraft(next,out _,out _),Is.False);Assert.That(State(),Is.EqualTo(before));
    }
    [Test] public void MixedCraftSaveFailureDoesNotSpendCoinsMaterialsOrIndividuals()
    {
        var water=CharacterInstances.Create("wasser");var letters=Letters("pistole");var selection=new[]{CraftingElement.Character(water.instanceId)}.Concat(letters).ToArray();
        string before=State();Directory.Delete(directory,true);File.WriteAllText(directory,"blocked");
        Assert.That(CraftingManager.TryCraft(selection,out _,out _),Is.False);Assert.That(State(),Is.EqualTo(before));
    }
    [TestCase(1)][TestCase(2)][TestCase(3)] public void OldVersionsReceiveInitialCoinsExactlyOnceAndKeepAllOldSections(int version)
    {
        var data=NewGameDefaults.Create();data.version=version;data.wallet=null;data.inventory.items.Add(new InventoryEntry("letter_a",7));
        data.stageProgress.clearedStageIds.Add("old-stage");
        if(version<3)data.inventory.items.Add(new InventoryEntry("word_wasser",2));
        else data.convertedWordItems.Add(new WordItemConversion{itemId="word_wasser",characterId="wasser",count=2});
        string json;
        if(version==1){var old=new LegacySaveData{version=1,inventory=data.inventory,characterEquipment=data.characterEquipment,stageProgress=data.stageProgress,ownedCharacters=new OwnedCharactersState()};old.ownedCharacters.characterIds.Add("nacht");json=JsonUtility.ToJson(old);}
        else json=JsonUtility.ToJson(data);
        File.WriteAllText(PlayerSaveService.Instance.Files.MainPath,json);PlayerSaveService.Instance.Reload();
        Assert.That(PlayerSaveService.Instance.LoadStatus,Is.EqualTo(SaveLoadStatus.Migrated));Assert.That(CurrencyService.Balance,Is.EqualTo(3000));
        Assert.That(InventoryManager.GetItemCount("letter_a"),Is.EqualTo(7));Assert.That(PlayerSaveService.Instance.Capture().convertedWordItems.Single().count,Is.EqualTo(2));
        Assert.That(File.ReadAllText(PlayerSaveService.Instance.Files.MainPath+".v"+version+".bak"),Is.EqualTo(json));
        if(version>1)Assert.That(CharacterInstances.Owns(data.characterInstances.instances[0].instanceId),Is.True);
        CurrencyService.TrySpend(100,out _);var ids=CharacterInstances.GetAll().Select(i=>i.instanceId).ToArray();
        for(int n=0;n<3;n++){PlayerSaveService.Instance.Reload();Assert.That(CurrencyService.Balance,Is.EqualTo(2900));Assert.That(CharacterInstances.GetAll().Select(i=>i.instanceId),Is.EqualTo(ids));}
    }
    [TestCase("null")][TestCase("{}")][TestCase("{\"balance\":-1}")][TestCase("{\"balance\":1.5}")][TestCase("{\"balance\":9223372036854775808}")]
    public void InvalidWalletJsonIsRejectedInsteadOfResettingCoins(string wallet)
    {
        string json=JsonUtility.ToJson(NewGameDefaults.Create());json=System.Text.RegularExpressions.Regex.Replace(json,"\\\"wallet\\\":\\{[^{}]*\\}","\"wallet\":"+wallet);
        Assert.That(SaveDataValidator.TryDecode(json,out _,out _),Is.False,wallet);
    }
    [Test] public void EditorDevelopmentGateAllowsTheSharedGrantAction()
    {int calls=0;Assert.That(DevelopmentCurrencyAccess.Enabled,Is.True);Assert.That(DevelopmentCurrencyAccess.Run(()=>calls++),Is.True);Assert.That(calls,Is.EqualTo(1));}
    [Test] public void CorruptCurrentSaveRecoversVersionThreeBackupAndDoesNotRegrantOnRestart()
    {
        var legacy=NewGameDefaults.Create();legacy.version=3;legacy.wallet=null;
        string main=PlayerSaveService.Instance.Files.MainPath;File.WriteAllText(main,"damaged");File.WriteAllText(main+".v3.bak",JsonUtility.ToJson(legacy));
        var store=new SaveFileStore(directory);Assert.That(store.Load(out var restored),Is.EqualTo(SaveLoadStatus.Recovered));Assert.That(restored.wallet.balance,Is.EqualTo(3000));
        restored.wallet.balance=2700;Assert.That(store.Save(restored),Is.True);
        Assert.That(File.ReadAllText(main),Is.EqualTo("damaged"));
        Assert.That(new SaveFileStore(directory).Load(out var restarted),Is.EqualTo(SaveLoadStatus.Recovered));Assert.That(restarted.wallet.balance,Is.EqualTo(2700));
    }
    [Test] public void ObserverFailureCannotTurnASavedOperationIntoARetryableCommand()
    {
        Action brokenState=()=>throw new InvalidOperationException("Coins state observer");
        Action<long> brokenBalance=_=>throw new InvalidOperationException("Coins balance observer");
        PlayerStateChanges.Changed+=brokenState;CurrencyService.BalanceChanged+=brokenBalance;
        try
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,new System.Text.RegularExpressions.Regex("InvalidOperationException: Coins state observer"));
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,new System.Text.RegularExpressions.Regex("InvalidOperationException: Coins balance observer"));
            var operation=new PlayerOperation();Assert.That(CurrencyService.TryAdd(100,out _,operation),Is.True);
            Assert.That(CurrencyService.TryAdd(100,out _,operation),Is.False);Assert.That(CurrencyService.Balance,Is.EqualTo(3100));
        }
        finally{PlayerStateChanges.Changed-=brokenState;CurrencyService.BalanceChanged-=brokenBalance;}
        PlayerSaveService.Instance.Reload();Assert.That(CurrencyService.Balance,Is.EqualTo(3100));
    }
}

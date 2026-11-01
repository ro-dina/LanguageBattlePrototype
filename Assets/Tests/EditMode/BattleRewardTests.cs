using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public class BattleRewardTests
{
    private string directory;
    [SetUp] public void Setup()
    {
        if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        GameDatabase.EnsureAvailable();directory=Path.Combine(Path.GetTempPath(),"LanguageBattleRewardTests",Guid.NewGuid().ToString("N"));PlayerSaveService.Initialize(directory);
    }
    [TearDown] public void Cleanup()
    {
        if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        if(GameDatabase.Instance!=null)Object.DestroyImmediate(GameDatabase.Instance.gameObject);
        if(Directory.Exists(directory))Directory.Delete(directory,true);else if(File.Exists(directory))File.Delete(directory);
    }
    private string State()=>JsonUtility.ToJson(PlayerSaveService.Instance.Capture());
    private string Begin(string enemy="feuer"){Assert.That(BattleRewardService.Begin(enemy,out string id,out string error),Is.True,error);return id;}
    private static Func<double> Rolls(params double[] values){int n=0;return ()=>values[n++];}
    [Test] public void SettingsAndIndependentBoundarySamplesProduceSeparatedRuns()
    {
        Assert.That(BattleRewardSettings.Current.victoryCoins,Is.EqualTo(100));Assert.That(BattleRewardSettings.Current.letterDropChance,Is.EqualTo(.4d));
        int calls=0;var values=new[]{0d,.39d,.9d,.1d,.2d};
        var drop=BattleDropGenerator.Roll(GameDatabase.Instance.GetCharacter("feuer"),BattleRewardSettings.Current.letterDropChance,()=>values[calls++]);
        Assert.That(calls,Is.EqualTo(5));Assert.That(drop.Materials.Select(e=>e.text),Is.EqualTo(new[]{"Fe","er"}));Assert.That(drop.CharacterId,Is.Null);
        drop=BattleDropGenerator.Roll(GameDatabase.Instance.GetCharacter("feuer"),.4d,Rolls(.4d,0,0,0,.4d));Assert.That(drop.Materials.Single().text,Is.EqualTo("eue"));
        drop=BattleDropGenerator.Roll(GameDatabase.Instance.GetCharacter("feuer"),0,()=>0);Assert.That(drop.Materials,Is.Empty);
        drop=BattleDropGenerator.Roll(GameDatabase.Instance.GetCharacter("feuer"),1,()=>.999);Assert.That(drop.CharacterId,Is.EqualTo("feuer"));Assert.That(drop.Materials,Is.Empty);
    }
    [Test] public void UnicodeTextElementsAreNotSplitAndAllSuccessUsesEnemyId()
    {
        var synthetic=new CharacterData{id="exact-enemy-id",characterName="A\u0308rger"};int calls=0;
        var plan=BattleDropGenerator.Roll(synthetic,1,()=>{calls++;return 0;});Assert.That(calls,Is.EqualTo(5));Assert.That(plan.CharacterId,Is.EqualTo("exact-enemy-id"));
        synthetic.characterName="Fuß";plan=BattleDropGenerator.Roll(synthetic,.4d,Rolls(.9,0,0));Assert.That(plan.Materials.Single().text,Is.EqualTo("uß"));
        synthetic.characterName="eXe";plan=BattleDropGenerator.Roll(synthetic,.4d,Rolls(0,.9,0));Assert.That(plan.Materials.Single().text,Is.EqualTo("e"));Assert.That(plan.Materials.Single().count,Is.EqualTo(2));
    }
    [Test] public void DynamicMaterialsNormalizeCaseAndNfcButNeverTransliterate()
    {
        Assert.That(DynamicMaterials.Id("Feu"),Is.EqualTo(DynamicMaterials.Id("feu")));
        Assert.That(DynamicMaterials.Id("Ä"),Is.EqualTo(DynamicMaterials.Id("A\u0308")));
        Assert.That(DynamicMaterials.Id("ä"),Is.Not.EqualTo(DynamicMaterials.Id("ae")));Assert.That(DynamicMaterials.Id("ß"),Is.Not.EqualTo(DynamicMaterials.Id("ss")));
        Assert.That(InventoryManager.AddText("Feu",2,out _),Is.True);Assert.That(InventoryManager.AddText("feu",3,out _),Is.True);InventoryManager.AddItem("letter_f",1);
        string id=DynamicMaterials.Id("Feu");Assert.That(InventoryManager.GetItemCount(id),Is.EqualTo(5));Assert.That(ItemDisplayNames.Get(id),Is.EqualTo("Feu"));
        PlayerSaveService.Instance.Reload();Assert.That(InventoryManager.GetItemCount(id),Is.EqualTo(5));Assert.That(ItemDisplayNames.Get(id),Is.EqualTo("Feu"));Assert.That(InventoryManager.GetItemCount("letter_f"),Is.EqualTo(1));
        Assert.That(InventoryManager.TryConsumeItems(new[]{new InventoryEntry(id,2)}),Is.True);Assert.That(InventoryManager.GetItemCount(id),Is.EqualTo(3));
    }
    [TestCase("")][TestCase("x y")][TestCase("<x>")][TestCase("x\n")][TestCase("\uD800")]
    public void InvalidStringsAreRefusedWithoutChangingAnyState(string text)
    {string before=State();Assert.That(InventoryManager.AddText(text,1,out _),Is.False);Assert.That(State(),Is.EqualTo(before));}
    [Test] public void LongOrForgedIdsAndMismatchedDisplayMetadataAreRejected()
    {
        Assert.That(InventoryManager.AddText(new string('a',129),1,out _),Is.False);
        foreach(string id in new[]{"material-text-v1:@@@","material-text-v1:QQ==","material-text-v1:////","material-text-v1:"})Assert.That(GameContentIds.IsItem(id),Is.False,id);
        var data=NewGameDefaults.Create();data.inventory.items.Add(new InventoryEntry(DynamicMaterials.Id("feu"),1){text="Wasser"});Assert.That(SaveDataValidator.Validate(data,out _),Is.False);
        Assert.That(RewardGrantService.Grant(new[]{new InventoryEntry(DynamicMaterials.Id("feu"),1)},out _),Is.False);
    }
    [Test] public void ConsecutiveDropsCommitCurrencyAndMaterialsTogetherAndRepeatIsHarmless()
    {
        string id=Begin();int changed=0;Action listener=()=>{changed++;Assert.That(SaveDataValidator.TryDecode(File.ReadAllText(PlayerSaveService.Instance.Files.MainPath),out var saved,out _),Is.True);Assert.That(saved.battleRewards.latest.status,Is.EqualTo(BattleRewardStatus.Victory));};
        PlayerStateChanges.Changed+=listener;
        try{Assert.That(BattleRewardService.Complete(id,true,out _,Rolls(0,0,0,.9,0)),Is.True);}finally{PlayerStateChanges.Changed-=listener;}
        Assert.That(changed,Is.EqualTo(1));Assert.That(CurrencyService.Balance,Is.EqualTo(3100));Assert.That(BattleRewardService.Latest.materials.Select(e=>e.text),Is.EqualTo(new[]{"Feu","r"}));
        Assert.That(InventoryManager.GetItemCount(DynamicMaterials.Id("Feu")),Is.EqualTo(1));string before=State();Assert.That(BattleRewardService.Complete(id,true,out _),Is.True);Assert.That(State(),Is.EqualTo(before));
    }
    [Test] public void AllFailuresGrantOnlyCoinsAndDefeatGrantsNothing()
    {
        string id=Begin();Assert.That(BattleRewardService.Complete(id,true,out _,()=>.9),Is.True);Assert.That(CurrencyService.Balance,Is.EqualTo(3100));Assert.That(InventoryManager.GetAllItems(),Is.Empty);
        id=Begin();long balance=CurrencyService.Balance;Assert.That(BattleRewardService.Complete(id,false,out _,()=>throw new Exception("must not roll")),Is.True);
        Assert.That(CurrencyService.Balance,Is.EqualTo(balance));Assert.That(BattleRewardService.Latest.status,Is.EqualTo(BattleRewardStatus.Defeat));Assert.That(ResultManager.FormatRewards(BattleRewardService.Latest),Is.Empty);
        Assert.That(BattleRewardService.Complete(id,true,out _),Is.False);
    }
    [Test] public void AllSuccessGrantsOneDistinctInitialLoadoutIndividualAndDiscoveryWithoutMaterial()
    {
        var old=CharacterInstances.Create("feuer");var gear=CharacterLoadoutStore.GetLoadout(old.instanceId);gear.adjectiveId="schwarz";CharacterLoadoutStore.Commit(gear);
        string id=Begin();Assert.That(BattleRewardService.Complete(id,true,out _,()=>0),Is.True);Assert.That(CharacterInstances.Count("feuer"),Is.EqualTo(2));Assert.That(CurrencyService.Balance,Is.EqualTo(3100));
        var receipt=BattleRewardService.Latest;Assert.That(receipt.characterId,Is.EqualTo("feuer"));Assert.That(receipt.characterInstanceId,Is.Not.EqualTo(old.instanceId));Assert.That(receipt.materials,Is.Empty);Assert.That(InventoryManager.GetAllItems(),Is.Empty);
        Assert.That(CharacterCollection.IsDiscovered("feuer"),Is.True);Assert.That(CharacterLoadoutStore.GetLoadout(receipt.characterInstanceId).adjectiveId,Is.EqualTo(GameDatabase.Instance.GetCharacter("feuer").equippedAdjectiveId ?? ""));Assert.That(CharacterLoadoutStore.GetLoadout(old.instanceId).adjectiveId,Is.EqualTo("schwarz"));
        Assert.That(CharacterInstances.Remove(receipt.characterInstanceId),Is.True);PlayerSaveService.Instance.Reload();Assert.That(BattleRewardService.Latest.characterInstanceId,Is.EqualTo(receipt.characterInstanceId));
    }
    [Test] public void APartialDropMatchingAnotherCharacterRemainsMaterial()
    {
        string id=Begin("wasserpistole");int n=0;Assert.That(BattleRewardService.Complete(id,true,out _,()=>n++<6?0:.9),Is.True);
        Assert.That(InventoryManager.GetItemCount(DynamicMaterials.Id("Wasser")),Is.EqualTo(1));Assert.That(CharacterInstances.Count("wasser"),Is.Zero);Assert.That(BattleRewardService.Latest.characterId,Is.Null.Or.Empty);
    }
    [TestCase(false)][TestCase(true)] public void SaveFailureKeepsEverythingAndRetryKeepsTheOriginalDropPlan(bool all)
    {
        string id=Begin();string before=State();Directory.Delete(directory,true);File.WriteAllText(directory,"blocked");int n=0;
        Assert.That(BattleRewardService.Complete(id,true,out _,()=>{n++;return all?0:(n==4?.9:0);}),Is.False);Assert.That(State(),Is.EqualTo(before));Assert.That(n,Is.EqualTo(5));
        Assert.That(BattleRewardService.Complete(id,false,out _),Is.False,"Save retry cannot change the outcome.");Assert.That(State(),Is.EqualTo(before));
        File.Delete(directory);Directory.CreateDirectory(directory);Assert.That(BattleRewardService.Complete(id,true,out _,()=>throw new Exception("must not reroll")),Is.True);Assert.That(CurrencyService.Balance,Is.EqualTo(3100));
        Assert.That(all?CharacterInstances.Count("feuer"):InventoryManager.GetItemCount(DynamicMaterials.Id("Feu")),Is.EqualTo(1));
    }
    [Test] public void RestartReplayAndOldTicketsAfterReplacementNeverGrantAgainAndStorageIsBounded()
    {
        string first=Begin();BattleRewardService.Complete(first,true,out _,()=>0);Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);PlayerSaveService.Initialize(directory);
        string before=State();Assert.That(BattleRewardService.Complete(first,true,out _),Is.True);Assert.That(State(),Is.EqualTo(before));
        string next=Begin();Assert.That(next,Is.Not.EqualTo(first));Assert.That(BattleRewardService.Complete(first,true,out _),Is.False);BattleRewardService.Complete(next,true,out _,()=>.9);Assert.That(CurrencyService.Balance,Is.EqualTo(3200));
        for(int n=0;n<25;n++)Begin();before=State();Assert.That(BattleRewardService.Complete(first,true,out _),Is.False);Assert.That(State(),Is.EqualTo(before));
        Assert.That(JsonUtility.ToJson(BattleRewardService.CaptureState()).Length,Is.LessThan(300));
        Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);PlayerSaveService.Initialize(directory);Assert.That(BattleRewardService.Complete(next,true,out _),Is.False);
    }
    [Test] public void DynamicFixedAndIndividualInputsComposeFreelyWithCostAndQuantitySafety()
    {
        InventoryManager.AddText("Feu",1,out _);InventoryManager.AddText("er",1,out _);
        var selection=new[]{CraftingElement.Material(DynamicMaterials.Id("Feu")),CraftingElement.Material(DynamicMaterials.Id("er"))};Assert.That(CraftingManager.TryCraft(selection,out var created,out _),Is.True);Assert.That(created.characterId,Is.EqualTo("feuer"));Assert.That(CurrencyService.Balance,Is.EqualTo(2800));
        InventoryManager.AddText("Feu",1,out _);InventoryManager.AddItem("chunk_er",1);selection=new[]{CraftingElement.Material(DynamicMaterials.Id("Feu")),CraftingElement.Material("chunk_er")};Assert.That(CraftingManager.TryCraft(selection,out _,out _),Is.True);
        var water=CharacterInstances.Create("wasser");InventoryManager.AddText("pistole",1,out _);selection=new[]{CraftingElement.Character(water.instanceId),CraftingElement.Material(DynamicMaterials.Id("pistole"))};Assert.That(CraftingManager.TryCraft(selection,out created,out _),Is.True);Assert.That(created.characterId,Is.EqualTo("wasserpistole"));Assert.That(CharacterInstances.Owns(water.instanceId),Is.False);Assert.That(CurrencyService.Balance,Is.EqualTo(2400));
        InventoryManager.AddText("f",1,out _);InventoryManager.AddText("e",2,out _);InventoryManager.AddText("u",1,out _);InventoryManager.AddText("r",1,out _);
        selection="feuer".Select(c=>CraftingElement.Material(DynamicMaterials.Id(c.ToString()))).ToArray();Assert.That(CraftingManager.TryCraft(selection,out _,out _),Is.True);
        string before=State();Assert.That(CraftingManager.TryCraft(selection,out _,out _),Is.False);Assert.That(State(),Is.EqualTo(before));
    }
    [Test] public void DynamicCraftOrAddSaveFailureLeavesEverySectionUnchanged()
    {
        InventoryManager.AddText("Feu",1,out _);InventoryManager.AddText("er",1,out _);string before=State();Directory.Delete(directory,true);File.WriteAllText(directory,"blocked");
        Assert.That(CraftingManager.TryCraft(new[]{CraftingElement.Material(DynamicMaterials.Id("Feu")),CraftingElement.Material(DynamicMaterials.Id("er"))},out _,out _),Is.False);Assert.That(State(),Is.EqualTo(before));
        Assert.That(InventoryManager.AddText("eue",1,out _),Is.False);Assert.That(State(),Is.EqualTo(before));
    }
    [Test] public void VersionFourMigrationKeepsZeroCoinsIdsGearHistoryAndOriginalBackupBytes()
    {
        var old=PlayerSaveService.Instance.Capture();old.version=4;old.wallet.balance=0;old.inventory.items.Add(new InventoryEntry("letter_a",7));old.convertedWordItems.Add(new WordItemConversion{itemId="word_wasser",characterId="wasser",count=2});old.stageProgress.clearedStageIds.Add("stage-one");
        var gear=CharacterLoadoutStore.GetDefaultLoadout("nacht",old.characterInstances.instances[0].instanceId);gear.adjectiveId="schwarz";old.characterEquipment.characters.Add(gear);
        string source=JsonUtility.ToJson(old);File.WriteAllText(PlayerSaveService.Instance.Files.MainPath,source);PlayerSaveService.Instance.Reload();var data=PlayerSaveService.Instance.Capture();
        Assert.That(PlayerSaveService.Instance.LoadStatus,Is.EqualTo(SaveLoadStatus.Migrated));Assert.That(data.version,Is.EqualTo(6));Assert.That(data.wallet.balance,Is.Zero);Assert.That(BattleRewardService.Latest,Is.Null);
        Assert.That(JsonUtility.ToJson(data.characterInstances),Is.EqualTo(JsonUtility.ToJson(old.characterInstances)));Assert.That(JsonUtility.ToJson(data.characterEquipment),Is.EqualTo(JsonUtility.ToJson(old.characterEquipment)));Assert.That(data.convertedWordItems.Single().count,Is.EqualTo(2));Assert.That(data.inventory.items.Single().count,Is.EqualTo(7));Assert.That(data.stageProgress.clearedStageIds,Does.Contain("stage-one"));
        Assert.That(File.ReadAllText(PlayerSaveService.Instance.Files.MainPath+".v4.bak"),Is.EqualTo(source));PlayerSaveService.Instance.Reload();Assert.That(CurrencyService.Balance,Is.Zero);
    }
    [Test] public void CorruptSaveRecoversDynamicInventoryAndClaimedReceiptWithoutRegrant()
    {
        string id=Begin();BattleRewardService.Complete(id,true,out _,Rolls(0,0,0,.9,0));Assert.That(PlayerSaveService.Instance.SaveNow(),Is.True);string main=PlayerSaveService.Instance.Files.MainPath;File.WriteAllText(main,"broken");
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,new System.Text.RegularExpressions.Regex("Save could not be loaded"));PlayerSaveService.Instance.Reload();Assert.That(PlayerSaveService.Instance.LoadStatus,Is.EqualTo(SaveLoadStatus.Recovered));
        Assert.That(BattleRewardService.Complete(id,true,out _),Is.True);Assert.That(CurrencyService.Balance,Is.EqualTo(3100));Assert.That(InventoryManager.GetItemCount(DynamicMaterials.Id("Feu")),Is.EqualTo(1));Assert.That(File.ReadAllText(main),Is.EqualTo("broken"));
    }
    [Test] public void VersionFourProtectedBackupRecoveryKeepsBalanceAndMalformedWalletKeepsSource()
    {
        var legacy=NewGameDefaults.Create();legacy.version=4;legacy.wallet.balance=137;string source=JsonUtility.ToJson(legacy);string main=PlayerSaveService.Instance.Files.MainPath;
        File.WriteAllText(main,"broken");File.WriteAllText(main+".bak","broken");File.WriteAllText(main+".v4.bak",source);
        var files=new SaveFileStore(directory);Assert.That(files.Load(out var data),Is.EqualTo(SaveLoadStatus.Recovered));Assert.That(data.wallet.balance,Is.EqualTo(137));Assert.That(files.Save(data),Is.True);
        Assert.That(File.ReadAllText(main),Is.EqualTo("broken"));Assert.That(File.ReadAllText(main+".v4.bak"),Is.EqualTo(source));Assert.That(new SaveFileStore(directory).Load(out data),Is.EqualTo(SaveLoadStatus.Recovered));Assert.That(data.wallet.balance,Is.EqualTo(137));
        Directory.Delete(directory,true);Directory.CreateDirectory(directory);legacy.wallet.balance=-1;source=JsonUtility.ToJson(legacy);File.WriteAllText(main,source);files=new SaveFileStore(directory);
        Assert.That(files.Load(out data),Is.EqualTo(SaveLoadStatus.MigrationFailed));Assert.That(files.Save(data),Is.False);Assert.That(File.ReadAllText(main),Is.EqualTo(source));
    }
    [Test] public void MissingReceiptAndOversizeSaveAreRejectedWithoutReplacingExistingFile()
    {
        string before=File.ReadAllText(PlayerSaveService.Instance.Files.MainPath);var data=PlayerSaveService.Instance.Capture();data.battleRewards=null;Assert.That(PlayerSaveService.Instance.Files.Save(data),Is.False);
        data=PlayerSaveService.Instance.Capture();data.stageProgress.clearedStageIds.Add(new string('x',SaveFileStore.MaxBytes));Assert.That(PlayerSaveService.Instance.Files.Save(data),Is.False);Assert.That(File.ReadAllText(PlayerSaveService.Instance.Files.MainPath),Is.EqualTo(before));
    }
}

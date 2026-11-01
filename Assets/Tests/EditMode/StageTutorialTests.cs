using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
public class StageTutorialTests
{
    private string directory;
    [SetUp] public void Setup()
    {
        if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        GameDatabase.EnsureAvailable();StageSession.Clear();directory=Path.Combine(Path.GetTempPath(),"LanguageBattleStages",Guid.NewGuid().ToString("N"));PlayerSaveService.Initialize(directory);
    }
    [TearDown] public void Cleanup()
    {
        if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        if(GameDatabase.Instance!=null)Object.DestroyImmediate(GameDatabase.Instance.gameObject);StageSession.Clear();
        if(Directory.Exists(directory))Directory.Delete(directory,true);else if(File.Exists(directory))File.Delete(directory);
    }
    private string State()=>JsonUtility.ToJson(PlayerSaveService.Instance.Capture());
    private string Begin(string id,bool guided=false)
    {var stage=StageCatalog.Get(id);Assert.That(BattleRewardService.Begin(stage.enemyCharacterId,out string ticket,out string error,id,guided),Is.True,error);return ticket;}
    private void Win(string id,bool guided=false,bool finish=false)=>Assert.That(BattleRewardService.Complete(Begin(id,guided),true,out string error,()=>.9,finish),Is.True,error);
    [Test] public void InitialMasterAndLocksUseFiveOrdinaryNounsAndKeepFeuer()
    {
        Assert.That(StageCatalog.All.Length,Is.EqualTo(6));Assert.That(GameDatabase.Instance.GetAllCharacters().Length,Is.EqualTo(208));
        foreach(var stage in StageCatalog.All){Assert.That(stage.firstClearCoins,Is.EqualTo(300));Assert.That(stage.repeatClearCoins,Is.EqualTo(100));Assert.That(GameDatabase.Instance.CharacterWords.Find(GameDatabase.Instance.GetCharacter(stage.enemyCharacterId).characterName).id,Is.EqualTo(stage.enemyCharacterId));}
        Assert.That(StageProgress.IsUnlocked("stage_1"),Is.True);Assert.That(StageProgress.IsUnlocked("feuer_practice"),Is.True);
        foreach(int n in new[]{2,3,4,5})Assert.That(StageProgress.IsUnlocked("stage_"+n),Is.False);
        Assert.That(StageSession.Select("stage_2",false,out _),Is.False);Assert.That(BattleRewardService.Begin("adverb",out _,out _,"stage_2"),Is.False);
        GameManager.EnemyCharacterId="modalverb";Assert.That(StageSession.ValidateBattle(out _),Is.False);Assert.That(BattleRewardService.Begin("modalverb",out _,out _),Is.False);
        GameManager.EnemyCharacterId="feuer";Assert.That(StageSession.ValidateBattle(out _),Is.True);Assert.That(BattleRewardService.Begin("feuer",out string free,out _),Is.True);BattleRewardService.Complete(free,true,out _,()=>.9);Assert.That(CurrencyService.Balance,Is.EqualTo(3100));
    }
    [Test] public void TutorialSpeciesStayOrdinaryOwnedCharactersAndCanBeCraftedWithoutStageRules()
    {
        foreach(string id in new[]{"technik","adverb","adjektiv","modalverb","zukunft"})
        {
            var original=GameDatabase.Instance.GetCharacter(id);var owned=CharacterInstances.Create(id);var character=CharacterLoadoutStore.GetCharacterForBattle(owned.instanceId);
            Assert.That(character.hp,Is.EqualTo(original.hp));Assert.That(JsonUtility.ToJson(character),Does.Not.Contain("guided_passive").And.Not.Contain("tutorialId"));
            var action=new BattleAction(BattleSide.Player,GameDatabase.Instance.GetSkill(character.skillIds[0]),modal:BattleModal.Muessen);
            Assert.That(new BattleContext(character,GameDatabase.Instance.GetCharacter("feuer")).Resolve(action).CanUse,Is.True);
            Assert.That(InventoryManager.AddText(original.characterName,1,out _),Is.True);Assert.That(CraftingManager.TryCraft(new[]{CraftingElement.Material(DynamicMaterials.Id(original.characterName))},out var created,out _),Is.True);Assert.That(created.characterId,Is.EqualTo(id));
        }
    }
    [Test] public void CyclicOrUnknownPrerequisitesAndInvalidCostsAreDetectedAndMastersAreCopies()
    {
        var data=StageCatalog.All;data[0].prerequisiteStageIds=new[]{"stage_2"};Assert.That(StageCatalog.Validate(data,out _),Is.False);
        data=StageCatalog.All;data[0].prerequisiteStageIds=new[]{"missing"};Assert.That(StageCatalog.Validate(data,out _),Is.False);
        data=StageCatalog.All;data[0].firstClearCoins=-1;Assert.That(StageCatalog.Validate(data,out _),Is.False);Assert.That(StageCatalog.Get("stage_1").firstClearCoins,Is.EqualTo(300));
    }
    [Test] public void FirstClearThenRetryUpdateCountUnlocksAndReceiptOnceEvenAfterRestart()
    {
        string id=Begin("stage_1");Assert.That(BattleRewardService.Complete(id,true,out _,()=>.9),Is.True);Assert.That(CurrencyService.Balance,Is.EqualTo(3400));Assert.That(StageProgress.ClearCount("stage_1"),Is.EqualTo(1));Assert.That(StageProgress.IsUnlocked("stage_2"),Is.True);
        Assert.That(BattleRewardService.Latest.firstClearCoins,Is.EqualTo(300));Assert.That(ResultManager.FormatRewards(BattleRewardService.Latest),Does.Contain("Coins +400").And.Contain("First clear: +300"));
        string before=State();Assert.That(BattleRewardService.Complete(id,true,out _),Is.True);Assert.That(State(),Is.EqualTo(before));Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);PlayerSaveService.Initialize(directory);Assert.That(BattleRewardService.Complete(id,true,out _),Is.True);Assert.That(State(),Is.EqualTo(before));
        Win("stage_1");Assert.That(CurrencyService.Balance,Is.EqualTo(3500));Assert.That(StageProgress.ClearCount("stage_1"),Is.EqualTo(2));Assert.That(BattleRewardService.Latest.firstClearCoins,Is.Zero);
    }
    [Test] public void AllFiveStagesUnlockInOrderAndEachCanBeRetried()
    {
        for(int n=1;n<=5;n++){Assert.That(StageProgress.IsUnlocked("stage_"+n),Is.True);Win("stage_"+n);Assert.That(StageProgress.IsCleared("stage_"+n),Is.True);}
        Assert.That(CurrencyService.Balance,Is.EqualTo(5000));Win("stage_5");Assert.That(CurrencyService.Balance,Is.EqualTo(5100));Assert.That(StageProgress.ClearCount("stage_5"),Is.EqualTo(2));
    }
    [Test] public void DefeatDoesNotClearUnlockOrGrantAnything()
    {
        string id=Begin("stage_1");string before=JsonUtility.ToJson(StageProgress.CaptureState());Assert.That(BattleRewardService.Complete(id,false,out _),Is.True);
        Assert.That(JsonUtility.ToJson(StageProgress.CaptureState()),Is.EqualTo(before));Assert.That(CurrencyService.Balance,Is.EqualTo(3000));Assert.That(StageProgress.IsUnlocked("stage_2"),Is.False);
    }
    [TestCase(false)][TestCase(true)] public void FailedSaveKeepsFirstClearRewardsDropsAndTutorialCompletionTogether(bool character)
    {
        TutorialPreferences.Choose("technik_basics",TutorialChoice.Take,out _);string id=Begin("stage_1",true);string before=State();Directory.Delete(directory,true);File.WriteAllText(directory,"blocked");
        int rolls=0;Assert.That(BattleRewardService.Complete(id,true,out _,()=>character?0:(++rolls==4?.9:0),true),Is.False);Assert.That(State(),Is.EqualTo(before));Assert.That(StageProgress.IsUnlocked("stage_2"),Is.False);
        File.Delete(directory);Directory.CreateDirectory(directory);int changes=0;Action changed=()=>changes++;PlayerStateChanges.Changed+=changed;
        try{Assert.That(BattleRewardService.Complete(id,true,out _,()=>throw new Exception("reroll"),true),Is.True);}finally{PlayerStateChanges.Changed-=changed;}
        Assert.That(changes,Is.EqualTo(1));Assert.That(CurrencyService.Balance,Is.EqualTo(3400));Assert.That(TutorialPreferences.Get("technik_basics").completed,Is.True);Assert.That(StageProgress.ClearCount("stage_1"),Is.EqualTo(1));Assert.That(StageProgress.IsUnlocked("stage_2"),Is.True);
        if(character){Assert.That(CharacterInstances.Count("technik"),Is.EqualTo(1));Assert.That(BattleRewardService.Latest.materials,Is.Empty);}
        else Assert.That(BattleRewardService.Latest.materials.Count,Is.EqualTo(2));
    }
    [TestCase(TutorialChoice.Take)][TestCase(TutorialChoice.SkipThis)][TestCase(TutorialChoice.SkipAll)]
    public void TutorialChoiceDoesNotReduceRewardsAndCompletionIsSeparate(TutorialChoice choice)
    {
        Assert.That(TutorialPreferences.NeedsChoice("technik_basics"),Is.True);Assert.That(TutorialPreferences.Choose("technik_basics",choice,out _),Is.True);
        Assert.That(TutorialPreferences.Get("technik_basics").individuallySkipped,Is.EqualTo(choice==TutorialChoice.SkipThis));
        Win("stage_1",choice==TutorialChoice.Take,choice==TutorialChoice.Take);Assert.That(CurrencyService.Balance,Is.EqualTo(3400));Assert.That(StageProgress.IsUnlocked("stage_2"),Is.True);
        Assert.That(TutorialPreferences.Get("technik_basics").completed,Is.EqualTo(choice==TutorialChoice.Take));
        Assert.That(TutorialPreferences.NeedsChoice("adverb_basics"),Is.EqualTo(choice!=TutorialChoice.SkipAll));
    }
    [Test] public void GlobalSkipPersistsCanBeDisabledAndExplicitReviewOverridesItWithoutFirstReward()
    {
        TutorialPreferences.Choose("technik_basics",TutorialChoice.SkipAll,out _);Win("stage_1");PlayerSaveService.Instance.Reload();Assert.That(TutorialPreferences.SkipAll,Is.True);Assert.That(TutorialPreferences.DefaultGuided("technik_basics"),Is.False);
        Assert.That(StageSession.Select("stage_1",true,out _),Is.True);Assert.That(StageSession.Guided,Is.True);TutorialPreferences.Choose("technik_basics",TutorialChoice.Take,out _);Win("stage_1",true,true);Assert.That(CurrencyService.Balance,Is.EqualTo(3500));Assert.That(TutorialPreferences.SkipAll,Is.True);
        Assert.That(TutorialPreferences.SetSkipAll(false,out _),Is.True);PlayerSaveService.Instance.Reload();Assert.That(TutorialPreferences.SkipAll,Is.False);Assert.That(TutorialPreferences.NeedsChoice("adverb_basics"),Is.True);
    }
    [Test] public void UnimplementedTutorialsHaveChoicesButCannotBeMarkedCompleted()
    {
        Win("stage_1");TutorialPreferences.Choose("adverb_basics",TutorialChoice.Take,out _);Assert.That(StageSession.Select("stage_2",true,out _),Is.True);Assert.That(StageSession.Guided,Is.False);
        Win("stage_2",true,true);Assert.That(TutorialPreferences.Get("adverb_basics").completed,Is.False);
        var invalid=PlayerSaveService.Instance.Capture();invalid.tutorials.records.Single(t=>t.tutorialId=="adverb_basics").completed=true;Assert.That(SaveDataValidator.Validate(invalid,out _),Is.False);
    }
    [TestCase(TutorialStepType.WaitForAdverbSelection)][TestCase(TutorialStepType.WaitForEquipmentChange)][TestCase(TutorialStepType.WaitForModalVerbSelection)][TestCase(TutorialStepType.WaitForFutureSelection)]
    public void UnsupportedStepsAreExplicitlyRejected(TutorialStepType type)
    {
        var definition=new TutorialDefinition{tutorialId="future",isImplemented=true,steps=new[]{new TutorialStep{type=type.ToString()},new TutorialStep{type="Complete"}}};Assert.That(TutorialCatalog.Supported(definition,out string error),Is.False);Assert.That(error,Does.Contain("Unsupported"));
        Assert.That(TutorialCatalog.Supported(TutorialCatalog.Get("technik_basics"),out _),Is.True);
    }
    [Test] public void VersionFiveMigrationPreservesExistingStateAndReceiptAndNeverRegrantsLegacyFirstClear()
    {
        InventoryManager.AddText("Är",2,out _);var owned=CharacterInstances.Create("feuer");var gear=CharacterLoadoutStore.GetLoadout(owned.instanceId);gear.adjectiveId="schwarz";CharacterLoadoutStore.Commit(gear);
        BattleRewardService.Begin("feuer",out string ticket,out _);BattleRewardService.Complete(ticket,true,out _,()=>.9);CurrencyService.TrySpend(CurrencyService.Balance,out _);
        var old=PlayerSaveService.Instance.Capture();old.version=5;old.stageProgress.records.Clear();old.stageProgress.clearedStageIds.Add("stage_1");old.stageProgress.unlockedStageIds.Add("legacy-stage");old.convertedWordItems.Add(new WordItemConversion{itemId="word_wasser",characterId="wasser",count=2});string source=JsonUtility.ToJson(old);
        File.WriteAllText(PlayerSaveService.Instance.Files.MainPath,source);PlayerSaveService.Instance.Reload();var after=PlayerSaveService.Instance.Capture();
        Assert.That(PlayerSaveService.Instance.LoadStatus,Is.EqualTo(SaveLoadStatus.Migrated));Assert.That(after.version,Is.EqualTo(6));Assert.That(after.wallet.balance,Is.Zero);Assert.That(after.stageProgress.unlockedStageIds,Does.Contain("legacy-stage"));Assert.That(StageProgress.IsUnlocked("stage_2"),Is.True);
        foreach(var pair in new[]{(JsonUtility.ToJson(after.characterInstances),JsonUtility.ToJson(old.characterInstances)),(JsonUtility.ToJson(after.characterEquipment),JsonUtility.ToJson(old.characterEquipment)),(JsonUtility.ToJson(after.inventory),JsonUtility.ToJson(old.inventory)),(JsonUtility.ToJson(after.battleRewards),JsonUtility.ToJson(old.battleRewards)),(JsonUtility.ToJson(after.convertedWordItems),JsonUtility.ToJson(old.convertedWordItems))})Assert.That(pair.Item1,Is.EqualTo(pair.Item2));
        Assert.That(File.ReadAllText(PlayerSaveService.Instance.Files.MainPath+".v5.bak"),Is.EqualTo(source));Assert.That(BattleRewardService.Complete(ticket,true,out _),Is.True);Assert.That(CurrencyService.Balance,Is.Zero);
        Win("stage_1");Assert.That(CurrencyService.Balance,Is.EqualTo(100));Assert.That(StageProgress.ClearCount("stage_1"),Is.EqualTo(2));Assert.That(after.tutorials.records,Is.Empty);Assert.That(after.convertedWordItems.Single().count,Is.EqualTo(2));
    }
    [Test] public void VersionFiveProtectedRecoveryKeepsSkipDefaultsAndCoinsAndOriginalBytes()
    {
        var old=NewGameDefaults.Create();old.version=5;old.wallet.balance=137;string source=JsonUtility.ToJson(old);string main=PlayerSaveService.Instance.Files.MainPath;
        File.WriteAllText(main,"broken");File.WriteAllText(main+".bak","broken");File.WriteAllText(main+".v5.bak",source);var files=new SaveFileStore(directory);
        Assert.That(files.Load(out var recovered),Is.EqualTo(SaveLoadStatus.Recovered));Assert.That(recovered.wallet.balance,Is.EqualTo(137));Assert.That(recovered.tutorials.skipAll,Is.False);Assert.That(files.Save(recovered),Is.True);Assert.That(File.ReadAllText(main),Is.EqualTo("broken"));Assert.That(File.ReadAllText(main+".v5.bak"),Is.EqualTo(source));
    }
    [Test] public void InvalidStageCountersOrTutorialSectionsRejectSaveWithoutChangingOriginal()
    {
        var data=PlayerSaveService.Instance.Capture();data.stageProgress.records.Add(new StageClearRecord{stageId="stage_1",clearCount=-1,firstClearRewardClaimed=true,lastClearResult="Victory"});Assert.That(SaveDataValidator.Validate(data,out _),Is.False);
        data=PlayerSaveService.Instance.Capture();data.tutorials=null;Assert.That(SaveDataValidator.Validate(data,out _),Is.False);
        data=PlayerSaveService.Instance.Capture();data.tutorials.records.Add(new TutorialRecord{tutorialId="technik_basics",lastChoice=(TutorialChoice)99});Assert.That(SaveDataValidator.Validate(data,out _),Is.False);
    }
}

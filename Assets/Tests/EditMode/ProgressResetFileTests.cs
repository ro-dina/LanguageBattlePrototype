using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
public class ProgressResetFileTests
{
    private string directory;
    private SaveFileStore files;
    private static bool Reset(SaveFileStore store,SaveData data)=>(bool)typeof(SaveFileStore).GetMethod("CommitReset",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(store,new object[]{data});
    [SetUp] public void Setup(){GameDatabase.EnsureAvailable();directory=Path.Combine(Path.GetTempPath(),"LanguageBattleResetFiles",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);files=new SaveFileStore(directory);}
    [TearDown] public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);if(PlayerSaveService.Instance!=null)Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);}
    private SaveData OldProgress()
    {
        var old=NewGameDefaults.Create();old.wallet.balance=12345;old.inventory.items.Add(new InventoryEntry("letter_a",41));old.inventory.items.Add(DynamicMaterials.Entry("Wass",4));
        var character=new CharacterInstance{instanceId=CharacterInstances.NewId(),characterId="feuer"};old.characterInstances.instances.Add(character);old.characterCollection.discoveredCharacterIds.Add("feuer");
        var loadout=CharacterLoadoutStore.GetDefaultLoadout("feuer",character.instanceId);loadout.adjectiveId="schnell";old.characterEquipment.characters.Add(loadout);
        old.tutorials.skipAll=true;old.tutorials.records.Add(new TutorialRecord{tutorialId="technik_basics",completed=true,lastChoice=TutorialChoice.SkipAll});
        old.stageProgress.clearedStageIds.Add("stage_1");old.stageProgress.unlockedStageIds.Add("stage_2");old.stageProgress.records.Add(new StageClearRecord{stageId="stage_1",clearCount=5,firstClearRewardClaimed=true,lastClearResult="Victory"});
        old.battleRewards.latest=new BattleRewardRecord{battleId="battle-"+Guid.NewGuid().ToString("N"),enemyCharacterId="feuer"};
        Assert.That(files.Save(old),Is.True,files.LastError);Assert.That(files.Save(old),Is.True);return old;
    }
    [Test] public void ResetCommitsNewDefaultsKeepsOldFilesAndRestartKeepsStableNewIdentity()
    {
        var old=OldProgress();string originalPath=files.MainPath;byte[] original=File.ReadAllBytes(originalPath),backup=File.ReadAllBytes(originalPath+".bak");var initial=NewGameDefaults.Create();
        Assert.That(Reset(files,initial),Is.True,files.LastError);Assert.That(files.MainPath,Is.Not.EqualTo(originalPath));Assert.That(File.ReadAllBytes(originalPath),Is.EqualTo(original));Assert.That(File.ReadAllBytes(originalPath+".bak"),Is.EqualTo(backup));
        var restart=new SaveFileStore(directory);Assert.That(restart.Load(out var restored),Is.EqualTo(SaveLoadStatus.Loaded));Assert.That(JsonUtility.ToJson(restored),Is.EqualTo(JsonUtility.ToJson(initial)));Assert.That(restored.characterInstances.instances[0].instanceId,Is.Not.EqualTo(old.characterInstances.instances[0].instanceId));
        Assert.That(File.ReadAllText(files.MainPath),Is.EqualTo(File.ReadAllText(files.MainPath+".bak")));
    }
    [Test] public void CorruptNewMainRecoversOnlyItsNewBackupNeverOldRecoveryOrMigrationFiles()
    {
        var old=OldProgress();File.WriteAllText(Path.Combine(directory,"save.recovery-newest.json"),JsonUtility.ToJson(old));File.WriteAllText(Path.Combine(directory,"save.json.v5.bak"),JsonUtility.ToJson(old));
        var initial=NewGameDefaults.Create();Assert.That(Reset(files,initial),Is.True);File.WriteAllText(files.MainPath,"broken");
        var restart=new SaveFileStore(directory);Assert.That(restart.Load(out var restored),Is.EqualTo(SaveLoadStatus.Recovered));Assert.That(JsonUtility.ToJson(restored),Is.EqualTo(JsonUtility.ToJson(initial)));Assert.That(restart.Save(restored),Is.True);Assert.That(new SaveFileStore(directory).Load(out restored),Is.EqualTo(SaveLoadStatus.Recovered));Assert.That(restored.wallet.balance,Is.EqualTo(EconomySettings.Current.initialCoins));
    }
    [Test] public void OrphanCandidateAndInterruptedPointerBeforeCommitKeepOriginalProgress()
    {
        var old=OldProgress();string id="progress-"+Guid.NewGuid().ToString("N");var orphan=new SaveFileStore(Path.Combine(directory,id));orphan.Save(NewGameDefaults.Create());
        File.WriteAllText(Path.Combine(directory,"save-generation.json.tmp-interrupted"),"{\"version\":1,\"generationId\":\""+id+"\"}");
        Assert.That(new SaveFileStore(directory).Load(out var restored),Is.EqualTo(SaveLoadStatus.Loaded));Assert.That(JsonUtility.ToJson(restored),Is.EqualTo(JsonUtility.ToJson(old)));
    }
    [Test] public void CommitFailureLeavesOriginalMainBackupAndNextStartupUnchanged()
    {
        var old=OldProgress();string main=files.MainPath;byte[] before=File.ReadAllBytes(main),bak=File.ReadAllBytes(main+".bak");Directory.CreateDirectory(Path.Combine(directory,"save-generation.json"));
        Assert.That(Reset(files,NewGameDefaults.Create()),Is.False);Assert.That(File.ReadAllBytes(main),Is.EqualTo(before));Assert.That(File.ReadAllBytes(main+".bak"),Is.EqualTo(bak));
        Assert.That(new SaveFileStore(directory).Load(out var restored),Is.EqualTo(SaveLoadStatus.Loaded));Assert.That(JsonUtility.ToJson(restored),Is.EqualTo(JsonUtility.ToJson(old)));
        Directory.Delete(Path.Combine(directory,"save-generation.json"));Assert.That(Reset(files,NewGameDefaults.Create()),Is.True);
    }
    [Test] public void RepeatedResetsSwitchGenerationAndRejectStaleWriters()
    {
        OldProgress();var stale=new SaveFileStore(directory);stale.Load(out var old);var first=NewGameDefaults.Create();Assert.That(Reset(files,first),Is.True);string firstPath=files.MainPath;
        Assert.That(stale.Save(old),Is.False);Assert.That(Reset(stale,old),Is.False);var second=NewGameDefaults.Create();Assert.That(Reset(files,second),Is.True);Assert.That(files.MainPath,Is.Not.EqualTo(firstPath));
        var restart=new SaveFileStore(directory);restart.Load(out var restored);Assert.That(JsonUtility.ToJson(restored),Is.EqualTo(JsonUtility.ToJson(second)));Assert.That(File.Exists(firstPath),Is.True);
    }
    [TestCase("broken")][TestCase("{\"version\":2,\"generationId\":\"future\"}")][TestCase("{\"version\":1,\"generationId\":\"../outside\"}")][TestCase("{\"version\":1,\"generationId\":\"progress-11111111111111111111111111111111\"}")]
    public void InvalidOrFuturePointerProtectsOriginalInsteadOfResurrectingIt(string pointer)
    {
        OldProgress();string main=files.MainPath;byte[] bytes=File.ReadAllBytes(main);File.WriteAllText(Path.Combine(directory,"save-generation.json"),pointer);
        var restart=new SaveFileStore(directory);Assert.That(restart.Load(out var candidate),Is.EqualTo(SaveLoadStatus.InvalidResetState));Assert.That(restart.WritesBlocked,Is.True);Assert.That(restart.Save(candidate),Is.False);Assert.That(Reset(restart,candidate),Is.False);Assert.That(File.ReadAllBytes(main),Is.EqualTo(bytes));Assert.That(File.ReadAllText(Path.Combine(directory,"save-generation.json")),Is.EqualTo(pointer));
    }
    [Test] public void FutureSaveCannotBeOverwrittenByResetEvenWithoutPriorLoad()
    {File.WriteAllText(files.MainPath,"{\"version\":7}");Assert.That(Reset(files,NewGameDefaults.Create()),Is.False);Assert.That(File.ReadAllText(files.MainPath),Is.EqualTo("{\"version\":7}"));Assert.That(File.Exists(Path.Combine(directory,"save-generation.json")),Is.False);}
    [Test] public void InvalidResetDataIsRejectedWithoutCreatingAnyGeneration()
    {OldProgress();var bad=NewGameDefaults.Create();bad.wallet.balance=-1;Assert.That(Reset(files,bad),Is.False);Assert.That(Directory.GetDirectories(directory,"progress-*").Length,Is.Zero);}
    [Test] public void OrdinaryResetIsPublicAndUnknownKindsAreAlwaysDenied()
    {Assert.That(ProgressResetAccess.Allows(ProgressResetKind.NewGame),Is.True);Assert.That(ProgressResetAccess.DevelopmentEnabled,Is.True);foreach(ProgressResetKind kind in Enum.GetValues(typeof(ProgressResetKind)))Assert.That(ProgressResetAccess.Allows(kind),Is.True);Assert.That(ProgressResetAccess.Allows((ProgressResetKind)999),Is.False);}
}

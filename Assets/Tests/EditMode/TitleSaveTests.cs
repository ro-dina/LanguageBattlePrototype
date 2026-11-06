using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
public class TitleSaveTests
{
    private string directory;
    private SaveFileStore files;
    private static string Fingerprint(string path) => Directory.Exists(path) ? string.Join("\n",Directory.GetFiles(path,"*",SearchOption.AllDirectories).OrderBy(p=>p).Select(p=>p+":"+File.GetLastWriteTimeUtc(p).Ticks+":"+Convert.ToBase64String(File.ReadAllBytes(p)))) : "missing";
    [SetUp] public void Setup()
    {
        GameDatabase.EnsureAvailable();directory=Path.Combine(Path.GetTempPath(),"LanguageBattleTitleFiles",Guid.NewGuid().ToString("N"));files=new SaveFileStore(directory);
    }
    [TearDown] public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);if(File.Exists(directory))File.Delete(directory);}
    [Test] public void EmptyInspectionDoesNotCreateDirectoryProgressOrDefaultIdentity()
    {Assert.That(files.Inspect(out var data),Is.EqualTo(SaveLoadStatus.NewGame));Assert.That(data,Is.Null);Assert.That(Directory.Exists(directory),Is.False);}
    [Test] public void ExistingVersionSixInspectionPreservesEverySectionBytesAndTimestamps()
    {
        var save=NewGameDefaults.Create();save.wallet.balance=12345;save.inventory.items.Add(new InventoryEntry("letter_a",7));save.tutorials.skipAll=true;save.stageProgress.clearedStageIds.Add("stage_1");
        var instance=save.characterInstances.instances[0];var gear=CharacterLoadoutStore.GetDefaultLoadout(instance.characterId,instance.instanceId);gear.adjectiveId="schwarz";save.characterEquipment.characters.Add(gear);
        Assert.That(files.Save(save),Is.True);Assert.That(files.Save(save),Is.True);string before=Fingerprint(directory);
        for(int i=0;i<3;i++){Assert.That(files.Inspect(out var loaded),Is.EqualTo(SaveLoadStatus.Loaded));Assert.That(JsonUtility.ToJson(loaded),Is.EqualTo(JsonUtility.ToJson(save)));}
        Assert.That(Fingerprint(directory),Is.EqualTo(before));
    }
    [Test] public void SettingsOnlyIsNotProgressAndNoProgressIsCreated()
    {
        var settings=new UserSettingsStore(Path.Combine(directory,"Settings"));settings.Save(UserSettingsData.Defaults(SystemLanguage.Japanese));string before=Fingerprint(directory);
        var progress=new SaveFileStore(Path.Combine(directory,"Progress"));Assert.That(progress.Inspect(out var data),Is.EqualTo(SaveLoadStatus.NewGame));Assert.That(data,Is.Null);Assert.That(Fingerprint(directory),Is.EqualTo(before));
    }
    [Test] public void DamagedMainWithValidBackupIsRecoverableWithoutWritingRecoveryFiles()
    {
        var save=NewGameDefaults.Create();save.wallet.balance=8765;files.Save(save);files.Save(save);File.WriteAllText(files.MainPath,"broken");string before=Fingerprint(directory);
        Assert.That(files.Inspect(out var data),Is.EqualTo(SaveLoadStatus.Recovered));Assert.That(data.wallet.balance,Is.EqualTo(8765));Assert.That(Fingerprint(directory),Is.EqualTo(before));
    }
    [Test] public void DamagedMainAndBackupAreCorruptInsteadOfAnUnconfirmedNewGame()
    {
        Directory.CreateDirectory(directory);File.WriteAllText(files.MainPath,"broken");File.WriteAllText(files.MainPath+".bak","broken-backup");string before=Fingerprint(directory);
        Assert.That(files.Inspect(out var data),Is.EqualTo(SaveLoadStatus.Corrupt));Assert.That(data,Is.Null);Assert.That(Fingerprint(directory),Is.EqualTo(before));
    }
    [Test] public void DamagedBackupWithoutMainIsNotMistakenForFirstRun()
    {Directory.CreateDirectory(directory);File.WriteAllText(files.MainPath+".bak","broken");Assert.That(files.Inspect(out var data),Is.EqualTo(SaveLoadStatus.Corrupt));Assert.That(data,Is.Null);}
    [Test] public void LegacyInspectionDoesNotMigrateOnDiskOrCreateBackupAndContinueLoadMigratesNormally()
    {
        Directory.CreateDirectory(directory);var old=new LegacySaveData{version=1,inventory=new InventoryState(),ownedCharacters=new OwnedCharactersState(),characterEquipment=new CharacterLoadoutState(),stageProgress=new StageProgressState()};old.ownedCharacters.characterIds.Add("nacht");
        File.WriteAllText(files.MainPath,JsonUtility.ToJson(old));string before=Fingerprint(directory);
        Assert.That(files.Inspect(out var first),Is.EqualTo(SaveLoadStatus.Migrated));Assert.That(files.Inspect(out var second),Is.EqualTo(SaveLoadStatus.Migrated));Assert.That(JsonUtility.ToJson(second),Is.EqualTo(JsonUtility.ToJson(first)));Assert.That(Fingerprint(directory),Is.EqualTo(before));
        Assert.That(files.Load(out var migrated),Is.EqualTo(SaveLoadStatus.Migrated));Assert.That(JsonUtility.ToJson(migrated),Is.EqualTo(JsonUtility.ToJson(first)));Assert.That(File.Exists(files.MainPath+".v1.bak"),Is.True);
    }
    [Test] public void FutureVersionIsUnavailableAndNeverReplaced()
    {Directory.CreateDirectory(directory);File.WriteAllText(files.MainPath,"{\"version\":99}");string before=Fingerprint(directory);Assert.That(files.Inspect(out var data),Is.EqualTo(SaveLoadStatus.UnsupportedVersion));Assert.That(data,Is.Null);Assert.That(files.WritesBlocked,Is.True);Assert.That(Fingerprint(directory),Is.EqualTo(before));}
    [Test] public void BrokenGenerationDoesNotFallBackToOldProgress()
    {files.Save(NewGameDefaults.Create());File.WriteAllText(Path.Combine(directory,"save-generation.json"),"broken");string before=Fingerprint(directory);Assert.That(files.Inspect(out var data),Is.EqualTo(SaveLoadStatus.InvalidResetState));Assert.That(data,Is.Null);Assert.That(Fingerprint(directory),Is.EqualTo(before));}
    [Test] public void UnreadableProgressLocationIsNotMissing()
    {File.WriteAllText(directory,"not-a-directory");Assert.That(files.Inspect(out var data),Is.EqualTo(SaveLoadStatus.ReadFailed));Assert.That(data,Is.Null);Assert.That(files.WritesBlocked,Is.True);}
    [Test] public void OnlyDeutschHasAPlayableLearningAdapterIndependentlyOfUiLanguage()
    {Assert.That(LearningLanguageCatalog.All.Select(l=>l.id),Is.EqualTo(new[]{"de"}));Assert.That(LearningLanguageCatalog.CanPlay("de"),Is.True);foreach(string id in new[]{"en","fr","it","ru","",null})Assert.That(LearningLanguageCatalog.CanPlay(id),Is.False);Assert.That(UserSettings.LearningLanguage,Is.EqualTo("de"));}
}

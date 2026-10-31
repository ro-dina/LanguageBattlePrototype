using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
public class WordItemMigrationTests
{
    private string directory;
    private SaveFileStore files;
    [SetUp] public void Setup()
    {
        if (PlayerSaveService.Instance != null) Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        GameDatabase.EnsureAvailable(); directory=Path.Combine(Path.GetTempPath(),"LanguageBattleWordMigration",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);files=new SaveFileStore(directory);
    }
    [TearDown] public void Cleanup()
    { if (PlayerSaveService.Instance != null) Object.DestroyImmediate(PlayerSaveService.Instance.gameObject); if (GameDatabase.Instance != null) Object.DestroyImmediate(GameDatabase.Instance.gameObject);Directory.Delete(directory,true); }
    private SaveData V2()
    {
        var data=NewGameDefaults.Create();data.version=2;
        data.inventory.items.AddRange(new[] {new InventoryEntry("word_nacht",26),new InventoryEntry("word_wasser",37),new InventoryEntry("word_pistole",42),new InventoryEntry("letter_a",54)});
        var gear=CharacterLoadoutStore.GetDefaultLoadout("nacht",data.characterInstances.instances[0].instanceId);gear.adjectiveId="schwarz";data.characterEquipment.characters.Add(gear);
        data.stageProgress.clearedStageIds.Add("old-stage");return data;
    }
    [Test] public void VersionTwoMigrationPreservesAllOldStateConvertsExactQuantitiesAndRetriesWithoutMultiplication()
    {
        var old=V2();string json=JsonUtility.ToJson(old,true);var original=Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(json)).ToArray();File.WriteAllBytes(files.MainPath,original);
        Assert.That(SaveDataMigration.TryMigrate(json,out var first,out _),Is.True);Assert.That(SaveDataMigration.TryMigrate(json,out var second,out _),Is.True);
        Assert.That(JsonUtility.ToJson(first),Is.EqualTo(JsonUtility.ToJson(second)));
        Assert.That(files.Load(out var data),Is.EqualTo(SaveLoadStatus.Migrated),files.LastError);
        Assert.That(data.version,Is.EqualTo(5));Assert.That(data.characterInstances.instances.Count,Is.EqualTo(106));
        Assert.That(data.characterInstances.instances.Count(i=>i.characterId=="nacht"),Is.EqualTo(27));
        Assert.That(data.characterInstances.instances.Count(i=>i.characterId=="wasser"),Is.EqualTo(37));
        Assert.That(data.characterInstances.instances.Count(i=>i.characterId=="pistole"),Is.EqualTo(42));
        Assert.That(data.characterInstances.instances[0].instanceId,Is.EqualTo(old.characterInstances.instances[0].instanceId));
        Assert.That(JsonUtility.ToJson(data.characterEquipment),Is.EqualTo(JsonUtility.ToJson(old.characterEquipment)));
        Assert.That(data.inventory.items.Single().count,Is.EqualTo(54));Assert.That(data.convertedWordItems.Sum(x=>x.count),Is.EqualTo(105));
        Assert.That(data.stageProgress.clearedStageIds,Does.Contain("old-stage"));Assert.That(data.characterCollection.discoveredCharacterIds,Does.Contain("wasser").And.Contain("pistole"));
        Assert.That(File.ReadAllBytes(files.MainPath+".v2.bak"),Is.EqualTo(original));
        for (int n=0;n<3;n++) {var restart=new SaveFileStore(directory);Assert.That(restart.Load(out var restored),Is.EqualTo(SaveLoadStatus.Loaded));Assert.That(JsonUtility.ToJson(restored),Is.EqualTo(JsonUtility.ToJson(data)));restart.Save(restored);}
        Assert.That(File.ReadAllBytes(files.MainPath+".v2.bak"),Is.EqualTo(original));
    }
    [Test] public void VersionOnePathChainsBothMigrationsAndKeepsLegacyEquipment()
    {
        var v2=V2(); var v1=new LegacySaveData {version=1,inventory=v2.inventory,characterEquipment=v2.characterEquipment,stageProgress=v2.stageProgress,ownedCharacters=new OwnedCharactersState()};
        v1.ownedCharacters.characterIds.Add("nacht");v1.characterEquipment.characters[0].instanceId=null;
        File.WriteAllText(files.MainPath,JsonUtility.ToJson(v1));Assert.That(files.Load(out var data),Is.EqualTo(SaveLoadStatus.Migrated));
        Assert.That(data.version,Is.EqualTo(5));Assert.That(data.characterInstances.instances.Count,Is.EqualTo(106));
        Assert.That(data.characterEquipment.characters.Single().adjectiveId,Is.EqualTo("schwarz"));Assert.That(File.Exists(files.MainPath+".v1.bak"),Is.True);
    }
    [Test] public void BackupAndInterruptedMigrationRecoverDeterministicIdsWithoutTouchingDamagedSources()
    {
        string old=JsonUtility.ToJson(V2());File.WriteAllText(files.MainPath,"damaged");File.WriteAllText(files.MainPath+".v2.bak",old);File.WriteAllText(files.MainPath+".tmp-half","incomplete");
        SaveDataMigration.TryMigrate(old,out var expected,out _);Assert.That(files.Load(out var restored),Is.EqualTo(SaveLoadStatus.Recovered));
        Assert.That(JsonUtility.ToJson(restored),Is.EqualTo(JsonUtility.ToJson(expected)));Assert.That(files.Save(restored),Is.True);
        Assert.That(File.ReadAllText(files.MainPath),Is.EqualTo("damaged"));Assert.That(File.ReadAllText(files.MainPath+".v2.bak"),Is.EqualTo(old));
    }
    [Test] public void BackupFailureAndExcessiveQuantitiesProtectTheVersionTwoOriginal()
    {
        string old=JsonUtility.ToJson(V2());File.WriteAllText(files.MainPath,old);Directory.CreateDirectory(files.MainPath+".v2.bak");
        Assert.That(files.Load(out var fallback),Is.EqualTo(SaveLoadStatus.MigrationFailed));Assert.That(files.Save(fallback),Is.False);Assert.That(File.ReadAllText(files.MainPath),Is.EqualTo(old));
        Directory.Delete(files.MainPath+".v2.bak");var huge=V2();huge.inventory.items[0].count=int.MaxValue;old=JsonUtility.ToJson(huge);File.WriteAllText(files.MainPath,old);
        Assert.That(new SaveFileStore(directory).Load(out _),Is.EqualTo(SaveLoadStatus.MigrationFailed));Assert.That(File.ReadAllText(files.MainPath),Is.EqualTo(old));
    }
    [Test] public void RuntimeAutosavesKeepMigrationAuditAndExistingEquipment()
    {
        var old=V2();File.WriteAllText(files.MainPath,JsonUtility.ToJson(old));var service=PlayerSaveService.Initialize(directory);
        var ids=CharacterInstances.GetAll().Select(i=>i.instanceId).ToArray();InventoryManager.AddItem("letter_e",1);service.Reload();
        Assert.That(CharacterInstances.GetAll().Select(i=>i.instanceId),Is.EqualTo(ids));Assert.That(service.Capture().convertedWordItems.Sum(x=>x.count),Is.EqualTo(105));
        Assert.That(CharacterLoadoutStore.GetLoadout(old.characterInstances.instances[0].instanceId).adjectiveId,Is.EqualTo("schwarz"));
    }
    [Test] public void DamagedVersionTwoSectionsCanStillRecoverAValidBackupWithoutOverwritingMain()
    {
        var damaged=V2();damaged.inventory.items.Add(new InventoryEntry("unknown",1));string main=JsonUtility.ToJson(damaged);
        File.WriteAllText(files.MainPath,main);File.WriteAllText(files.MainPath+".bak",JsonUtility.ToJson(V2()));
        Assert.That(files.Load(out var data),Is.EqualTo(SaveLoadStatus.Recovered));Assert.That(data.characterInstances.instances.Count,Is.EqualTo(106));
        Assert.That(files.Save(data),Is.True,files.LastError);Assert.That(File.ReadAllText(files.MainPath),Is.EqualTo(main));
        Assert.That(new SaveFileStore(directory).Load(out var restart),Is.EqualTo(SaveLoadStatus.Recovered));
        Assert.That(JsonUtility.ToJson(restart),Is.EqualTo(JsonUtility.ToJson(data)));
    }
}

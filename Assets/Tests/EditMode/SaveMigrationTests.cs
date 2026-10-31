using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class SaveMigrationTests
{
    private string directory;
    private SaveFileStore files;
    [SetUp] public void Setup()
    {
        if (PlayerSaveService.Instance != null) Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        GameDatabase.EnsureAvailable(); CharacterInstances.RestoreState(new CharacterInstanceState());
        CharacterCollection.RestoreState(new CharacterCollectionState()); CharacterLoadoutStore.RestoreState(new CharacterLoadoutState());
        directory = Path.Combine(Path.GetTempPath(), "LanguageBattleMigrationTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        files = new SaveFileStore(directory);
    }
    [TearDown] public void Cleanup()
    {
        if (PlayerSaveService.Instance != null) Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        if (GameDatabase.Instance != null) Object.DestroyImmediate(GameDatabase.Instance.gameObject);
        Directory.Delete(directory, true);
    }
    private string Legacy()
    {
        var old = new LegacySaveData { version = 1, inventory = new InventoryState(), ownedCharacters = new OwnedCharactersState(),
            characterEquipment = new CharacterLoadoutState(), stageProgress = new StageProgressState() };
        old.ownedCharacters.characterIds.AddRange(new[] { "nacht", "hund" });
        old.inventory.items.Add(new InventoryEntry("letter_a", 7)); old.stageProgress.clearedStageIds.Add("stage-old");
        var loadout = CharacterLoadoutStore.GetDefaultLoadout("nacht"); loadout.adjectiveId = "schwarz";
        loadout.articleId = "die_nom_fem_sg"; loadout.skillSlots[0].equippedAdverbId = "sehr"; old.characterEquipment.characters.Add(loadout);
        return JsonUtility.ToJson(old, true);
    }
    [Test] public void VersionOneMigratesAllSectionsAndPreservesOriginalBytesBeforeReplacingMain()
    {
        string old = Legacy(); var bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(old)).ToArray();
        File.WriteAllBytes(files.MainPath, bytes);
        Assert.That(files.Load(out var data), Is.EqualTo(SaveLoadStatus.Migrated), files.LastError);
        Assert.That(data.version, Is.EqualTo(5)); Assert.That(data.characterInstances.instances.Count, Is.EqualTo(2));
        Assert.That(data.characterCollection.discoveredCharacterIds, Is.EquivalentTo(new[] { "nacht", "hund" }));
        var nacht = data.characterInstances.instances.Single(i => i.characterId == "nacht");
        var loadout = data.characterEquipment.characters.Single();
        Assert.That(loadout.instanceId, Is.EqualTo(nacht.instanceId)); Assert.That(loadout.adjectiveId, Is.EqualTo("schwarz"));
        Assert.That(loadout.articleId, Is.EqualTo("die_nom_fem_sg")); Assert.That(loadout.skillSlots[0].equippedAdverbId, Is.EqualTo("sehr"));
        Assert.That(data.inventory.items.Single().count, Is.EqualTo(7)); Assert.That(data.stageProgress.clearedStageIds, Does.Contain("stage-old"));
        Assert.That(File.ReadAllBytes(files.MainPath + ".v1.bak"), Is.EqualTo(bytes));
        Assert.That(SaveDataValidator.TryDecode(File.ReadAllText(files.MainPath), out _, out _), Is.True);
    }
    [Test] public void RetryAfterBackupOrTemporaryWriteKeepsTheSameIdsAndDoesNotDuplicateInstances()
    {
        string old = Legacy(); File.WriteAllText(files.MainPath, old); File.WriteAllText(files.MainPath + ".v1.bak", old);
        SaveDataMigration.TryMigrate(old, out var expected, out _);
        File.WriteAllText(files.MainPath + ".tmp-interrupted", "half a write");
        Assert.That(files.Load(out var migrated), Is.EqualTo(SaveLoadStatus.Migrated));
        Assert.That(migrated.characterInstances.instances.Select(i => i.instanceId), Is.EqualTo(expected.characterInstances.instances.Select(i => i.instanceId)));
        for (int i = 0; i < 3; i++)
        {
            var restarted = new SaveFileStore(directory); Assert.That(restarted.Load(out var restored), Is.EqualTo(SaveLoadStatus.Loaded));
            Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(JsonUtility.ToJson(migrated))); Assert.That(restarted.Save(restored), Is.True);
        }
        Assert.That(File.ReadAllText(files.MainPath + ".v1.bak"), Is.EqualTo(old));
    }
    [Test] public void MigrationBackupFailureBlocksAllWritesAndCanBeRetriedSafely()
    {
        string old = Legacy(); File.WriteAllText(files.MainPath, old); Directory.CreateDirectory(files.MainPath + ".v1.bak");
        Assert.That(files.Load(out var fallback), Is.EqualTo(SaveLoadStatus.MigrationFailed));
        Assert.That(files.WritesBlocked, Is.True); Assert.That(files.Save(fallback), Is.False);
        Assert.That(File.ReadAllText(files.MainPath), Is.EqualTo(old));
        Directory.Delete(files.MainPath + ".v1.bak");
        Assert.That(new SaveFileStore(directory).Load(out var data), Is.EqualTo(SaveLoadStatus.Migrated));
        Assert.That(data.characterInstances.instances.Count, Is.EqualTo(2));
    }
    [Test] public void InvalidLegacyEquipmentNeverOverwritesTheVersionOneSource()
    {
        var old = JsonUtility.FromJson<LegacySaveData>(Legacy()); old.characterEquipment.characters[0].adjectiveId = "missing";
        string json = JsonUtility.ToJson(old); File.WriteAllText(files.MainPath, json);
        Assert.That(files.Load(out var fallback), Is.EqualTo(SaveLoadStatus.MigrationFailed));
        Assert.That(files.Save(fallback), Is.False); Assert.That(File.ReadAllText(files.MainPath), Is.EqualTo(json));
    }
    [Test] public void LegacyBackupCanRecoverAfterMainDamageWithoutDestroyingEitherSource()
    {
        string old = Legacy(); File.WriteAllText(files.MainPath, "broken-main"); File.WriteAllText(files.MainPath + ".bak", old);
        Assert.That(files.Load(out var data), Is.EqualTo(SaveLoadStatus.Recovered)); Assert.That(files.Save(data), Is.True);
        Assert.That(File.ReadAllText(files.MainPath), Is.EqualTo("broken-main")); Assert.That(File.ReadAllText(files.MainPath + ".bak"), Is.EqualTo(old));
        Assert.That(new SaveFileStore(directory).Load(out var restored), Is.EqualTo(SaveLoadStatus.Recovered));
        Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(JsonUtility.ToJson(data)));
    }
    [Test] public void VersionTwoKeepsIndependentLoadoutsAndDiscoveryAfterDeletingAllCopies()
    {
        var service = PlayerSaveService.Initialize(directory);
        var a = CharacterInstances.GetByCharacterId("nacht").Single(); var b = CharacterInstances.Create("nacht");
        var draft = CharacterLoadoutStore.GetLoadout(a.instanceId); draft.adjectiveId = "schwarz"; CharacterLoadoutStore.Commit(draft);
        draft = CharacterLoadoutStore.GetLoadout(b.instanceId); draft.adjectiveId = "schnell"; CharacterLoadoutStore.Commit(draft);
        service.Reload();
        Assert.That(CharacterLoadoutStore.GetLoadout(a.instanceId).adjectiveId, Is.EqualTo("schwarz"));
        Assert.That(CharacterLoadoutStore.GetLoadout(b.instanceId).adjectiveId, Is.EqualTo("schnell"));
        CharacterInstances.Remove(a.instanceId); service.Reload();
        Assert.That(CharacterInstances.Owns(a.instanceId), Is.False); Assert.That(CharacterInstances.Owns(b.instanceId), Is.True);
        CharacterInstances.Remove(b.instanceId); service.Reload();
        Assert.That(CharacterInstances.GetAll(), Is.Empty); Assert.That(CharacterLoadoutStore.CaptureState().characters, Is.Empty);
        Assert.That(CharacterCollection.IsDiscovered("nacht"), Is.True); Assert.That(GameManager.SelectedCharacterInstanceId, Is.Empty);
    }
    [Test] public void RuntimeBootstrapPreservesMigratedEquipmentAndIdsAcrossAutosaves()
    {
        string old = Legacy(); File.WriteAllText(files.MainPath, old);
        var service = PlayerSaveService.Initialize(directory);
        Assert.That(service.LoadStatus, Is.EqualTo(SaveLoadStatus.Migrated));
        var ids = CharacterInstances.GetAll().Select(i => i.instanceId).ToArray();
        var nacht = CharacterInstances.GetByCharacterId("nacht").Single();
        Assert.That(CharacterLoadoutStore.GetLoadout(nacht.instanceId).adjectiveId, Is.EqualTo("schwarz"));
        Assert.That(CharacterLoadoutStore.GetLoadout(nacht.instanceId).skillSlots[0].equippedAdverbId, Is.EqualTo("sehr"));
        InventoryManager.AddItem("letter_a", 1); service.Reload();
        Assert.That(CharacterInstances.GetAll().Select(i => i.instanceId), Is.EqualTo(ids));
        Assert.That(InventoryManager.GetItemCount("letter_a"), Is.EqualTo(8));
        Assert.That(File.ReadAllText(files.MainPath + ".v1.bak"), Is.EqualTo(old));
    }
    [Test] public void ConflictingOrInterruptedLegacyBackupIsRetainedAndAnotherValidBackupCanRecover()
    {
        string old = Legacy(); File.WriteAllText(files.MainPath, old); File.WriteAllText(files.MainPath + ".v1.bak", "partial-old-backup");
        Assert.That(files.Load(out var data), Is.EqualTo(SaveLoadStatus.Migrated));
        var expectedIds = data.characterInstances.instances.Select(i => i.instanceId).ToArray();
        Assert.That(File.ReadAllText(files.MainPath + ".v1.bak"), Is.EqualTo("partial-old-backup"));
        Assert.That(Directory.GetFiles(directory, "save.json.v1.bak-*").Length, Is.EqualTo(1));
        File.WriteAllText(files.MainPath, "damaged-main"); File.WriteAllText(files.MainPath + ".bak", "damaged-rotating-backup");
        var recovery = new SaveFileStore(directory); Assert.That(recovery.Load(out var restored), Is.EqualTo(SaveLoadStatus.Recovered));
        Assert.That(restored.characterInstances.instances.Select(i => i.instanceId), Is.EqualTo(expectedIds));
        Assert.That(recovery.Save(restored), Is.True); Assert.That(File.ReadAllText(files.MainPath), Is.EqualTo("damaged-main"));
    }
    [Test] public void LegacyOrphanLoadoutIsProtectedInsteadOfSilentlyLostOrGrantingAnUnownedSpecies()
    {
        var old = JsonUtility.FromJson<LegacySaveData>(Legacy()); old.characterEquipment.characters.Add(CharacterLoadoutStore.GetDefaultLoadout("feuer"));
        string json = JsonUtility.ToJson(old); File.WriteAllText(files.MainPath, json);
        Assert.That(files.Load(out var fallback), Is.EqualTo(SaveLoadStatus.MigrationFailed));
        Assert.That(files.Save(fallback), Is.False); Assert.That(File.ReadAllText(files.MainPath), Is.EqualTo(json));
    }
    [TestCase("duplicate")][TestCase("unknown")][TestCase("orphan-loadout")][TestCase("missing-discovery")][TestCase("mismatched-loadout")]
    public void InvalidVersionTwoReferencesAreRejected(string defect)
    {
        var data = NewGameDefaults.Create(); var instance = data.characterInstances.instances.Single();
        if (defect == "duplicate") data.characterInstances.instances.Add(instance);
        if (defect == "unknown") instance.characterId = "missing";
        if (defect == "missing-discovery") data.characterCollection.discoveredCharacterIds.Clear();
        if (defect.Contains("loadout"))
        {
            var loadout = CharacterLoadoutStore.GetDefaultLoadout("nacht", defect == "orphan-loadout" ? "missing" : instance.instanceId);
            if (defect == "mismatched-loadout") loadout.characterId = "hund";
            data.characterEquipment.characters.Add(loadout);
        }
        Assert.That(files.Save(data), Is.False); Assert.That(File.Exists(files.MainPath), Is.False);
    }
}

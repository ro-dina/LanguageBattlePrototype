using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class SaveTests
{
    private static string Instance(string species)
    {
        var instances = CharacterInstances.GetByCharacterId(species);
        return instances.Count > 0 ? instances[0].instanceId : CharacterInstances.Create(species).instanceId;
    }

    private string directory;
    private SaveFileStore files;

    [SetUp]
    public void Setup()
    {
        GameDatabase.EnsureAvailable();
        directory = Path.Combine(Path.GetTempPath(), "LanguageBattleSaveTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        files = new SaveFileStore(directory);
    }

    [TearDown]
    public void Cleanup()
    {
        if (PlayerSaveService.Instance != null) Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        if (GameDatabase.Instance != null) Object.DestroyImmediate(GameDatabase.Instance.gameObject);
        Directory.Delete(directory, true);
    }

    [Test]
    public void NewGameHasExplicitStarterAndNoInventedStageProgress()
    {
        Assert.That(files.Load(out var data), Is.EqualTo(SaveLoadStatus.NewGame));
        Assert.That(data.version, Is.EqualTo(5));
        Assert.That(data.characterCollection.discoveredCharacterIds, Does.Contain("nacht"));
        Assert.That(data.inventory.items, Is.Empty);
        Assert.That(data.stageProgress.unlockedStageIds, Is.Empty);
        Assert.That(data.stageProgress.clearedStageIds, Is.Empty);
    }

    [Test]
    public void AtomicSaveBackupAndRestartRoundTripAllSections()
    {
        var data = NewGameDefaults.Create();
        Assert.That(files.Save(data), Is.True, files.LastError);
        data.inventory.items.Add(new InventoryEntry("letter_a", 3));
        var hundId = CharacterInstances.NewId();
        data.characterInstances.instances.Add(new CharacterInstance { instanceId = hundId, characterId = "hund" });
        data.characterCollection.discoveredCharacterIds.Add("hund");
        var loadout = CharacterLoadoutStore.GetDefaultLoadout("hund", hundId);
        loadout.adjectiveId = "schnell";
        loadout.articleId = "die_nom_fem_sg";
        loadout.skillSlots[0].equippedAdverbId = "sehr";
        data.characterEquipment.characters.Add(loadout);
        data.stageProgress.unlockedStageIds.Add("test-stage");
        data.stageProgress.clearedStageIds.Add("test-stage");
        Assert.That(files.Save(data), Is.True, files.LastError);
        Assert.That(File.Exists(files.MainPath + ".bak"), Is.True);
        var restarted = new SaveFileStore(directory);
        Assert.That(restarted.Load(out var restored), Is.EqualTo(SaveLoadStatus.Loaded));
        Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(JsonUtility.ToJson(data)));
        Assert.That(Directory.GetFiles(directory, "*.tmp-*"), Is.Empty);
    }

    [TestCase("{broken")]
    [TestCase("{}")]
    [TestCase("")]
    public void CorruptFileIsPreservedAndRecoverySurvivesRestart(string corrupt)
    {
        File.WriteAllText(files.MainPath, corrupt);
        Assert.That(files.Load(out var data), Is.EqualTo(SaveLoadStatus.Recovered));
        Assert.That(files.Save(data), Is.True, files.LastError);
        Assert.That(File.ReadAllText(files.MainPath), Is.EqualTo(corrupt));
        data.inventory.items.Add(new InventoryEntry("letter_a", 2));
        Assert.That(files.Save(data), Is.True, files.LastError);
        Assert.That(new SaveFileStore(directory).Load(out var restored), Is.EqualTo(SaveLoadStatus.Recovered));
        Assert.That(restored.inventory.items[0].count, Is.EqualTo(2));
    }

    [Test]
    public void ValidBackupIsUsedWithoutOverwritingDamagedMain()
    {
        var data = NewGameDefaults.Create();
        data.inventory.items.Add(new InventoryEntry("letter_a", 4));
        files.Save(data);
        files.Save(data);
        File.WriteAllText(files.MainPath, "broken");
        var restarted = new SaveFileStore(directory);
        Assert.That(restarted.Load(out var restored), Is.EqualTo(SaveLoadStatus.Recovered));
        Assert.That(restored.inventory.items[0].count, Is.EqualTo(4));
        Assert.That(restarted.Save(restored), Is.True);
        Assert.That(File.ReadAllText(files.MainPath), Is.EqualTo("broken"));
    }

    [Test]
    public void InvalidBackupAndRecoveryCannotLeakInvalidStateOrBeOverwritten()
    {
        File.WriteAllText(files.MainPath, "broken-main");
        File.WriteAllText(files.MainPath + ".bak", "broken-backup");
        string recovery = Path.Combine(directory, "save.recovery-broken.json");
        File.WriteAllText(recovery, "broken-recovery");
        Assert.That(files.Load(out var data), Is.EqualTo(SaveLoadStatus.Recovered));
        Assert.That(data.characterCollection.discoveredCharacterIds, Does.Contain("nacht"));
        Assert.That(files.Save(data), Is.True);
        Assert.That(File.ReadAllText(files.MainPath), Is.EqualTo("broken-main"));
        Assert.That(File.ReadAllText(files.MainPath + ".bak"), Is.EqualTo("broken-backup"));
        Assert.That(File.ReadAllText(recovery), Is.EqualTo("broken-recovery"));
    }

    [Test]
    public void DamageOrFutureVersionAppearingAfterLoadIsPreserved()
    {
        var data = NewGameDefaults.Create();
        Assert.That(files.Save(data), Is.True);
        files.Load(out data);
        File.WriteAllText(files.MainPath, "damaged-after-load");
        Assert.That(files.Save(data), Is.True);
        Assert.That(File.ReadAllText(files.MainPath), Is.EqualTo("damaged-after-load"));
        var future = NewGameDefaults.Create(); future.version = 6;
        string json = JsonUtility.ToJson(future);
        File.WriteAllText(files.ActivePath, json);
        Assert.That(files.Save(data), Is.False);
        Assert.That(files.WritesBlocked, Is.True);
        Assert.That(File.ReadAllText(files.ActivePath), Is.EqualTo(json));
    }

    [TestCase(6)]
    [TestCase(-1)]
    public void UnsupportedVersionBlocksWritesAndRetainsBytes(int version)
    {
        var data = NewGameDefaults.Create();
        data.version = version;
        string json = JsonUtility.ToJson(data);
        File.WriteAllText(files.MainPath, json);
        Assert.That(files.Load(out var recovered), Is.EqualTo(SaveLoadStatus.UnsupportedVersion));
        Assert.That(files.Save(recovered), Is.False);
        Assert.That(File.ReadAllText(files.MainPath), Is.EqualTo(json));
    }

    [TestCase("missing", 1)]
    [TestCase("letter_a", -1)]
    [TestCase("letter_a", 0)]
    public void InvalidInventoryIsRejectedAndNotRestored(string id, int count)
    {
        var data = NewGameDefaults.Create();
        data.inventory.items.Add(new InventoryEntry(id, count));
        Assert.That(files.Save(data), Is.False);
        File.WriteAllText(files.MainPath, JsonUtility.ToJson(data));
        Assert.That(files.Load(out var restored), Is.EqualTo(SaveLoadStatus.Recovered));
        Assert.That(restored.inventory.items, Is.Empty);
    }

    [Test]
    public void UnknownCharacterSkillAndEquipmentIdsAreRejected()
    {
        var data = NewGameDefaults.Create();
        data.characterInstances.instances.Add(new CharacterInstance { instanceId = "bad", characterId = "missing" });
        Assert.That(files.Save(data), Is.False);
        data.characterInstances.instances.RemoveAt(data.characterInstances.instances.Count - 1);
        var loadout = CharacterLoadoutStore.GetDefaultLoadout("nacht", data.characterInstances.instances[0].instanceId);
        data.characterEquipment.characters.Add(loadout);
        loadout.skillSlots[0].skillId = "missing";
        Assert.That(files.Save(data), Is.False);
        loadout.skillSlots[0].skillId = "sein";
        loadout.adjectiveId = "missing";
        Assert.That(files.Save(data), Is.False);
    }

    [Test]
    public void IoFailureReturnsFalseWithoutThrowing()
    {
        string notDirectory = Path.Combine(directory, "file");
        File.WriteAllText(notDirectory, "occupied");
        var blocked = new SaveFileStore(notDirectory);
        Assert.That(blocked.Save(NewGameDefaults.Create()), Is.False);
        Assert.That(blocked.LastError, Does.Contain("failed"));
    }

    [Test]
    public void RuntimeMutationsAutosaveButDraftDoesNotAndCraftIsOneCommit()
    {
        var service = PlayerSaveService.Initialize(directory);
        Assert.That(InventoryManager.AddItem("word_wasser", 1), Is.True);
        Assert.That(InventoryManager.AddItem("word_pistole", 1), Is.True);
        string beforeDraft = File.ReadAllText(files.MainPath);
        var draft = CharacterLoadoutStore.GetLoadout(Instance("nacht"));
        draft.adjectiveId = "schwarz";
        Assert.That(File.ReadAllText(files.MainPath), Is.EqualTo(beforeDraft));
        CharacterLoadoutStore.Commit(draft);
        Assert.That(SaveDataValidator.TryDecode(File.ReadAllText(files.MainPath), out var saved, out _), Is.True);
        Assert.That(saved.characterEquipment.characters[0].adjectiveId, Is.EqualTo("schwarz"));
        int notifications = 0;
        Action count = () => notifications++;
        PlayerStateChanges.Changed += count;
        try { Assert.That(CraftingManager.Craft("craft_wasserpistole"), Is.True); }
        finally { PlayerStateChanges.Changed -= count; }
        Assert.That(notifications, Is.EqualTo(1));
        service.Reload();
        Assert.That(InventoryManager.GetItemCount("word_wasser"), Is.EqualTo(0));
        Assert.That(OwnedCharacters.IsCharacterOwned("wasserpistole"), Is.True);
        Assert.That(CharacterLoadoutStore.GetLoadout(Instance("nacht")).adjectiveId, Is.EqualTo("schwarz"));
    }
}

using System.Linq;
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

[PrebuildSetup(typeof(TestSaveIsolation))]
[PostBuildCleanup(typeof(TestSaveIsolation))]
public class SaveSceneTests
{
    private static string Instance(string species)
    {
        var instances = CharacterInstances.GetByCharacterId(species);
        return instances.Count > 0 ? instances[0].instanceId : CharacterInstances.Create(species).instanceId;
    }

    private string directory;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        SceneManager.LoadScene("GermanyHome"); yield return null; yield return null;
        if (PlayerSaveService.Instance != null) Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        directory = Path.Combine(Path.GetTempPath(), "LanguageBattlePlayTests", Guid.NewGuid().ToString("N"));
        PlayerSaveService.Initialize(directory);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        SceneManager.LoadScene("GermanyHome");
        yield return null;
        if (PlayerSaveService.Instance != null) Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        Directory.Delete(directory, true);
    }

    [UnityTest]
    public IEnumerator RestartRestoresInventoryOwnedLoadoutAndGenericStageState()
    {
        InventoryManager.AddItem("letter_a", 7);
        OwnedCharacters.UnlockCharacter("hund");
        StageProgress.Unlock("test-stage");
        StageProgress.MarkCleared("test-stage");
        var draft = CharacterLoadoutStore.GetLoadout(Instance("hund"));
        draft.adjectiveId = "schnell";
        draft.skillSlots[0].equippedAdverbId = "sehr";
        CharacterLoadoutStore.Commit(draft);
        Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        InventoryManager.RestoreState(new InventoryState());
        OwnedCharacters.RestoreState(new OwnedCharactersState());
        CharacterLoadoutStore.RestoreState(new CharacterLoadoutState());
        StageProgress.RestoreState(new StageProgressState());
        var service = PlayerSaveService.Initialize(directory);
        Assert.That(service.LoadStatus, Is.EqualTo(SaveLoadStatus.Loaded));
        Assert.That(InventoryManager.GetItemCount("letter_a"), Is.EqualTo(7));
        Assert.That(OwnedCharacters.IsCharacterOwned("hund"), Is.True);
        Assert.That(CharacterLoadoutStore.GetLoadout(Instance("hund")).skillSlots[0].equippedAdverbId, Is.EqualTo("sehr"));
        Assert.That(CharacterLoadoutStore.GetLoadout(Instance("hund")).adjectiveId, Is.EqualTo("schnell"));
        Assert.That(StageProgress.CaptureState().clearedStageIds, Does.Contain("test-stage"));
        SceneManager.LoadScene("Inventory");
        yield return null;
        yield return null;
        Assert.That(InventoryManager.GetItemCount("letter_a"), Is.EqualTo(7));
    }

    [UnityTest]
    public IEnumerator GachaAutosavesMaterialAndCharacterRewardsTogether()
    {
        int before=CharacterInstances.GetAll().Count;
        SceneManager.LoadScene("Gacha");yield return null;yield return null;
        var gacha=Object.FindAnyObjectByType<GachaManager>();gacha.PullOnce();yield return new WaitForSecondsRealtime(.3f);gacha.PullTen();
        int count=InventoryManager.GetAllItems().Sum(e=>e.count)+CharacterInstances.GetAll().Count-before;
        Assert.That(count,Is.EqualTo(11));
        var ids=CharacterInstances.GetAll().Select(i=>i.instanceId).ToArray();
        PlayerSaveService.Instance.Reload();
        Assert.That(InventoryManager.GetAllItems().Sum(e=>e.count)+CharacterInstances.GetAll().Count-before,Is.EqualTo(11));
        Assert.That(CharacterInstances.GetAll().Select(i=>i.instanceId),Is.EqualTo(ids));
        Assert.That(InventoryManager.GetItemCount("word_nacht"),Is.Zero);
    }

    [UnityTest]
    public IEnumerator CraftAutosavesConsumedMaterialsAndUnlockedCharacterTogether()
    {
        InventoryManager.AddItem("word_wasser", 1);
        InventoryManager.AddItem("word_pistole", 1);
        Assert.That(CraftingManager.Craft("craft_wasserpistole"), Is.True);
        PlayerSaveService.Instance.Reload();
        Assert.That(InventoryManager.GetItemCount("word_wasser"), Is.EqualTo(0));
        Assert.That(InventoryManager.GetItemCount("word_pistole"), Is.EqualTo(0));
        Assert.That(OwnedCharacters.IsCharacterOwned("wasserpistole"), Is.True);
        Assert.That(InventoryManager.AddItem("missing", 1), Is.False);
        Assert.That(OwnedCharacters.UnlockCharacter("missing"), Is.False);
        Assert.That(CraftingManager.Craft("craft_wasserpistole"), Is.False);
        yield return null;
    }
}

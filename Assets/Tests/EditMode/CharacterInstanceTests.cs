using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class CharacterInstanceTests
{
    [SetUp] public void Setup()
    {
        if (PlayerSaveService.Instance != null) Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        GameDatabase.EnsureAvailable();
        CharacterInstances.RestoreState(new CharacterInstanceState());
        CharacterCollection.RestoreState(new CharacterCollectionState());
        CharacterLoadoutStore.RestoreState(new CharacterLoadoutState());
        InventoryManager.RestoreState(new InventoryState());
    }
    [TearDown] public void Cleanup()
    {
        foreach (var instance in CharacterInstances.GetAll()) CharacterInstances.EndBattle(instance.instanceId);
        if (GameDatabase.Instance != null) Object.DestroyImmediate(GameDatabase.Instance.gameObject);
    }
    [Test] public void DuplicateSpeciesHaveUniqueDetachedPersistentIdentitiesAndCorrectCounts()
    {
        var first = CharacterInstances.Create("nacht"); var second = CharacterInstances.Create("nacht");
        var dog = CharacterInstances.Create("hund");
        Assert.That(first.instanceId, Is.Not.EqualTo(second.instanceId));
        Assert.That(Guid.TryParseExact(first.instanceId.Substring(10), "N", out _), Is.True);
        Assert.That(CharacterInstances.Get(first.instanceId).characterId, Is.EqualTo("nacht"));
        first.characterId = "feuer";
        Assert.That(CharacterInstances.Get(first.instanceId).characterId, Is.EqualTo("nacht"));
        Assert.That(CharacterInstances.GetByCharacterId("nacht").Count, Is.EqualTo(2));
        Assert.That(CharacterInstances.GetAll().Count, Is.EqualTo(3));
        Assert.That(CharacterInstances.Count("hund"), Is.EqualTo(1));
        var state = CharacterInstances.CaptureState(); CharacterInstances.RestoreState(state);
        Assert.That(CharacterInstances.Owns(second.instanceId), Is.True);
        Assert.That(CharacterInstances.DisplayName(first.instanceId), Is.Not.EqualTo(CharacterInstances.DisplayName(second.instanceId)));
    }
    [Test] public void GeneratingManyInstancesDoesNotReuseIdsOrModifyTheMaster()
    {
        string master = JsonUtility.ToJson(GameDatabase.Instance.GetCharacter("nacht"));
        var ids = Enumerable.Range(0, 200).Select(_ => CharacterInstances.Create("nacht").instanceId).ToArray();
        Assert.That(ids.Distinct().Count(), Is.EqualTo(200));
        Assert.That(JsonUtility.ToJson(GameDatabase.Instance.GetCharacter("nacht")), Is.EqualTo(master));
        Assert.That(CharacterInstances.Create("missing"), Is.Null);
    }
    [Test] public void SameSpeciesLoadoutsAndDraftsAreIndependentAndDeletionCleansOnlyItsOwnState()
    {
        var a = CharacterInstances.Create("nacht"); var b = CharacterInstances.Create("nacht");
        var draft = CharacterLoadoutStore.GetLoadout(a.instanceId); draft.adjectiveId = "schwarz";
        draft.articleId = "die_nom_fem_sg"; draft.skillSlots[0].equippedAdverbId = "sehr";
        Assert.That(CharacterLoadoutStore.GetLoadout(a.instanceId).adjectiveId, Is.Empty);
        Assert.That(CharacterLoadoutStore.Commit(draft), Is.True);
        draft.adjectiveId = "schnell";
        Assert.That(CharacterLoadoutStore.GetLoadout(a.instanceId).adjectiveId, Is.EqualTo("schwarz"));
        Assert.That(CharacterLoadoutStore.GetLoadout(b.instanceId).adjectiveId, Is.Empty);
        var other = CharacterLoadoutStore.GetLoadout(b.instanceId); other.adjectiveId = "schnell"; CharacterLoadoutStore.Commit(other);
        GameManager.SelectedCharacterInstanceId = a.instanceId;
        Assert.That(CharacterInstances.Remove(a.instanceId), Is.True);
        Assert.That(CharacterInstances.Count("nacht"), Is.EqualTo(1));
        Assert.That(GameManager.SelectedCharacterInstanceId, Is.EqualTo(b.instanceId));
        Assert.That(CharacterLoadoutStore.GetLoadout(a.instanceId), Is.Null);
        Assert.That(CharacterLoadoutStore.Commit(draft), Is.False);
        Assert.That(CharacterLoadoutStore.CaptureState().characters.Single().instanceId, Is.EqualTo(b.instanceId));
        Assert.That(CharacterLoadoutStore.GetLoadout(b.instanceId).adjectiveId, Is.EqualTo("schnell"));
    }
    [Test] public void CollectionRetainsDiscoveryAfterLastInstanceIsRemovedAndZeroOwnershipIsValid()
    {
        Assert.That(CharacterCollection.Status("feuer"), Is.EqualTo(CharacterCollectionStatus.Undiscovered));
        var a = CharacterInstances.Create("feuer");
        Assert.That(CharacterCollection.Status("feuer"), Is.EqualTo(CharacterCollectionStatus.Owned));
        CharacterInstances.Remove(a.instanceId);
        Assert.That(CharacterCollection.Status("feuer"), Is.EqualTo(CharacterCollectionStatus.DiscoveredNotOwned));
        Assert.That(CharacterCollection.IsDiscovered("feuer"), Is.True);
        Assert.That(CharacterInstances.Get(a.instanceId), Is.Null);
        Assert.That(CharacterLoadoutStore.GetCharacterForBattle(a.instanceId), Is.Null);
        Assert.That(GameManager.SelectedCharacterInstanceId, Is.Empty);
    }
    [Test] public void ActiveBattleLeaseRejectsDeletionAndOrphaningRestoreUntilReleased()
    {
        var a = CharacterInstances.Create("nacht"); var b = CharacterInstances.Create("nacht");
        Assert.That(CharacterInstances.TryBeginBattle(a.instanceId), Is.True);
        Assert.That(CharacterInstances.TryBeginBattle(a.instanceId), Is.False);
        Assert.That(CharacterInstances.Remove(a.instanceId), Is.False);
        Assert.That(CharacterInstances.RestoreState(new CharacterInstanceState()), Is.False);
        Assert.That(CharacterInstances.Remove(b.instanceId), Is.True);
        CharacterInstances.EndBattle(a.instanceId); Assert.That(CharacterInstances.Remove(a.instanceId), Is.True);
    }
    [Test] public void SpeciesIdsCannotAccidentallyAddressAnInstanceOrItsEquipment()
    {
        var a = CharacterInstances.Create("nacht"); var b = CharacterInstances.Create("nacht");
        Assert.That(CharacterLoadoutStore.GetLoadout("nacht"), Is.Null);
        GameManager.SelectedCharacterInstanceId = b.instanceId;
        GameManager.SelectedCharacterId = "nacht";
        Assert.That(GameManager.SelectedCharacterInstanceId, Is.EqualTo(b.instanceId));
        Assert.That(GameManager.SelectedCharacterId, Is.EqualTo("nacht"));
        GameManager.SelectedCharacterEquipmentId = "schwarz";
        Assert.That(CharacterLoadoutStore.GetLoadout(b.instanceId).adjectiveId, Is.EqualTo("schwarz"));
        Assert.That(CharacterLoadoutStore.GetLoadout(a.instanceId).adjectiveId, Is.Empty);
    }
    [Test] public void RepeatCraftingConsumesEachRecipeAndCreatesSeparateInstancesInOneNotification()
    {
        InventoryManager.AddItem("word_wasser", 2); InventoryManager.AddItem("word_pistole", 2);
        int notifications = 0; Action listener = () => notifications++; PlayerStateChanges.Changed += listener;
        try
        {
            Assert.That(CraftingManager.Craft("craft_wasserpistole"), Is.True);
            Assert.That(CraftingManager.Craft("craft_wasserpistole"), Is.True);
        }
        finally { PlayerStateChanges.Changed -= listener; }
        Assert.That(notifications, Is.EqualTo(2));
        Assert.That(CharacterInstances.Count("wasserpistole"), Is.EqualTo(2));
        Assert.That(CharacterInstances.GetByCharacterId("wasserpistole").Select(i => i.instanceId).Distinct().Count(), Is.EqualTo(2));
        Assert.That(InventoryManager.GetItemCount("word_wasser"), Is.Zero);
        Assert.That(CharacterCollection.IsDiscovered("wasserpistole"), Is.True);
    }
    [Test] public void InvalidGenerationTargetNeverConsumesMaterials()
    {
        InventoryManager.AddItem("word_wasser", 1); InventoryManager.AddItem("word_pistole", 1);
        var field = typeof(GameDatabase).GetField("characters", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        ((System.Collections.Generic.Dictionary<string, CharacterData>)field.GetValue(GameDatabase.Instance)).Remove("wasserpistole");
        Assert.That(CraftingManager.Craft("craft_wasserpistole"), Is.False);
        Assert.That(InventoryManager.GetItemCount("word_wasser"), Is.EqualTo(1));
        Assert.That(InventoryManager.GetItemCount("word_pistole"), Is.EqualTo(1));
        Assert.That(CharacterInstances.GetAll(), Is.Empty);
    }
}

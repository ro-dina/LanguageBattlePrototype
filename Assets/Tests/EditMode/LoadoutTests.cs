using NUnit.Framework;
using UnityEngine;

public class LoadoutTests
{
    private static string Instance(string species)
    {
        var instances = CharacterInstances.GetByCharacterId(species);
        return instances.Count > 0 ? instances[0].instanceId : CharacterInstances.Create(species).instanceId;
    }

    [SetUp]
    public void Setup()
    {
        GameDatabase.EnsureAvailable();
        var owned = new OwnedCharactersState(); owned.characterIds.AddRange(new[] { "nacht", "hund" }); OwnedCharacters.RestoreState(owned);
        CharacterLoadoutStore.RestoreState(new CharacterLoadoutState());
    }

    [TearDown]
    public void Cleanup()
    {
        CharacterLoadoutStore.RestoreState(new CharacterLoadoutState());
        if (GameDatabase.Instance != null) Object.DestroyImmediate(GameDatabase.Instance.gameObject);
    }

    [Test]
    public void EditingDraftDoesNotCommitOrChangeMaster()
    {
        var master = GameDatabase.Instance.GetCharacter("nacht");
        string initial = JsonUtility.ToJson(master);
        var draft = CharacterLoadoutStore.GetLoadout(Instance("nacht"));
        draft.adjectiveId = "schwarz";
        draft.skillSlots[0].equippedAdverbId = "sehr";
        Assert.That(CharacterLoadoutStore.GetLoadout(Instance("nacht")).adjectiveId, Is.Null.Or.Empty);
        Assert.That(CharacterLoadoutStore.GetLoadout(Instance("nacht")).skillSlots[0].equippedAdverbId, Is.Null.Or.Empty);
        Assert.That(JsonUtility.ToJson(master), Is.EqualTo(initial));
    }

    [Test]
    public void CommitIsIndependentAndOwnsItsCopy()
    {
        var nacht = CharacterLoadoutStore.GetLoadout(Instance("nacht"));
        nacht.adjectiveId = "schwarz";
        nacht.articleId = "die_nom_fem_sg";
        nacht.skillSlots[0].equippedAdverbId = "sehr";
        Assert.That(CharacterLoadoutStore.Commit(nacht), Is.True);
        nacht.adjectiveId = "schnell";
        nacht.skillSlots[0].equippedAdverbId = "schnell";
        var hund = CharacterLoadoutStore.GetLoadout(Instance("hund"));
        Assert.That(hund.adjectiveId, Is.Null.Or.Empty);
        Assert.That(hund.articleId, Is.Null.Or.Empty);
        hund.adjectiveId = "schnell";
        hund.skillSlots[0].equippedAdverbId = "schnell";
        Assert.That(CharacterLoadoutStore.Commit(hund), Is.True);
        var committed = CharacterLoadoutStore.GetLoadout(Instance("nacht"));
        Assert.That(committed.adjectiveId, Is.EqualTo("schwarz"));
        Assert.That(committed.articleId, Is.EqualTo("die_nom_fem_sg"));
        Assert.That(committed.skillSlots[0].equippedAdverbId, Is.EqualTo("sehr"));
    }

    [Test]
    public void UneditedCharacterUsesInitialMasterEquipment()
    {
        var master = GameDatabase.Instance.GetCharacter("hund");
        master.equippedAdjectiveId = "schnell";
        master.skillSlots = new[] { new CharacterSkillSlotData { skillId = "laufen", equippedAdverbId = "sehr" } };
        var defaults = CharacterLoadoutStore.GetLoadout(Instance("hund"));
        Assert.That(defaults.adjectiveId, Is.EqualTo("schnell"));
        Assert.That(defaults.skillSlots[0].equippedAdverbId, Is.EqualTo("sehr"));
        Assert.That(CharacterLoadoutStore.CaptureState().characters, Is.Empty);
    }

    [Test]
    public void CommittedSkillOrderAndAdverbsReachBattleSnapshot()
    {
        var draft = CharacterLoadoutStore.GetLoadout(Instance("nacht"));
        draft.adjectiveId = "schwarz";
        draft.skillSlots = new[] { new CharacterSkillSlotData { skillId = "brennen", equippedAdverbId = "sehr" } };
        Assert.That(CharacterLoadoutStore.Commit(draft), Is.True);
        var battle = CharacterLoadoutStore.GetCharacterForBattle(Instance("nacht"));
        Assert.That(battle.skillIds, Is.EqualTo(new[] { "brennen" }));
        Assert.That(battle.skillSlots[0].equippedAdverbId, Is.EqualTo("sehr"));
        Assert.That(battle.equippedAdjectiveId, Is.EqualTo("schwarz"));
        Assert.That(GameDatabase.Instance.GetCharacter("nacht").skillIds.Length, Is.EqualTo(3));
    }

    [Test]
    public void InvalidCommitAndRestoreLeaveCommittedStateUntouched()
    {
        var draft = CharacterLoadoutStore.GetLoadout(Instance("nacht"));
        draft.adjectiveId = "missing";
        Assert.That(CharacterLoadoutStore.Commit(draft), Is.False);
        draft.adjectiveId = "schwarz";
        Assert.That(CharacterLoadoutStore.Commit(draft), Is.True);
        var bad = new CharacterLoadoutState();
        bad.characters.Add(draft);
        bad.characters.Add(draft);
        Assert.That(CharacterLoadoutStore.RestoreState(bad), Is.False);
        Assert.That(CharacterLoadoutStore.GetLoadout(Instance("nacht")).adjectiveId, Is.EqualTo("schwarz"));
    }
}

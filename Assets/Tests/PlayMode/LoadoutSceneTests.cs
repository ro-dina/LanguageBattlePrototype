using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

[PrebuildSetup(typeof(TestSaveIsolation))]
[PostBuildCleanup(typeof(TestSaveIsolation))]
public class LoadoutSceneTests
{
    private static string Instance(string species)
    {
        var instances = CharacterInstances.GetByCharacterId(species);
        return instances.Count > 0 ? instances[0].instanceId : CharacterInstances.Create(species).instanceId;
    }

    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);

    [UnitySetUp]
    public IEnumerator Setup()
    {
        SceneManager.LoadScene("GermanyHome"); yield return null; yield return null;
        GameDatabase.EnsureAvailable();
        var seed = new OwnedCharactersState(); seed.characterIds.AddRange(new[] { "nacht", "hund" }); OwnedCharacters.RestoreState(seed);
        CharacterLoadoutStore.RestoreState(new CharacterLoadoutState());
        yield return null;
    }

    private static IEnumerator Detail(string id, string previous)
    {
        GameManager.SelectedCharacterId = id;
        GameManager.PreviousSceneName = previous;
        SceneManager.LoadScene("CharacterDetail");
        yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator CancelKeepsDraftAndDiscardDoesNotCommit()
    {
        yield return Detail("nacht", "CharacterCollection");
        var detail = Object.FindAnyObjectByType<CharacterDetailManager>();
        Call(detail, "EquipCharacterEquipment", "schwarz");
        detail.Back();
        Assert.That(Field<GameObject>(detail, "unsavedConfirmPanel").activeSelf, Is.True);
        detail.CancelBack();
        Assert.That(Field<GameObject>(detail, "unsavedConfirmPanel").activeSelf, Is.False);
        Assert.That(Field<string>(detail, "tempCharacterEquipmentId"), Is.EqualTo("schwarz"));
        Assert.That(CharacterLoadoutStore.GetLoadout(Instance("nacht")).adjectiveId, Is.Null.Or.Empty);
        detail.Back();
        detail.DiscardChangesAndBack();
        yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CharacterCollection"));
        yield return Detail("nacht", "CharacterSelect");
        detail = Object.FindAnyObjectByType<CharacterDetailManager>();
        Assert.That(Field<string>(detail, "tempCharacterEquipmentId"), Is.Null.Or.Empty);
    }

    [UnityTest]
    public IEnumerator SaveButtonCommitsAndBothSceneRoutesReadSameLoadout()
    {
        yield return Detail("nacht", "CharacterCollection");
        var detail = Object.FindAnyObjectByType<CharacterDetailManager>();
        Call(detail, "EquipCharacterEquipment", "schwarz");
        detail.OpenSkillEquipmentList(0);
        Call(detail, "EquipSkillEquipment", "sehr");
        detail.Back();
        GameObject.Find("SaveButton").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CharacterCollection"));
        foreach (string route in new[] { "CharacterSelect", "CharacterCollection" })
        {
            yield return Detail("nacht", route);
            detail = Object.FindAnyObjectByType<CharacterDetailManager>();
            Assert.That(Field<string>(detail, "tempCharacterEquipmentId"), Is.EqualTo("schwarz"));
            Assert.That(Field<string[]>(detail, "tempSkillEquipmentIds")[0], Is.EqualTo("sehr"));
        }
        yield return Detail("hund", "CharacterSelect");
        detail = Object.FindAnyObjectByType<CharacterDetailManager>();
        Assert.That(Field<string>(detail, "tempCharacterEquipmentId"), Is.Null.Or.Empty);
        Call(detail, "EquipCharacterEquipment", "schnell");
        detail.ConfirmEquipment();
        yield return null;
        Assert.That(CharacterLoadoutStore.GetLoadout(Instance("nacht")).adjectiveId, Is.EqualTo("schwarz"));
        Assert.That(CharacterLoadoutStore.GetLoadout(Instance("hund")).adjectiveId, Is.EqualTo("schnell"));
    }

    [UnityTest]
    public IEnumerator SelectionPreviewUsesCommittedEquipmentStats()
    {
        var draft = CharacterLoadoutStore.GetLoadout(Instance("nacht"));
        draft.adjectiveId = "schwarz";
        CharacterLoadoutStore.Commit(draft);
        var owned = new OwnedCharactersState(); owned.characterIds.Add("nacht");
        OwnedCharacters.RestoreState(owned);
        GameManager.SelectedCharacterId = "nacht";
        SceneManager.LoadScene("CharacterSelect");
        yield return null; yield return null;
        var select = Object.FindAnyObjectByType<CharacterSelectManager>();
        Assert.That(Field<TMPro.TMP_Text>(select, "selectedCharacterText").text, Does.Contain("ATK: 8 /"));
    }

    [UnityTest]
    public IEnumerator BattleReadsCommittedEquipmentAndEquippedSkills()
    {
        var draft = CharacterLoadoutStore.GetLoadout(Instance("nacht"));
        draft.adjectiveId = "schwarz";
        draft.skillSlots = new[] { new CharacterSkillSlotData { skillId = "brennen", equippedAdverbId = "sehr" } };
        Assert.That(CharacterLoadoutStore.Commit(draft), Is.True);
        GameManager.SelectedCharacterId = "nacht";
        GameManager.EnemyCharacterId = "feuer";
        SceneManager.LoadScene("Battle");
        yield return null;
        yield return null;
        var battle = Object.FindAnyObjectByType<BattleManager>();
        Assert.That(Field<EquipmentData>(battle, "playerCharacterEquipment").id, Is.EqualTo("schwarz"));
        Assert.That(Field<CharacterData>(battle, "playerCharacter").skillIds, Is.EqualTo(new[] { "brennen" }));
        Assert.That(Field<EquipmentData[]>(battle, "playerSkillEquipments")[0].id, Is.EqualTo("sehr"));
        Assert.That(Field<GameObject[]>(battle, "skillButtons")[1].activeSelf, Is.False);
    }
}

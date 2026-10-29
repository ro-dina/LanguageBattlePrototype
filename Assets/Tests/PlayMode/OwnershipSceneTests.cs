using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

[PrebuildSetup(typeof(TestSaveIsolation))]
[PostBuildCleanup(typeof(TestSaveIsolation))]
public class OwnershipSceneTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);

    [UnitySetUp]
    public IEnumerator Setup()
    {
        GameDatabase.EnsureAvailable();
        var owned = new OwnedCharactersState();
        owned.characterIds.Add("nacht");
        OwnedCharacters.RestoreState(owned);
        CharacterLoadoutStore.RestoreState(new CharacterLoadoutState());
        GameManager.SelectedCharacterId = "nacht";
        yield return null;
    }

    [UnityTest]
    public IEnumerator SelectionOnlyOffersOwnedCharactersAndIncludesNewUnlock()
    {
        SceneManager.LoadScene("CharacterSelect");
        yield return null; yield return null;
        var select = Object.FindAnyObjectByType<CharacterSelectManager>();
        var content = Field<Transform>(select, "characterButtonParent");
        Assert.That(content.GetComponentsInChildren<Button>().Length, Is.EqualTo(1));
        Call(select, "SelectCharacter", "hund");
        Assert.That(GameManager.SelectedCharacterId, Is.EqualTo("nacht"));
        Assert.That(OwnedCharacters.UnlockCharacter("hund"), Is.True);
        SceneManager.LoadScene("CharacterSelect");
        yield return null; yield return null;
        select = Object.FindAnyObjectByType<CharacterSelectManager>();
        Assert.That(Field<Transform>(select, "characterButtonParent").GetComponentsInChildren<Button>().Length, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator CollectionShowsLockedCharactersAndTheirDetailsCannotStartBattle()
    {
        SceneManager.LoadScene("CharacterCollection");
        yield return null; yield return null;
        var collection = Object.FindAnyObjectByType<CharacterCollectionManager>();
        var buttons = Field<Transform>(collection, "characterButtonParent").GetComponentsInChildren<Button>();
        Assert.That(buttons.Length, Is.EqualTo(GameDatabase.Instance.GetAllCharacters().Length));
        var hund = buttons.Single(b => b.GetComponentInChildren<TMP_Text>().text == "Hund [Not owned]");
        hund.onClick.Invoke();
        collection.OpenSelectedCharacterDetail();
        yield return null; yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CharacterDetail"));
        var detail = Object.FindAnyObjectByType<CharacterDetailManager>();
        detail.StartBattle();
        yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CharacterDetail"));
        Assert.That(GameObject.Find("BattleButton").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Confirm"));
    }

    [UnityTest]
    public IEnumerator DirectBattleEntryCannotBypassOwnership()
    {
        GameManager.SelectedCharacterId = "hund";
        SceneManager.LoadScene("Battle");
        yield return null; yield return null;
        var battle = Object.FindAnyObjectByType<BattleManager>();
        Assert.That(battle.enabled, Is.False);
        battle.OpenSkillPanel();
        Assert.That(Field<GameObject>(battle, "skillPanel").activeSelf, Is.False);
        Assert.That(Field<CharacterData>(battle, "playerCharacter"), Is.Null);
    }
}

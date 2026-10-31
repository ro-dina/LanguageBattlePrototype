using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[PrebuildSetup(typeof(TestSaveIsolation))]
[PostBuildCleanup(typeof(TestSaveIsolation))]
public class CharacterInstanceSceneTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private string a, b, directory;
    private float oldScale;
    [UnitySetUp] public IEnumerator Setup()
    {
        oldScale = Time.timeScale; Time.timeScale = 8;
        SceneManager.LoadScene("GermanyHome"); yield return null; yield return null;
        if (PlayerSaveService.Instance != null) Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        directory = Path.Combine(Path.GetTempPath(), "LanguageBattleInstancePlayTests", Guid.NewGuid().ToString("N"));
        var service = PlayerSaveService.Initialize(directory);
        using (PlayerStateChanges.BeginBatch())
        {
            CharacterInstances.RestoreState(new CharacterInstanceState()); CharacterCollection.RestoreState(new CharacterCollectionState());
            CharacterLoadoutStore.RestoreState(new CharacterLoadoutState()); InventoryManager.RestoreState(new InventoryState());
            a = CharacterInstances.Create("nacht").instanceId; b = CharacterInstances.Create("nacht").instanceId;
        }
        GameManager.SelectedCharacterInstanceId = a; GameManager.EnemyCharacterId = "feuer";
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        Time.timeScale = oldScale;
        SceneManager.LoadScene("GermanyHome"); yield return null; yield return null;
        if (PlayerSaveService.Instance != null) Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        Directory.Delete(directory, true);
    }
    private IEnumerator Load(string scene) { SceneManager.LoadScene(scene); yield return null; yield return null; }
    private IEnumerator Detail(string instance)
    { GameManager.SelectedCharacterInstanceId = instance; GameManager.PreviousSceneName = "CharacterCollection"; yield return Load("CharacterDetail"); }
    private IEnumerator Turn(BattleManager battle)
    {
        float end = Time.realtimeSinceStartup + 18;
        while (battle != null && Field<bool>(battle, "isProcessing") && Time.realtimeSinceStartup < end) yield return null;
        Assert.That(battle == null || !Field<bool>(battle, "isProcessing"), Is.True);
    }
    [UnityTest] public IEnumerator SelectionSeparatesSameSpeciesAndDetailSaveDoesNotLeakToAnotherInstance()
    {
        yield return Load("CharacterSelect"); var selection = Object.FindAnyObjectByType<CharacterSelectManager>();
        var buttons = Field<Transform>(selection, "characterButtonParent").GetComponentsInChildren<Button>();
        Assert.That(buttons.Length, Is.EqualTo(2));
        Assert.That(buttons.Select(x => x.GetComponentInChildren<TMP_Text>().text).Distinct().Count(), Is.EqualTo(2));
        Assert.That(buttons.All(x => x.GetComponentInChildren<TMP_Text>().text.Contains("Owned: 2")), Is.True);
        buttons.Single(x => x.name.EndsWith(b)).onClick.Invoke(); Assert.That(GameManager.SelectedCharacterInstanceId, Is.EqualTo(b));
        selection.CharacterDetail(); yield return null; yield return null;
        var detail = Object.FindAnyObjectByType<CharacterDetailManager>();
        Assert.That(Field<TMP_Text>(detail, "characterNameText").text, Is.EqualTo("Nacht"));
        Assert.That(Field<TMP_Text>(detail, "characterInfoText").text, Does.Contain("Instance: " + CharacterInstances.DisplayBadge(b)));
        Call(detail, "EquipCharacterEquipment", "schnell"); detail.ConfirmEquipment(); yield return null; yield return null;
        yield return Detail(a); detail = Object.FindAnyObjectByType<CharacterDetailManager>();
        Assert.That(Field<string>(detail, "tempCharacterEquipmentId"), Is.Empty);
        Call(detail, "EquipCharacterEquipment", "schwarz"); detail.OpenSkillEquipmentList(0); Call(detail, "EquipSkillEquipment", "sehr");
        detail.Back(); detail.CancelBack(); Assert.That(Field<string>(detail, "tempCharacterEquipmentId"), Is.EqualTo("schwarz"));
        detail.ConfirmEquipment(); yield return null; yield return null;
        yield return Detail(b); detail = Object.FindAnyObjectByType<CharacterDetailManager>();
        Assert.That(Field<string>(detail, "tempCharacterEquipmentId"), Is.EqualTo("schnell"));
        Call(detail, "EquipCharacterEquipment", "schwarz"); detail.DiscardChangesAndBack(); yield return null; yield return null;
        yield return Detail(a); detail = Object.FindAnyObjectByType<CharacterDetailManager>();
        Assert.That(Field<string>(detail, "tempCharacterEquipmentId"), Is.EqualTo("schwarz"));
        Assert.That(Field<string[]>(detail, "tempSkillEquipmentIds")[0], Is.EqualTo("sehr"));
        PlayerSaveService.Instance.Reload();
        Assert.That(CharacterLoadoutStore.GetLoadout(b).adjectiveId, Is.EqualTo("schnell"));
        Assert.That(CharacterLoadoutStore.GetLoadout(a).adjectiveId, Is.EqualTo("schwarz"));
    }
    [UnityTest] public IEnumerator ThreeCollectionStatesAndReadOnlyDiscoveredDetailsSurviveDeletionAndReload()
    {
        var fire = CharacterInstances.Create("feuer"); CharacterInstances.Remove(fire.instanceId); PlayerSaveService.Instance.Reload();
        yield return Load("CharacterCollection"); var collection = Object.FindAnyObjectByType<CharacterCollectionManager>();
        var buttons = Field<Transform>(collection, "characterButtonParent").GetComponentsInChildren<Button>();
        Assert.That(buttons.Any(x => x.GetComponentInChildren<TMP_Text>().text == "Nacht [Owned: 2]"), Is.True);
        var discovered = buttons.Single(x => x.GetComponentInChildren<TMP_Text>().text == "Feuer [Discovered • 0 owned]");
        Assert.That(buttons.Any(x => x.GetComponentInChildren<TMP_Text>().text == "Hund [Undiscovered]"), Is.True);
        discovered.onClick.Invoke(); collection.OpenSelectedCharacterDetail(); yield return null; yield return null;
        var detail = Object.FindAnyObjectByType<CharacterDetailManager>();
        Assert.That(GameManager.SelectedCharacterInstanceId, Is.Empty);
        Call(detail, "EquipCharacterEquipment", "schwarz"); detail.OpenAdjectiveEquipmentList(); detail.OpenSkillEquipmentList(0);
        Assert.That(Field<bool>(detail, "hasUnsavedChanges"), Is.False);
        Assert.That(Field<GameObject>(detail, "characterEquipmentPanel").activeSelf, Is.False);
        Assert.That(Field<GameObject>(detail, "skillEquipmentPanel").activeSelf, Is.False);
        detail.StartBattle(); yield return null; Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CharacterDetail"));
        Assert.That(CharacterLoadoutStore.CaptureState().characters, Is.Empty);
    }
    [UnityTest] public IEnumerator CraftingUiCanGenerateTheExistingRecipeTwiceWithTwoDifferentIds()
    {
        InventoryManager.AddItem("word_wasser", 2); InventoryManager.AddItem("word_pistole", 2);
        yield return Load("Crafting"); var ui = Object.FindAnyObjectByType<CraftingUIManager>();
        for (int i = 1; i <= 2; i++)
        {
            Call(ui, "SelectMaterial", "word_wasser"); Call(ui, "SelectMaterial", "word_pistole"); ui.CraftSelectedMaterials(); yield return null;
            Assert.That(CharacterInstances.Count("wasserpistole"), Is.EqualTo(i));
            Assert.That(Field<TMP_Text>(ui, "resultText").text, Does.Contain("Owned: " + i));
        }
        PlayerSaveService.Instance.Reload(); Assert.That(CharacterInstances.GetByCharacterId("wasserpistole").Select(x => x.instanceId).Distinct().Count(), Is.EqualTo(2));
        Assert.That(InventoryManager.GetItemCount("word_wasser"), Is.Zero);
    }
    [UnityTest] public IEnumerator EachInstanceLoadoutReachesOrdinaryFutureAndMuessenWithoutChangingCombatRules()
    {
        var draft = CharacterLoadoutStore.GetLoadout(a); draft.adjectiveId = "schwarz"; CharacterLoadoutStore.Commit(draft);
        foreach (var instance in new[] { a, b })
        foreach (string mode in new[] { "normal", "future", "muessen" })
        {
            GameManager.SelectedCharacterInstanceId = instance; yield return Load("Battle");
            var battle = Object.FindAnyObjectByType<BattleManager>(); var context = battle.Context;
            Assert.That(battle.PlayerInstanceId, Is.EqualTo(instance));
            Assert.That(CharacterInstances.Remove(instance), Is.False, "Active battle instance cannot disappear.");
            typeof(BattleCombatant).GetProperty("HP").SetValue(context.Enemy, 200);
            typeof(BattleCombatant).GetProperty("HP").SetValue(context.Player, 200);
            Call(battle, "SelectNoAdverb"); if (mode == "future") battle.SelectFuture(true); if (mode == "muessen") battle.SelectMuessen(true);
            battle.UseSkill1(); yield return Turn(battle);
            int normal = instance == a ? 15 : 14;
            int damage = mode == "future" ? BattleActionResolver.ScaleDamage(normal, 1.5f) : mode == "muessen" ? normal * 2 : normal;
            Assert.That(context.Enemy.HP, Is.EqualTo(200 - damage), instance + " " + mode);
            Assert.That(context.MuessenUsed, Is.EqualTo(mode == "muessen"));
            Assert.That(context.PendingAttack, Is.Null); Assert.That(context.TurnState, Is.EqualTo(BattleTurnState.AwaitingPlayer));
            yield return Load("GermanyHome");
        }
        Assert.That(CharacterInstances.Remove(a), Is.True);
        GameManager.SelectedCharacterInstanceId = a; yield return Load("Battle");
        Assert.That(Object.FindAnyObjectByType<BattleManager>().enabled, Is.False, "Another Nacht cannot substitute for a deleted identity.");
        Assert.That(CharacterInstances.Owns(b), Is.True);
    }
    [UnityTest] public IEnumerator DeletingAnInstanceWhileItsDetailIsOpenNeverCommitsIntoTheFallbackSelection()
    {
        yield return Detail(a); var detail = Object.FindAnyObjectByType<CharacterDetailManager>();
        Call(detail, "EquipCharacterEquipment", "schwarz"); Assert.That(CharacterInstances.Remove(a), Is.True);
        Assert.That(GameManager.SelectedCharacterInstanceId, Is.EqualTo(b));
        detail.ConfirmEquipment(); yield return null; yield return null;
        Assert.That(CharacterLoadoutStore.GetLoadout(b).adjectiveId, Is.Empty);
        Assert.That(CharacterLoadoutStore.CaptureState().characters, Is.Empty);
    }
}

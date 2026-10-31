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
        Assert.That(buttons.Length, Is.EqualTo(1));
        Assert.That(buttons[0].GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Nacht ×2"));
        Field<Button>(selection, "nextInstanceButton").onClick.Invoke();
        Assert.That(GameManager.SelectedCharacterInstanceId, Is.EqualTo(b));
        selection.CharacterDetail(); yield return null; yield return null;
        var detail = Object.FindAnyObjectByType<CharacterDetailManager>();
        Assert.That(Field<TMP_Text>(detail, "characterNameText").text, Is.EqualTo("Nacht"));
        Assert.That(Field<TMP_Text>(detail, "characterInfoText").text, Does.Contain("Individual " + CharacterInstances.DisplayBadge(b)));
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
            ui.SelectCharacterInstance(CharacterInstances.GetByCharacterId("wasser")[0].instanceId);
            ui.SelectCharacterInstance(CharacterInstances.GetByCharacterId("pistole")[0].instanceId);
            ui.CraftSelectedMaterials(); Assert.That(Field<GameObject>(ui,"characterDialog").activeSelf, Is.True);
            ui.AcceptDialog(); yield return null;
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
    [UnityTest] public IEnumerator GroupedListCyclesThreeRealIdsShowsGearAndStartsEachBattleWithThatIndividual()
    {
        string c = CharacterInstances.Create("nacht").instanceId;
        var ids = new[] { a,b,c };
        var gear = CharacterLoadoutStore.GetLoadout(a); gear.adjectiveId = "schwarz"; CharacterLoadoutStore.Commit(gear);
        gear = CharacterLoadoutStore.GetLoadout(b); gear.adjectiveId = "schnell"; CharacterLoadoutStore.Commit(gear);
        for (int index = 0; index < ids.Length; index++)
        {
            GameManager.SelectedCharacterInstanceId = a;
            yield return Load("CharacterSelect"); var ui = Object.FindAnyObjectByType<CharacterSelectManager>();
            Assert.That(Field<Transform>(ui,"characterButtonParent").GetComponentsInChildren<Button>().Length, Is.EqualTo(1));
            for (int n = 0; n < index; n++) Field<Button>(ui,"nextInstanceButton").onClick.Invoke();
            Assert.That(GameManager.SelectedCharacterInstanceId, Is.EqualTo(ids[index]));
            Assert.That(Field<TMP_Text>(ui,"instancePositionText").text, Is.EqualTo($"Individual {index+1} / 3"));
            string adjective = CharacterLoadoutStore.GetLoadout(ids[index]).adjectiveId;
            Assert.That(Field<TMP_Text>(ui,"selectedCharacterText").text, Does.Contain("Adjective: " +
                (string.IsNullOrEmpty(adjective) ? "none" : GameDatabase.Instance.GetEquipment(adjective).equipmentName)));
            Assert.That(Object.FindObjectsByType<TMP_Text>().Any(t => ids.Any(id => t.text.Contains(id))), Is.False);
            ui.StartBattle(); yield return null; yield return null;
            var battle = Object.FindAnyObjectByType<BattleManager>();
            Assert.That(battle.PlayerInstanceId, Is.EqualTo(ids[index]));
            Assert.That(battle.Context.Player.Character.equippedAdjectiveId, Is.EqualTo(adjective));
            yield return Load("GermanyHome");
        }
        GameManager.SelectedCharacterInstanceId = c; yield return Load("CharacterSelect");
        var select = Object.FindAnyObjectByType<CharacterSelectManager>();
        select.NextInstance(); Assert.That(GameManager.SelectedCharacterInstanceId, Is.EqualTo(a));
        select.PreviousInstance(); Assert.That(GameManager.SelectedCharacterInstanceId, Is.EqualTo(c));
        var reversed = CharacterInstances.CaptureState(); reversed.instances.Reverse();
        CharacterInstances.RestoreState(reversed); yield return null;
        Assert.That(GameManager.SelectedCharacterInstanceId, Is.EqualTo(c), "Reordering changes only the display ordinal.");
        Assert.That(Field<TMP_Text>(select,"instancePositionText").text, Is.EqualTo("Individual 1 / 3"));
        PlayerSaveService.Instance.SaveNow(); PlayerSaveService.Instance.Reload(); yield return null;
        Assert.That(CharacterInstances.GetAll().Select(x => x.instanceId), Is.EquivalentTo(ids));
        Assert.That(GameManager.SelectedCharacterInstanceId, Is.EqualTo(c));
        Assert.That(CharacterLoadoutStore.GetLoadout(a).adjectiveId, Is.EqualTo("schwarz"));
        Assert.That(CharacterLoadoutStore.GetLoadout(b).adjectiveId, Is.EqualTo("schnell"));
        CharacterInstances.Remove(c); yield return null;
        Assert.That(CharacterInstances.Owns(GameManager.SelectedCharacterInstanceId), Is.True);
        Assert.That(GameManager.SelectedCharacterInstanceId, Is.Not.EqualTo(c));
    }
    [UnityTest] public IEnumerator OneOrZeroIndividualsDisableCyclingAndCannotSelectUnknownOrDeletedIds()
    {
        CharacterInstances.Remove(b); yield return Load("CharacterSelect");
        var ui = Object.FindAnyObjectByType<CharacterSelectManager>();
        Assert.That(Field<TMP_Text>(ui,"instancePositionText").text, Is.EqualTo("Individual 1 / 1"));
        Assert.That(Field<Button>(ui,"previousInstanceButton").interactable, Is.False);
        Assert.That(Field<Button>(ui,"nextInstanceButton").interactable, Is.False);
        ui.NextInstance(); Call(ui,"SelectCharacter",b); Call(ui,"SelectSpecies","feuer");
        Assert.That(GameManager.SelectedCharacterInstanceId, Is.EqualTo(a));
        CharacterInstances.Remove(a); yield return null;
        Assert.That(Field<TMP_Text>(ui,"selectedCharacterText").text, Is.EqualTo("No owned characters."));
        Assert.That(Field<TMP_Text>(ui,"instancePositionText").text, Is.EqualTo("No individual"));
        Assert.That(Field<Transform>(ui,"characterButtonParent").GetComponentsInChildren<Button>(), Is.Empty);
        ui.StartBattle(); yield return null; Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CharacterSelect"));
    }
    [UnityTest] public IEnumerator GachaAwardsKeepCountsAndSharedLabelsSurviveInventoryCraftingAndSaveReload()
    {
        var items = JsonUtility.FromJson<GachaItemDatabase>(Resources.Load<TextAsset>("Data/gacha_items").text).items;
        foreach (var item in items) Assert.That(InventoryManager.AddItem(item.id,3), Is.True,item.id);
        PlayerSaveService.Instance.SaveNow(); PlayerSaveService.Instance.Reload();
        foreach (var item in items) Assert.That(InventoryManager.GetItemCount(item.id), Is.EqualTo(WordCharacterRewards.Character(item.id) == null ? 3 : 0));
        yield return Load("Gacha"); var gacha = Object.FindAnyObjectByType<GachaManager>();
        var randomState = UnityEngine.Random.state; UnityEngine.Random.InitState(914);
        int before = (InventoryManager.GetAllItems().Sum(i => i.count) + CharacterInstances.GetAll().Count);
        gacha.PullOnce(); yield return new WaitForSecondsRealtime(.3f); gacha.PullTen(); UnityEngine.Random.state = randomState;
        Assert.That((InventoryManager.GetAllItems().Sum(i => i.count) + CharacterInstances.GetAll().Count), Is.EqualTo(before+11));
        Assert.That(Field<TMP_Text>(gacha,"resultText").text, Does.Not.Contain("letter_").And.Not.Contain("chunk_"));
        yield return Load("Inventory"); var inventory = Object.FindAnyObjectByType<InventoryUIManager>();
        var names = Field<Transform>(inventory,"content").GetComponentsInChildren<TMP_Text>().Where(t => t.name == "ItemNameText").Select(t => t.text).ToArray();
        foreach (var item in items.Where(i => WordCharacterRewards.Character(i.id) == null)) Assert.That(names, Does.Contain(item.displayName));
        yield return Load("Crafting"); var crafting = Object.FindAnyObjectByType<CraftingUIManager>();
        foreach (string id in new[] { "letter_ae_umlaut","letter_oe_umlaut","letter_ue_umlaut","letter_eszett" }) Call(crafting,"SelectMaterial",id);
        Assert.That(Field<TMP_Text>(crafting,"selectedMaterialText").text, Is.EqualTo("ä + ö + ü + ß"));
        Assert.That(Field<TMP_Text>(crafting,"combinedWordText").text, Is.EqualTo("äöüß"));
        PlayerSaveService.Instance.SaveNow(); PlayerSaveService.Instance.Reload();
        Assert.That((InventoryManager.GetAllItems().Sum(i => i.count) + CharacterInstances.GetAll().Count), Is.EqualTo(before+11));
    }
    [UnityTest] public IEnumerator DirectWordRewardsAppearAsSelectableCharactersAndTenPullDuplicatesHaveDistinctIdentities()
    {
        yield return Load("Gacha"); var gacha=Object.FindAnyObjectByType<GachaManager>();
        var items=JsonUtility.FromJson<GachaItemDatabase>(Resources.Load<TextAsset>("Data/gacha_items").text).items;
        string[] ids={"word_nacht","word_nacht","word_nacht","word_wasser","word_wasser","word_wasser","word_pistole","word_pistole","letter_a","chunk_ss"};
        Assert.That(gacha.GrantDraws(ids.Select(id=>items.Single(i=>i.id==id)).ToArray(),true),Is.True);
        Assert.That(Field<TMP_Text>(gacha,"resultText").text,Does.Contain("Character obtained!"));
        Assert.That(CharacterInstances.Count("nacht"),Is.EqualTo(5)); Assert.That(CharacterInstances.Count("wasser"),Is.EqualTo(3));Assert.That(CharacterInstances.Count("pistole"),Is.EqualTo(2));
        Assert.That(InventoryManager.GetItemCount("word_nacht"),Is.Zero);Assert.That(CharacterInstances.GetAll().Select(i=>i.instanceId).Distinct().Count(),Is.EqualTo(10));
        PlayerSaveService.Instance.Reload(); yield return Load("CharacterSelect");
        var select=Object.FindAnyObjectByType<CharacterSelectManager>();
        var rows=Field<Transform>(select,"characterButtonParent").GetComponentsInChildren<Button>();
        Assert.That(rows.Length,Is.EqualTo(3));rows.Single(x=>x.name=="CharacterButton_wasser").onClick.Invoke();
        string first=GameManager.SelectedCharacterInstanceId;select.NextInstance();Assert.That(GameManager.SelectedCharacterInstanceId,Is.Not.EqualTo(first));
        Assert.That(Field<TMP_Text>(select,"instancePositionText").text,Is.EqualTo("Individual 2 / 3"));
        foreach (string type in new[] {"wasser","pistole"})
        {
            GameManager.SelectedCharacterInstanceId=CharacterInstances.GetByCharacterId(type)[0].instanceId;
            yield return Load("CharacterDetail"); var detail=Object.FindAnyObjectByType<CharacterDetailManager>();
            detail.StartBattle();yield return null;yield return null;var battle=Object.FindAnyObjectByType<BattleManager>();
            Assert.That(battle.Context.Player.Character.id,Is.EqualTo(type));Assert.That(battle.CanChoose,Is.True);
            typeof(BattleCombatant).GetProperty("HP").SetValue(battle.Context.Enemy,200);typeof(BattleCombatant).GetProperty("HP").SetValue(battle.Context.Player,200);
            Call(battle,"SelectNoAdverb");battle.UseSkill1();yield return Turn(battle);
            Assert.That(battle.Context.Enemy.HP,Is.LessThan(200));yield return Load("GermanyHome");
        }
    }
    [UnityTest] public IEnumerator CraftIndividualPickerAndConfirmationConsumeOnlyTheNamedCopyAndKeepDiscoveries()
    {
        InventoryManager.AddItem("word_wasser",2);InventoryManager.AddItem("word_pistole",1);
        var water=CharacterInstances.GetByCharacterId("wasser");string pistol=CharacterInstances.GetByCharacterId("pistole")[0].instanceId;
        var gear=CharacterLoadoutStore.GetLoadout(water[1].instanceId);gear.adjectiveId="schnell";CharacterLoadoutStore.Commit(gear);
        yield return Load("Crafting");var ui=Object.FindAnyObjectByType<CraftingUIManager>();
        Assert.That(Field<Button>(ui,"craftButton").interactable,Is.False);
        Field<Transform>(ui,"inventoryContent").GetComponentsInChildren<Button>().Single(x=>x.name=="CraftCharacter_wasser").onClick.Invoke();
        Assert.That(Field<GameObject>(ui,"characterDialog").activeSelf,Is.True);
        Field<Button>(ui,"dialogNext").onClick.Invoke();Assert.That(Field<TMP_Text>(ui,"dialogTitle").text,Does.Contain("2 / 2"));
        Assert.That(Field<TMP_Text>(ui,"dialogDetails").text,Does.Contain("schnell"));ui.AcceptDialog();
        ui.OpenIndividualPicker("pistole");ui.AcceptDialog();
        Assert.That(Field<TMP_Text>(ui,"combinedWordText").text,Is.EqualTo("Wasserpistole"));
        Assert.That(Field<TMP_Text>(ui,"resultText").text,Does.Contain("Wasserpistole (Character)"));
        Assert.That(Field<Button>(ui,"craftButton").interactable,Is.True);ui.CraftSelectedMaterials();
        Assert.That(CharacterInstances.Owns(water[1].instanceId),Is.True);Assert.That(CharacterInstances.Owns(pistol),Is.True);
        Assert.That(Field<TMP_Text>(ui,"dialogDetails").text,Does.Contain("2 / 2").And.Contain("schnell").And.Contain("Pistole"));
        ui.CancelDialog();Assert.That(CharacterInstances.Count("wasser"),Is.EqualTo(2));
        ui.CraftSelectedMaterials();ui.AcceptDialog();yield return null;
        Assert.That(CharacterInstances.Owns(water[0].instanceId),Is.True);Assert.That(CharacterInstances.Owns(water[1].instanceId),Is.False);
        Assert.That(CharacterInstances.Owns(pistol),Is.False);Assert.That(CharacterCollection.Status("pistole"),Is.EqualTo(CharacterCollectionStatus.DiscoveredNotOwned));
        var result=CharacterInstances.GetByCharacterId("wasserpistole").Single();Assert.That(CharacterLoadoutStore.GetLoadout(result.instanceId).adjectiveId,Is.Empty);
        Assert.That(Field<Transform>(ui,"inventoryContent").GetComponentsInChildren<Button>().Any(x=>x.name=="CraftCharacter_wasserpistole"),Is.True);
        PlayerSaveService.Instance.Reload();Assert.That(CharacterInstances.Owns(result.instanceId),Is.True);
    }
    [UnityTest] public IEnumerator StaleCraftConfirmationFailsWithoutConsumingOtherInputsAndUndoMaintainsOrder()
    {
        InventoryManager.AddItem("word_wasser",1);InventoryManager.AddItem("word_pistole",1);
        string water=CharacterInstances.GetByCharacterId("wasser")[0].instanceId,pistol=CharacterInstances.GetByCharacterId("pistole")[0].instanceId;
        yield return Load("Crafting");var ui=Object.FindAnyObjectByType<CraftingUIManager>();
        ui.SelectCharacterInstance(water);ui.SelectCharacterInstance(water);ui.SelectCharacterInstance(pistol);
        Assert.That(Field<TMP_Text>(ui,"selectedMaterialText").text.Split('+').Length,Is.EqualTo(2));
        ui.UndoLast();Assert.That(Field<Button>(ui,"craftButton").interactable,Is.True,"A single existing word remains a dictionary match.");
        ui.SelectCharacterInstance(pistol);ui.CraftSelectedMaterials();CharacterInstances.Remove(pistol);
        ui.AcceptDialog();yield return null;Assert.That(CharacterInstances.Owns(water),Is.True);Assert.That(CharacterInstances.Count("wasserpistole"),Is.Zero);
        Assert.That(Field<TMP_Text>(ui,"resultText").text,Does.Contain("nothing consumed"));ui.ClearSelection();Assert.That(Field<Button>(ui,"craftButton").interactable,Is.False);
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

[PrebuildSetup(typeof(TestSaveIsolation))]
[PostBuildCleanup(typeof(TestSaveIsolation))]
public class BattleModifierSceneTests
{
    private static string Instance(string species)
    {
        var instances = CharacterInstances.GetByCharacterId(species);
        return instances.Count > 0 ? instances[0].instanceId : CharacterInstances.Create(species).instanceId;
    }

    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == name);
    private float oldScale;
    [UnitySetUp] public IEnumerator Setup()
    {
        oldScale = Time.timeScale; Time.timeScale = 8;
        GameDatabase.EnsureAvailable();
        var owned = new OwnedCharactersState(); owned.characterIds.Add("nacht"); OwnedCharacters.RestoreState(owned);
        CharacterLoadoutStore.RestoreState(new CharacterLoadoutState());
        GameManager.SelectedCharacterId = "nacht"; GameManager.EnemyCharacterId = "feuer";
        SceneManager.LoadScene("Battle"); yield return null; yield return null;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        Time.timeScale = oldScale;
        SceneManager.LoadScene("GermanyHome"); yield return null;
    }
    private static void HP(BattleCombatant combatant, int hp) => typeof(BattleCombatant).GetProperty("HP").SetValue(combatant, hp);
    private IEnumerator WaitForTurn(BattleManager battle)
    {
        float end = Time.realtimeSinceStartup + 18;
        while (battle != null && Field<bool>(battle, "isProcessing") && Time.realtimeSinceStartup < end) yield return null;
        Assert.That(battle == null || !Field<bool>(battle, "isProcessing"), Is.True, "Turn completes within timeout.");
    }
    [UnityTest] public IEnumerator FutureHasZeroReservationDamageThenFiresBeforeNormalInputAndAllowsAnotherAction()
    {
        var battle = Object.FindAnyObjectByType<BattleManager>(); var context = battle.Context;
        HP(context.Enemy, 200); HP(context.Player, 200);
        Button("FutureButton").onClick.Invoke();
        Assert.That(battle.Selection.Tense, Is.EqualTo(BattleTense.Future));
        Assert.That(Button("MuessenButton").interactable, Is.False);
        Assert.That(battle.SelectMuessen(true), Is.False);
        battle.UseSkill1();
        float end = Time.realtimeSinceStartup + 10;
        while (context.PendingAttack == null && Time.realtimeSinceStartup < end) yield return null;
        Assert.That(context.PendingAttack, Is.Not.Null); Assert.That(context.Enemy.HP, Is.EqualTo(200));
        Assert.That(battle.CanChoose, Is.False); Assert.That(Button("FutureButton").interactable, Is.False);
        Assert.That(GameObject.Find("ModifierStatus").GetComponent<TMP_Text>().text, Does.Contain("21 damage next turn"));
        yield return WaitForTurn(battle);
        Assert.That(context.Enemy.HP, Is.EqualTo(179)); Assert.That(context.PendingAttack, Is.Null);
        Assert.That(context.Round, Is.EqualTo(2)); Assert.That(battle.CanChoose, Is.True);
        Assert.That(battle.LastCommittedAction.Selection.Elements[BattleElementKind.Tense], Is.EqualTo("futur1"));
        battle.UseSkill1(); yield return WaitForTurn(battle);
        Assert.That(context.Enemy.HP, Is.EqualTo(165)); Assert.That(context.Round, Is.EqualTo(3));
    }
    [UnityTest] public IEnumerator FutureKnockoutAutomaticallyLoadsVictoryWithoutAskingForAnAction()
    {
        var battle = Object.FindAnyObjectByType<BattleManager>(); var context = battle.Context;
        battle.SelectFuture(true); battle.UseSkill1(); yield return WaitForTurn(battle);
        Assert.That(context.Outcome, Is.EqualTo(BattleOutcome.PlayerWin));
        Assert.That(context.Round, Is.EqualTo(2)); Assert.That(context.PendingAttack, Is.Null);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Result")); Assert.That(GameManager.IsWin, Is.True);
    }
    [UnityTest] public IEnumerator DefeatCancelsFutureBeforeItCanFire()
    {
        var battle = Object.FindAnyObjectByType<BattleManager>(); var context = battle.Context; HP(context.Player, 1);
        battle.SelectFuture(true); battle.UseSkill1(); yield return WaitForTurn(battle);
        Assert.That(context.Outcome, Is.EqualTo(BattleOutcome.EnemyWin)); Assert.That(context.PendingAttack, Is.Null);
        Assert.That(context.Enemy.HP, Is.EqualTo(18)); Assert.That(GameManager.IsWin, Is.False);
    }
    [UnityTest] public IEnumerator MuessenUsedIndicatorAndNewBattleResetAreVisible()
    {
        var battle = Object.FindAnyObjectByType<BattleManager>(); HP(battle.Context.Enemy, 200); HP(battle.Context.Player, 200);
        Button("MuessenButton").onClick.Invoke(); Assert.That(Button("FutureButton").interactable, Is.False);
        Assert.That(battle.SelectFuture(true), Is.False);
        battle.UseSkill1(); yield return WaitForTurn(battle);
        Assert.That(battle.Context.Enemy.HP, Is.EqualTo(172)); Assert.That(battle.Context.MuessenUsed, Is.True);
        Assert.That(Button("MuessenButton").interactable, Is.False);
        Assert.That(Button("MuessenButton").GetComponentInChildren<TMP_Text>().text, Does.Contain("USED"));
        Assert.That(GameObject.Find("ModifierStatus").GetComponent<TMP_Text>().text, Does.Contain("USED"));
        Assert.That(battle.SelectMuessen(true), Is.False);
        SceneManager.LoadScene("Battle"); yield return null; yield return null;
        battle = Object.FindAnyObjectByType<BattleManager>();
        Assert.That(battle.Context.MuessenUsed, Is.False); Assert.That(Button("MuessenButton").interactable, Is.True);
    }
    [UnityTest] public IEnumerator UiAllowsChangingAndClearingEveryOptionalModifierAndRecordsIntent()
    {
        var battle = Object.FindAnyObjectByType<BattleManager>();
        var cardButtons = Field<Button[]>(battle, "adverbCardButtons");
        cardButtons[0].onClick.Invoke(); string adverb = battle.Selection.Get(BattleElementKind.Adverb);
        Assert.That(adverb, Is.Not.Null);
        Field<Button>(battle, "noAdverbButton").onClick.Invoke(); Assert.That(battle.Selection.Get(BattleElementKind.Adverb), Is.Null);
        Button("FutureButton").onClick.Invoke(); Button("NoTenseButton").onClick.Invoke();
        Assert.That(battle.Selection.Tense, Is.EqualTo(BattleTense.Present));
        Assert.That(Button("MuessenButton").interactable, Is.True);
        Button("MuessenButton").onClick.Invoke(); Button("NoModalButton").onClick.Invoke();
        Assert.That(battle.Selection.Modal, Is.EqualTo(BattleModal.None));
        cardButtons[0].onClick.Invoke(); Button("FutureButton").onClick.Invoke(); Button("ClearModifiersButton").onClick.Invoke();
        Assert.That(battle.Selection.Modal, Is.EqualTo(BattleModal.None));
        Assert.That(battle.Selection.Tense, Is.EqualTo(BattleTense.Present));
        Assert.That(battle.Selection.Get(BattleElementKind.Adverb), Is.Null);
        var history = battle.Selection.Commit("sein").History;
        Assert.That(history.Any(e => e.Change == BattleSelectionChange.Cleared), Is.True);
        Assert.That(history.Last().Kind, Is.EqualTo(BattleElementKind.Skill));
        Assert.That(GameObject.Find("SelectionSummary").GetComponent<TMP_Text>().text, Does.Contain("Adverb: none"));
        yield return null;
    }
    [UnityTest] public IEnumerator TransientModifiersNeverChangeSavedLoadoutsInventoryOwnershipOrSaveFormat()
    {
        var draft = CharacterLoadoutStore.GetLoadout(Instance("nacht")); draft.adjectiveId = "schwarz";
        draft.skillSlots[0].equippedAdverbId = "sehr"; CharacterLoadoutStore.Commit(draft);
        var service = PlayerSaveService.Instance;
        string before = JsonUtility.ToJson(service.Capture());
        var battle = Object.FindAnyObjectByType<BattleManager>(); HP(battle.Context.Enemy, 200); HP(battle.Context.Player, 200);
        battle.SelectMuessen(true); battle.UseSkill1(); yield return WaitForTurn(battle);
        Assert.That(battle.Context.MuessenUsed, Is.True);
        Assert.That(JsonUtility.ToJson(service.Capture()), Is.EqualTo(before));
        Assert.That(service.SaveNow(), Is.True); service.Reload();
        Assert.That(JsonUtility.ToJson(service.Capture()), Is.EqualTo(before));
        Assert.That(before, Does.Not.Contain("MuessenUsed").And.Not.Contain("PendingAttack").And.Not.Contain("futur1"));
        SceneManager.LoadScene("Battle"); yield return null; yield return null;
        battle = Object.FindAnyObjectByType<BattleManager>();
        Assert.That(battle.Context.MuessenUsed, Is.False); Assert.That(battle.Context.PendingAttack, Is.Null);
        Assert.That(CharacterLoadoutStore.GetLoadout(Instance("nacht")).adjectiveId, Is.EqualTo("schwarz"));
        Assert.That(CharacterLoadoutStore.GetLoadout(Instance("nacht")).skillSlots[0].equippedAdverbId, Is.EqualTo("sehr"));
    }
    [UnityTest] public IEnumerator NewControlsAndExistingCommandsFitAndReceivePointerInputAtAllRequiredResolutions()
    {
        var battle = Object.FindAnyObjectByType<BattleManager>();
        foreach (var resolution in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1600,1200), new Vector2Int(2560,1080) })
        {
            Screen.SetResolution(resolution.x, resolution.y, false); yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b => b.IsActive() && b.interactable))
            {
                var rect = (RectTransform)button.transform; var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                Assert.That(corners.All(p => p.x >= -1 && p.x <= Screen.width + 1 && p.y >= -1 && p.y <= Screen.height + 1), Is.True, resolution + " bounds " + button.name);
                var pointer = new PointerEventData(EventSystem.current) { position = rect.TransformPoint(rect.rect.center) };
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, Is.True, resolution + " pointer " + button.name);
            }
            battle.OpenSkillPanel(); yield return null; yield return null;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Field<GameObject>(battle, "skillPanel").transform);
            Canvas.ForceUpdateCanvases();
            foreach (var button in Field<GameObject[]>(battle, "skillButtons").Where(b => b.activeInHierarchy))
            {
                var rect = (RectTransform)button.transform;
                var pointer = new PointerEventData(EventSystem.current) { position = rect.TransformPoint(rect.rect.center) };
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>().gameObject == button, Is.True, resolution + " skill " + button.name + " hits=" + string.Join(",", hits.Select(h => h.gameObject.name)));
            }
            battle.CloseSkillPanel();
        }
    }
}

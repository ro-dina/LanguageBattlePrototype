// Copy to Assets in a disposable validation project; this harness changes test state only.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class UIPlayValidation : MonoBehaviour
{
    private readonly List<string> failures = new List<string>();
    private readonly HashSet<string> warnings = new HashSet<string>();
    private readonly HashSet<string> editorDiagnostics = new HashSet<string>();
    private readonly List<string> passes = new List<string>();
    private readonly string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../results"));
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private void Awake()
    {
        Application.runInBackground = true;
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory(output);
        Application.logMessageReceived += OnLog;
    }

    private void Start()
    {
        Debug.Log("UI_VALIDATE_START");
        StartCoroutine(SafeRun());
    }

    private void OnLog(string message, string stack, LogType type)
    {
        if (stack.Contains("UnityEditor.Search.SearchDatabase"))
        {
            editorDiagnostics.Add(message + "\n" + stack);
            return;
        }
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            failures.Add(message + "\n" + stack);
        else if (type == LogType.Warning) warnings.Add(message);
    }

    private IEnumerator SafeRun()
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(Validate());
        while (stack.Count > 0)
        {
            object current = null;
            bool more = false;
            try { more = stack.Peek().MoveNext(); if (more) current = stack.Peek().Current; }
            catch (Exception exception) { failures.Add(exception.ToString()); break; }
            if (!more) { stack.Pop(); continue; }
            if (current is IEnumerator nested) stack.Push(nested);
            else yield return current;
        }
        File.WriteAllText(Path.Combine(output, "validation.txt"),
            "PASSES\n" + string.Join("\n", passes) + "\nFAILURES\n" + string.Join("\n", failures) +
            "\nWARNINGS\n" + string.Join("\n", warnings) + "\nEDITOR_DIAGNOSTICS\n" + string.Join("\n", editorDiagnostics));
        Debug.Log($"UI_VALIDATION_COMPLETE: {passes.Count} passes, {failures.Count} failures, {warnings.Count} warning types.");
        EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
    }

    private void Check(bool condition, string description)
    {
        if (condition) passes.Add(description);
        else failures.Add(description);
    }

    private static T Find<T>() where T : Object => Object.FindAnyObjectByType<T>();
    private static T Field<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    private static Button ButtonNamed(string name) => Object.FindObjectsByType<Button>().First(b => b.name == name);
    private void Click(string name)
    {
        Canvas.ForceUpdateCanvases();
        var button = ButtonNamed(name);
        var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var rect = (RectTransform)button.transform;
        var scroll=button.GetComponentInParent<ScrollRect>();
        if(scroll!=null && scroll.viewport!=null && scroll.content!=null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);Canvas.ForceUpdateCanvases();scroll.StopMovement();
            var point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
            if(!RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport,point,camera))
            {
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,rect);var position=scroll.content.anchoredPosition;
                float delta=bounds.min.y<scroll.viewport.rect.yMin?scroll.viewport.rect.yMin+8-bounds.min.y:scroll.viewport.rect.yMax-8-bounds.max.y;
                position.y=Mathf.Clamp(position.y+delta,0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height));scroll.content.anchoredPosition=position;Canvas.ForceUpdateCanvases();
            }
        }
        var pointer = new PointerEventData(EventSystem.current) {
            position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center))
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
            SceneManager.GetActiveScene().name + " raycast reaches " + name);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    private static int ActiveChildren(Transform parent) => parent.Cast<Transform>().Count(t => t.gameObject.activeSelf);

    private IEnumerator Load(string scene)
    {
        Debug.Log("UI_VALIDATE_LOAD " + scene);
        SceneManager.LoadScene(scene);
        yield return null; yield return null;
        Canvas.ForceUpdateCanvases();
    }

    private IEnumerator Validate()
    {
        RewardGrantService.Grant(new[] {new InventoryEntry("word_nacht",26),new InventoryEntry("word_wasser",37),new InventoryEntry("word_pistole",42)},out _);
        CharacterInstances.Create("hund"); CharacterInstances.Create("hund");
        var discoveredFire = CharacterInstances.Create("feuer"); CharacterInstances.Remove(discoveredFire.instanceId);
        foreach (var item in JsonUtility.FromJson<GachaItemDatabase>(Resources.Load<TextAsset>("Data/gacha_items").text).items)
            if (WordCharacterRewards.Character(item.id) == null) InventoryManager.AddItem(item.id, 5);

        foreach (string scene in new[] { "GermanyHome", "StageSelect", "CharacterSelect", "CharacterCollection", "CharacterDetail", "Gacha", "Inventory", "Crafting", "Battle", "Result" })
        {
            GameManager.PreviousSceneName = "CharacterSelect";
            GameManager.SelectedCharacterId = "nacht";
            GameManager.EnemyCharacterId = "feuer";
            yield return Load(scene);
            if (scene == "Gacha") Click("PullTenButton");
            if (scene == "Crafting")
            {
                var crafting = Find<CraftingUIManager>();
                crafting.SelectCharacterInstance(CharacterInstances.GetByCharacterId("wasser")[0].instanceId);
                crafting.SelectCharacterInstance(CharacterInstances.GetByCharacterId("pistole")[0].instanceId);
            }
            var walletHUD = Find<CurrencyHUD>();
            if (scene != "Battle" && scene != "Result")
                Check(walletHUD != null && Field<TMP_Text>(walletHUD,"balanceText").text == EconomySettings.Price(CurrencyService.Balance),scene + " displays current saved currency");
            yield return RenderSizes(scene);
        }

        GameManager.SelectedCharacterInstanceId = CharacterInstances.GetByCharacterId("nacht")[0].instanceId;
        yield return Load("CharacterSelect");
        var copies = CharacterInstances.GetByCharacterId("nacht");
        var select = Find<CharacterSelectManager>();
        var list = Field<Transform>(select, "characterButtonParent");
        Check(list.GetComponentsInChildren<Button>().Length == CharacterInstances.GetAll().Select(i => i.characterId).Distinct().Count(),
            "Selection groups owned individuals by species");
        Check(list.GetComponentsInChildren<Button>().Single(b => b.name == "CharacterButton_nacht").GetComponentInChildren<TMP_Text>().text == "Nacht ×" + CharacterInstances.Count("nacht"),
            "Species row groups dozens of Nacht individuals");
        Click("CharacterButton_nacht"); Click("NextInstanceButton");
        Check(GameManager.SelectedCharacterInstanceId == copies[1].instanceId, "Pointer selects the second Nacht identity");
        Check(Field<TMP_Text>(select,"instancePositionText").text == "Individual 2 / " + copies.Count, "Ordinal identifies the second of three individuals");
        yield return RenderSizes("CharacterSelect-instanceB");
        Click("CharacterListButton"); yield return null; yield return null;
        var secondDetail = Find<CharacterDetailManager>();
        Call(secondDetail, "EquipCharacterEquipment", "schnell");
        yield return RenderSizes("CharacterDetail-instanceB");
        secondDetail.Back(); yield return null; yield return null; Click("SaveButton"); yield return null; yield return null;
        Check(CharacterLoadoutStore.GetLoadout(copies[1].instanceId).adjectiveId == "schnell" &&
            string.IsNullOrEmpty(CharacterLoadoutStore.GetLoadout(copies[0].instanceId).adjectiveId),
            "Saving the second Nacht leaves the first Nacht equipment unchanged");

        yield return Load("CharacterSelect");
        Click("NextInstanceButton");
        Check(GameManager.SelectedCharacterInstanceId == copies[2].instanceId, "Pointer selects the third Nacht");
        yield return RenderSizes("CharacterSelect-instanceC");
        GameManager.SelectedCharacterInstanceId = copies.Last().instanceId;
        yield return Load("CharacterSelect");
        Click("NextInstanceButton"); Check(GameManager.SelectedCharacterInstanceId == copies[0].instanceId, "Large individual list wraps to its first ID");
        Click("PreviousInstanceButton"); Check(GameManager.SelectedCharacterInstanceId == copies.Last().instanceId, "Previous wraps to the last owned ID");

        yield return Load("Inventory");
        var inventory = Find<InventoryUIManager>();
        var content = Field<Transform>(inventory, "content");
        inventory.RefreshInventory(); inventory.RefreshInventory();
        yield return null;
        Check(content != null && content.parent.name == "Viewport", "Inventory content survives repeated refresh and remains below Viewport");
        Check(ActiveChildren(content) == InventoryManager.GetAllItems().Count, "Inventory repeated refresh has one row per item");
        var inventoryLabels = content.GetComponentsInChildren<TMP_Text>().Where(t => t.name == "ItemNameText").Select(t => t.text).ToArray();
        Check(inventoryLabels.Contains("ä") && inventoryLabels.Contains("ö") && inventoryLabels.Contains("ü") && inventoryLabels.Contains("ß") && !inventoryLabels.Any(t => t.StartsWith("letter_")), "Inventory displays German characters rather than internal IDs");
        content.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 0; yield return null; yield return null;
        yield return RenderSizes("Inventory-umlauts");
        Click("BackButton"); yield return null;
        Check(SceneManager.GetActiveScene().name == "GermanyHome", "Inventory Back returns home");

        yield return Load("Crafting");
        var craft = Find<CraftingUIManager>();
        var materials = Field<Transform>(craft,"inventoryContent");
        craft.RefreshInventory(); craft.RefreshInventory(); yield return null; yield return null;
        Check(materials.parent.name == "Viewport" && ActiveChildren(materials) == InventoryManager.GetAllItems().Count + CharacterInstances.GetAll().Select(i=>i.characterId).Distinct().Count(),
            "Crafting lists materials and grouped characters without duplicate rows");
        string water = CharacterInstances.GetByCharacterId("wasser")[1].instanceId;
        string spareWater = CharacterInstances.GetByCharacterId("wasser")[0].instanceId;
        string pistol = CharacterInstances.GetByCharacterId("pistole")[0].instanceId;
        var waterGear = CharacterLoadoutStore.GetLoadout(water); waterGear.adjectiveId = "schwarz"; CharacterLoadoutStore.Commit(waterGear);
        int waterBefore = CharacterInstances.Count("wasser"), pistolBefore = CharacterInstances.Count("pistole");
        yield return null; yield return null;
        materials.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 0; yield return null; yield return null;
        Click("CraftCharacter_wasser"); yield return null; yield return null;
        Click("CraftNextIndividual");
        Check(Field<TMP_Text>(craft,"dialogTitle").text.Contains("2 / ") && Field<TMP_Text>(craft,"dialogDetails").text.Contains("schwarz"),
            "Individual picker identifies the selected copy and its equipment");
        yield return RenderSizes("Crafting-individual-picker");
        Click("CraftDialogAccept"); yield return null; yield return null;
        Click("CraftCharacter_pistole"); yield return null; yield return null; Click("CraftDialogAccept");
        Check(Field<TMP_Text>(craft,"combinedWordText").text == "Wasserpistole" && Field<Button>(craft,"craftButton").interactable,
            "Ordered character concatenation previews a dictionary match");
        yield return RenderSizes("Crafting-dictionary-match");
        Click("ClearButton"); Check(Field<TMP_Text>(craft,"selectedMaterialText").text == "", "Clear empties selected crafting elements");
        craft.SelectCharacterInstance(water); craft.SelectCharacterInstance(pistol);
        Click("CraftButton"); yield return null; yield return null;
        Check(CharacterInstances.Owns(water) && CharacterInstances.Owns(pistol), "Opening confirmation consumes nothing");
        yield return RenderSizes("Crafting-consumption-confirm");
        Click("CraftDialogCancel"); Check(CharacterInstances.Owns(water), "Cancel retains the selected equipped individual");
        Click("CraftButton"); yield return null; yield return null; Click("CraftDialogAccept"); yield return null; yield return null;
        Check(!CharacterInstances.Owns(water) && !CharacterInstances.Owns(pistol) && CharacterInstances.Owns(spareWater) &&
            CharacterInstances.Count("wasser") == waterBefore-1 && CharacterInstances.Count("pistole") == pistolBefore-1,
            "Confirmed craft consumes only explicit individuals and retains their same-species siblings");
        craft.SelectCharacterInstance(CharacterInstances.GetByCharacterId("wasser")[0].instanceId);
        craft.SelectCharacterInstance(CharacterInstances.GetByCharacterId("pistole")[0].instanceId);
        Click("CraftButton"); yield return null; yield return null; Click("CraftDialogAccept"); yield return null; yield return null;
        Check(CharacterInstances.Count("wasserpistole") == 2 && CharacterInstances.GetByCharacterId("wasserpistole").Select(i=>i.instanceId).Distinct().Count()==2,
            "Dictionary crafting creates a second distinct result");
        yield return RenderSizes("Crafting-repeat");

        yield return Load("CharacterCollection");
        var collection = Find<CharacterCollectionManager>();
        var collectionLabels = Field<Transform>(collection, "characterButtonParent").GetComponentsInChildren<Button>().Select(b => b.GetComponentInChildren<TMP_Text>().text).ToArray();
        Check(collectionLabels.Contains("Nacht [Owned: " + CharacterInstances.Count("nacht") + "]") && collectionLabels.Contains("Feuer [Discovered • 0 owned]") &&
            collectionLabels.Any(t => t.Contains("Undiscovered")), "Collection distinguishes owned, discovered-not-owned and undiscovered species");
        var search = Field<TMP_InputField>(collection, "searchInputField");
        search.text = "nacht"; yield return null;
        Check(ActiveChildren(Field<Transform>(collection, "characterButtonParent")) == 1, "Character search filters without duplicate rows");
        Field<Transform>(collection, "characterButtonParent").GetComponentsInChildren<Button>().Single().onClick.Invoke();
        Click("DetailButton"); yield return null; yield return null;
        Check(SceneManager.GetActiveScene().name == "CharacterDetail", "Collection selection opens detail");
        var detail = Find<CharacterDetailManager>();
        Check(ButtonNamed("BattleButton").GetComponentInChildren<TMP_Text>().text == "Confirm", "Collection detail shows Confirm");
        Check(ButtonNamed("BattleButton").onClick.GetPersistentEventCount() == 0, "Mode-specific Confirm clears the persistent StartBattle callback");
        detail.OpenAdjectiveEquipmentList(); detail.OpenAdjectiveEquipmentList(); yield return null;
        var equipmentParent = Field<Transform>(detail, "characterEquipmentButtonParent");
        Check(equipmentParent.GetComponentsInChildren<Button>().Select(b => b.GetComponentInChildren<TMP_Text>().text).Distinct().Count() == ActiveChildren(equipmentParent), "Reopened equipment list has no duplicate active entries");
        yield return RenderSizes("CharacterDetail-equipment");
        equipmentParent.GetComponentsInChildren<Button>()[0].onClick.Invoke();
        Check(!Field<GameObject>(detail, "characterEquipmentPanel").activeSelf, "Choosing equipment closes its panel");
        detail.Back();
        Check(Field<GameObject>(detail, "unsavedConfirmPanel").activeSelf, "Back with changes opens the save dialog");
        yield return RenderSizes("CharacterDetail-unsaved");
        Click("CancelButton");
        Check(!Field<GameObject>(detail, "unsavedConfirmPanel").activeSelf, "Cancel closes the save dialog");
        detail.OpenSkillEquipmentList(0); detail.OpenSkillEquipmentList(0); yield return null;
        Check(Field<Transform>(detail, "skillEquipmentButtonParent").GetComponent<VerticalLayoutGroup>() != null, "Skill equipment uses a vertical layout");
        yield return RenderSizes("CharacterDetail-skills");
        Field<Transform>(detail, "skillEquipmentButtonParent").GetComponentsInChildren<Button>()[0].onClick.Invoke();
        Check(!Field<GameObject>(detail, "skillEquipmentPanel").activeSelf, "Choosing skill equipment closes its panel");
        Click("BattleButton"); yield return null; yield return null;
        Check(SceneManager.GetActiveScene().name == "CharacterCollection", "Confirm from collection returns to collection without starting battle");

        yield return Load("CharacterSelect");
        Click("CharacterListButton"); yield return null; yield return null;
        Check(ButtonNamed("BattleButton").GetComponentInChildren<TMP_Text>().text == "Battle", "Selection detail retains Battle mode");
        Click("BattleButton"); yield return null; yield return null;
        Check(SceneManager.GetActiveScene().name == "Battle", "Battle button still starts battle");
        Click("Fight");
        Check(Field<GameObject>(Find<BattleManager>(), "skillPanel").activeSelf, "Fight opens skill commands");
        yield return RenderSizes("Battle-commands");
        Click("back");
        Check(!Field<GameObject>(Find<BattleManager>(), "skillPanel").activeSelf, "Back closes skill commands");
        Click("FutureButton");
        Check(Find<BattleManager>().Selection.Tense == BattleTense.Future && ButtonNamed("MuessenButton").interactable,
            "Future selection leaves müssen available for switching");
        yield return RenderSizes("Battle-future-selected");
        Click("FutureButton"); Check(Find<BattleManager>().Selection.Tense == BattleTense.Present, "Second future click switches it off");
        Click("FutureButton"); Click("MuessenButton");
        Check(Find<BattleManager>().Selection.Modal == BattleModal.Muessen && Find<BattleManager>().Selection.Tense == BattleTense.Present && ButtonNamed("FutureButton").interactable,
            "Müssen switches future off while leaving future available");
        yield return RenderSizes("Battle-muessen-selected");
        Click("MuessenButton"); Check(Find<BattleManager>().Selection.Modal == BattleModal.None && !Find<BattleManager>().Context.MuessenUsed, "Second müssen click switches it off without spending the use");
        Click("MuessenButton"); Click("FutureButton");
        Check(Find<BattleManager>().Selection.Tense == BattleTense.Future && Find<BattleManager>().Selection.Modal == BattleModal.None, "Future switches müssen off");
        Click("ClearModifiersButton");
        Check(Find<BattleManager>().Selection.Tense == BattleTense.Present && Find<BattleManager>().Selection.Modal == BattleModal.None,
            "Clear modifiers restores an ordinary skill command");
        Click("NoAdverbButton"); Click("Fight"); yield return null; yield return null; Click("SkillButton1");
        Check(Field<bool>(Find<BattleManager>(), "isProcessing"), "Skill command starts the existing battle turn");
        float deadline = Time.realtimeSinceStartup + 25;
        while (Find<BattleManager>() != null && Field<bool>(Find<BattleManager>(), "isProcessing") && Time.realtimeSinceStartup < deadline) yield return null;
        Check(Find<BattleManager>() == null || !Field<bool>(Find<BattleManager>(), "isProcessing"), "Battle turn completes without getting stuck");
        yield return Load("Battle");
        var futureBattle = Find<BattleManager>(); var futureContext = futureBattle.Context;
        Click("NoAdverbButton"); Click("FutureButton"); Click("Fight"); yield return null; yield return null; Click("SkillButton1");
        deadline = Time.realtimeSinceStartup + 20;
        while (futureContext.PendingAttack == null && futureContext.Outcome == BattleOutcome.Ongoing && Time.realtimeSinceStartup < deadline) yield return null;
        Check(futureContext.PendingAttack != null && futureContext.Enemy.HP == futureContext.Enemy.Character.hp,
            "Future reservation causes zero immediate enemy damage");
        Time.timeScale = 0;
        yield return RenderSizes("Battle-reserved");
        Check(!ButtonNamed("FutureButton").interactable && !ButtonNamed("MuessenButton").interactable,
            "Modifier input stays locked while resolving the reserved turn");
        Time.timeScale = 1;
        deadline = Time.realtimeSinceStartup + 20;
        while (Find<BattleManager>() != null && Field<bool>(Find<BattleManager>(), "isProcessing") && Time.realtimeSinceStartup < deadline) yield return null;
        yield return null;
        Check(futureContext.Outcome == BattleOutcome.PlayerWin && futureContext.PendingAttack == null && SceneManager.GetActiveScene().name == "Result",
            "Reserved knockout enters Result without requesting another skill");
        yield return Load("Battle");
        var muessenBattle = Find<BattleManager>();
        typeof(BattleCombatant).GetProperty("HP").SetValue(muessenBattle.Context.Enemy, 200);
        Click("NoAdverbButton"); Click("MuessenButton"); Click("Fight"); yield return null; yield return null; Click("SkillButton1");
        deadline = Time.realtimeSinceStartup + 20;
        while (Field<bool>(muessenBattle, "isProcessing") && Time.realtimeSinceStartup < deadline) yield return null;
        Check(muessenBattle.Context.MuessenUsed && !ButtonNamed("MuessenButton").interactable &&
            ButtonNamed("MuessenButton").GetComponentInChildren<TMP_Text>().text.Contains("USED"), "Müssen used state is visible and unavailable");
        yield return RenderSizes("Battle-muessen-used");
        // Test direct entry after destroying the persistent database.
        if (GameDatabase.Instance != null) Destroy(GameDatabase.Instance.gameObject);
        yield return null;
        yield return Load("CharacterDetail");
        Check(GameDatabase.Instance != null && Field<CharacterData>(Find<CharacterDetailManager>(), "character") != null, "CharacterDetail can start directly with its scene database");
        yield return Load("GermanyHome");
        var money = Find<CurrencyHUD>(); long balanceBefore = CurrencyService.Balance;
        Click("CoinsPlusButton"); yield return RenderSizes("Currency-add-dialog");
        Click("AddCoins0"); Check(CurrencyService.Balance == balanceBefore+100,"Development +100 commits once");
        Check(!money.AddDevelopmentCoins(0),"Immediate duplicate development click is ignored");
        yield return new WaitForSecondsRealtime(.3f); Click("AddCoins1");
        yield return new WaitForSecondsRealtime(.3f); Click("AddCoins2");
        Check(CurrencyService.Balance == balanceBefore+11100,"Development grants all use the shared wallet");
        Click("CloseCoinDialog"); PlayerSaveService.Instance.Reload();
        Check(Field<TMP_Text>(money,"balanceText").text == EconomySettings.Price(CurrencyService.Balance),"Currency survives reload and is formatted");
        CurrencyService.TryAdd(long.MaxValue-CurrencyService.Balance,out _);
        yield return RenderSizes("Currency-long-balance");
        CurrencyService.TrySpend(CurrencyService.Balance,out _);
        yield return Load("Gacha");
        Check(!ButtonNamed("PullButton").interactable && !ButtonNamed("PullTenButton").interactable,"Insufficient funds disable both gacha commands");
        Check(Field<TMP_Text>(Find<GachaManager>(),"costText").text.Contains("Not enough Coins"),"Gacha explains insufficient currency");
        yield return RenderSizes("Gacha-insufficient");
        yield return Load("Crafting");
        var paidCraft = Find<CraftingUIManager>();
        foreach (string id in new[] {"letter_f","letter_e","letter_u","letter_e","letter_r"}) Call(paidCraft,"SelectMaterial",id);
        Check(!Field<Button>(paidCraft,"craftButton").interactable && Field<TMP_Text>(paidCraft,"resultText").text.Contains("Not enough Coins"),"Dictionary match with no funds is disabled and explained");
        yield return RenderSizes("Crafting-insufficient");
        CurrencyService.TryAdd(200,out _); Click("CraftButton"); yield return null; yield return null;
        Check(Field<TMP_Text>(paidCraft,"dialogDetails").text.Contains("Cost: 200 Coins"),"Letter-only crafting confirms currency cost");
        yield return RenderSizes("Crafting-coin-confirm");
        Click("CraftDialogCancel"); Check(CurrencyService.Balance == 200,"Cancel spends no coins");
        Click("CraftButton"); yield return null; yield return null;
        CurrencyService.TrySpend(100,out _);
        Check(!ButtonNamed("CraftDialogAccept").interactable && Field<TMP_Text>(paidCraft,"dialogDetails").text.Contains("Current: 100 Coins"),"Open confirmation rechecks and refreshes balance");
        yield return RenderSizes("Crafting-funds-changed");
        int paidFireBefore = CharacterInstances.Count("feuer");
        CurrencyService.TryAdd(100,out _); Click("CraftDialogAccept"); yield return null; yield return null;
        Check(CurrencyService.Balance == 0 && CharacterInstances.Count("feuer") == paidFireBefore+1,"Confirmed letter-only craft atomically spends 200 and creates its character");
        yield return RenderSizes("Crafting-coins-spent");

        // Phase 10.1: real saved receipts drive Result; rendering never grants rewards.
        yield return Load("GermanyHome");
        long priorCoins=CurrencyService.Balance;int dropIndex=0;
        Check(BattleRewardService.Begin("feuer",out string dropBattle,out _),"Victory reward ticket saved");
        Check(BattleRewardService.Complete(dropBattle,true,out _,()=>++dropIndex==4?.9:0),"Coins and separated runs committed");
        Check(CurrencyService.Balance==priorCoins+100 && InventoryManager.GetItemCount(DynamicMaterials.Id("Feu"))>=1,"Victory grants 100 and Feu material");
        yield return Load("Result");
        Check(Find<ResultManager>().RewardSummary.Contains("Coins +100") && Find<ResultManager>().RewardSummary.Contains("Feu ×1") && Find<ResultManager>().RewardSummary.Contains("r ×1"),"Result displays saved partial drops");
        yield return RenderSizes("Result-material-rewards");
        string receiptSnapshot=JsonUtility.ToJson(PlayerSaveService.Instance.Capture());
        yield return Load("Result");
        Check(JsonUtility.ToJson(PlayerSaveService.Instance.Capture())==receiptSnapshot,"Reopening Result cannot grant again");
        Check(BattleRewardService.Begin("feuer",out string fullBattle,out _) && BattleRewardService.Complete(fullBattle,true,out _,()=>0),"All-success character granted with currency");
        yield return Load("Result");
        Check(Find<ResultManager>().RewardSummary.Contains("Character obtained!") && !Find<ResultManager>().RewardSummary.Contains("Materials"),"All-success result omits material section");
        yield return RenderSizes("Result-character-reward");
        Check(BattleRewardService.Begin("feuer",out string loseBattle,out _) && BattleRewardService.Complete(loseBattle,false,out _),"Defeat receipt saved without rewards");
        yield return Load("Result");
        Check(Find<ResultManager>().RewardSummary=="" && !Field<GameObject>(Find<ResultManager>(),"rewardPanel").activeSelf,"Defeat hides all victory rewards");
        yield return RenderSizes("Result-defeat");
        Check(!BattleRewardService.Complete(fullBattle,true,out _),"Replaced battle ticket cannot be claimed again");
        InventoryManager.AddText("Feu",2,out _);InventoryManager.AddText("er",1,out _);
        for(int n=0;n<28;n++)InventoryManager.AddText(new string((char)('a'+n%26),110)+(n<26?"ä":"ß"),1,out _);
        yield return Load("Inventory");
        Check(Find<InventoryUIManager>()!=null && Object.FindObjectsByType<TMP_Text>().Any(t=>t.text=="Feu"),"Dynamic material names appear alongside fixed items");
        yield return RenderSizes("Inventory-dynamic-long-materials");
        yield return ScrollToLongMaterial();yield return RenderSizes("Inventory-long-material-row");
        yield return Load("Crafting");
        var dynamicCraft=Find<CraftingUIManager>();
        Call(dynamicCraft,"SelectMaterial",DynamicMaterials.Id("Feu"));Call(dynamicCraft,"SelectMaterial",DynamicMaterials.Id("er"));
        Check(Field<TMP_Text>(dynamicCraft,"combinedWordText").text=="Feuer","Dynamic Feu plus er matches Feuer");
        yield return RenderSizes("Crafting-dynamic-materials");
        yield return ScrollToLongMaterial();yield return RenderSizes("Crafting-long-material-row");
        Click("CraftButton");yield return null;yield return null;
        Check(Field<TMP_Text>(dynamicCraft,"dialogDetails").text.Contains("Cost: 200 Coins"),"Dynamic crafting shows the unchanged cost");
        yield return RenderSizes("Crafting-dynamic-confirm");
        Click("CraftDialogCancel");Check(CurrencyService.Balance==200,"Cancel dynamic craft preserves coins");
        Click("CraftButton");yield return null;yield return null;int fireBefore=CharacterInstances.Count("feuer");Click("CraftDialogAccept");yield return null;yield return null;
        Check(CurrencyService.Balance==0 && CharacterInstances.Count("feuer")==fireBefore+1,"Dynamic materials craft a new individual and spend 200");
        yield return Load("CharacterSelect");yield return RenderSizes("CharacterSelect-dropped-character");
        yield return Load("CharacterCollection");yield return RenderSizes("CharacterCollection-dropped-character");
        // Stress only the isolated receipt display: long text must scroll above the old navigation controls.
        BattleRewardService.Begin("feuer",out string manyBattle,out _);BattleRewardService.Complete(manyBattle,true,out _,()=>.9);
        Check(PlayerStateTransactions.TryChange(data=>{var receipt=data.battleRewards.latest;for(int n=0;n<28;n++)receipt.materials.Add(DynamicMaterials.Entry(new string((char)('a'+n%26),110)+(n<26?"ä":"ß")));return true;},out _),"Long receipt fixture committed in isolated test profile");
        yield return Load("Result");
        var rewardScroll=GameObject.Find("BattleRewardScroll").GetComponent<ScrollRect>();
        yield return RenderSizes("Result-many-long-rewards");
        Check(rewardScroll.content.rect.height>rewardScroll.viewport.rect.height,"Many long rewards have scrollable content");
        rewardScroll.verticalNormalizedPosition=0;yield return RenderSizes("Result-many-long-rewards-bottom");
        yield return Load("Battle");
        var rewardBattle=Find<BattleManager>();typeof(BattleCombatant).GetProperty("HP").SetValue(rewardBattle.Context.Enemy,1);
        string testDirectory=Path.GetDirectoryName(PlayerSaveService.Instance.Files.MainPath);
        string movedDirectory=testDirectory+"-held";
        Directory.Move(testDirectory,movedDirectory);File.WriteAllText(testDirectory,"blocked for save retry validation");
        rewardBattle.UseSkill1();deadline=Time.realtimeSinceStartup+20;
        while(Find<BattleRewardRetryUI>()==null && Time.realtimeSinceStartup<deadline)yield return null;
        Check(Find<BattleRewardRetryUI>()!=null && SceneManager.GetActiveScene().name=="Battle" && !rewardBattle.CanChoose,"Failed reward save blocks Result transition and actions");
        yield return RenderSizes("Battle-reward-save-retry");
        File.Delete(testDirectory);Directory.Move(movedDirectory,testDirectory);
        Click("RetryRewardSaveButton");yield return null;yield return null;
        Check(SceneManager.GetActiveScene().name=="Result" && Find<ResultManager>().RewardSummary.Contains("Coins +100"),"Retry saves rewards then displays Result");

        // Phase 10.2: stage routes, choices, dialogue and actual guided battle.
        yield return Load("StageSelect");
        Check(Find<StageSelectManager>()!=null && Object.FindObjectsByType<StageRowUI>().Length==6,"All six data stages are listed including Feuer practice");
        Check(StageProgress.IsUnlocked("stage_1") && !StageProgress.IsUnlocked("stage_2"),"Initial stage locks are derived from progress");
        var stageScroll=GameObject.Find("StageList").GetComponent<ScrollRect>();
        Check(stageScroll.verticalScrollbar!=null,"Stage list exposes a scrollbar");
        yield return RenderSizes("StageSelect-initial-locks");
        stageScroll.verticalNormalizedPosition=0;yield return RenderSizes("StageSelect-practice-and-locked-bottom");stageScroll.verticalNormalizedPosition=1;
        TutorialChoiceUI.Request("stage_1",false);yield return null;
        yield return RenderSizes("TutorialChoice-first-visit");
        Click("TutorialOption0");yield return null;yield return null;
        Check(StageSession.StageId=="stage_1" && StageSession.Guided,"Take choice saves and retains the selected stage");
        yield return RenderSizes("CharacterSelect-Technik");
        Click("StartBattleButton");yield return null;yield return null;
        Check(DialogueUI.IsOpen && TutorialController.Active!=null,"Technik starts shared dialogue");
        yield return RenderSizes("Dialogue-Technik-introduction");
        yield return new WaitForSecondsRealtime(.22f);Click("DialogueNext");
        yield return RenderSizes("Dialogue-Technik-verbs");
        yield return new WaitForSecondsRealtime(.22f);Click("DialogueNext");yield return null;yield return null;
        var guidedBattle=Find<BattleManager>();var guidedController=TutorialController.Active;
        Check(guidedController.CurrentStep==TutorialStepType.WaitForSkillSelection,"Dialogue leads to required skill selection");
        guidedBattle.UseSkill2();Check(guidedBattle.LastCommittedAction==null && !guidedBattle.SelectMuessen(true),"Wrong skill and modifier APIs refuse tutorial input");
        yield return RenderSizes("Battle-Technik-highlighted-skill");
        int guidedPlayerHP=guidedBattle.Context.Player.HP;long guidedCoins=CurrencyService.Balance;
        Click(guidedBattle.SkillButton(guidedController.SkillId).name);
        deadline=Time.realtimeSinceStartup+20;while(!DialogueUI.IsOpen && Time.realtimeSinceStartup<deadline)yield return null;
        Check(DialogueUI.IsOpen && DialogueUI.Active.Current.dialogueId=="technik_after_attack" && guidedBattle.Context.Player.HP==guidedPlayerHP,"A real attack is followed by explanation before the enemy can hurt the learner");
        yield return RenderSizes("Dialogue-Technik-attack-explanation");
        yield return new WaitForSecondsRealtime(.22f);Click("DialogueNext");
        deadline=Time.realtimeSinceStartup+30;
        while(SceneManager.GetActiveScene().name=="Battle" && Time.realtimeSinceStartup<deadline)
        {
            var currentBattle=Find<BattleManager>();
            if(currentBattle.CanChoose && currentBattle.AllowsInput(TutorialInput.Skill,currentBattle.Context.Player.Character.skillIds[0]))currentBattle.UseSkill1();
            yield return null;
        }
        yield return null;yield return null;
        Check(SceneManager.GetActiveScene().name=="Result" && CurrencyService.Balance==guidedCoins+400,"Guided victory grants normal 100 plus first-clear 300");
        Check(StageProgress.IsUnlocked("stage_2") && TutorialPreferences.Get("technik_basics").completed,"Tutorial completion and stage unlock commit together");
        Check(Find<ResultManager>().RewardSummary.Contains("First clear: +300"),"Result shows the saved first-clear breakdown");
        yield return RenderSizes("Result-Technik-first-clear");
        yield return Load("StageSelect");yield return RenderSizes("StageSelect-after-Technik");
        TutorialChoiceUI.Request("stage_1",true);yield return null;yield return RenderSizes("TutorialChoice-review");Click("TutorialChoiceClose");
        TutorialChoiceUI.Request("stage_2",false);yield return null;yield return RenderSizes("TutorialChoice-coming-soon");
        Check(Field<TMP_Text>(Find<TutorialChoiceUI>(),"details").text.Contains("not available yet"),"Unimplemented guidance is clearly identified");
        Click("TutorialOption0");yield return null;yield return null;Check(!StageSession.Guided,"Unimplemented guidance starts a normal battle");
        Click("StartBattleButton");yield return null;yield return null;
        var adverbBattle=Find<BattleManager>();typeof(BattleCombatant).GetProperty("HP").SetValue(adverbBattle.Context.Enemy,1);adverbBattle.UseSkill1();
        deadline=Time.realtimeSinceStartup+20;while(SceneManager.GetActiveScene().name=="Battle" && Time.realtimeSinceStartup<deadline)yield return null;
        yield return null;yield return null;
        Check(StageProgress.IsUnlocked("stage_3") && !TutorialPreferences.Get("adverb_basics").completed,"Normal clear unlocks the next stage without false tutorial completion");
        yield return RenderSizes("Result-Adverb-normal-clear");
        yield return Load("StageSelect");Click("TutorialSettingsButton");yield return null;yield return RenderSizes("Settings-tutorial-prompts");
        Click("TutorialOption1");Check(TutorialPreferences.SkipAll,"Settings save global skip");Click("TutorialChoiceClose");
        TutorialChoiceUI.Request("stage_3",false);yield return null;yield return null;Check(SceneManager.GetActiveScene().name=="CharacterSelect" && Find<TutorialChoiceUI>()==null && !StageSession.Guided,"Global skip bypasses future prompts");
        yield return Load("StageSelect");TutorialChoiceUI.Request("stage_1",true);yield return null;
        yield return RenderSizes("TutorialChoice-review-overrides-global-skip");Click("TutorialOption0");yield return null;yield return null;Check(StageSession.Guided,"Explicit review overrides global skip");
        yield return Load("GermanyHome");Find<GermanyHomeManager>().GoSettings();yield return null;Click("TutorialOption0");Check(!TutorialPreferences.SkipAll,"Home settings restore future prompts");Click("TutorialChoiceClose");
        StageSession.Select("stage_1",true,out _);
        var storyLine=new DialogueLine{dialogueId="npc-preview",speakerName="Technik",speakerSide="Right",leftPortraitId="guide",rightPortraitId="feuer",text=string.Join(" ",Enumerable.Repeat("This shared dialogue can show a longer story on multiple pages.",30)),eventOnComplete="preview_finished"};
        Check(DialogueUI.PlayLine(storyLine,_=>{},()=>{},out _),"Standalone story dialogue uses the same UI");yield return null;
        yield return RenderSizes("Dialogue-long-right-speaker");yield return new WaitForSecondsRealtime(.22f);Click("DialogueNext");yield return RenderSizes("Dialogue-next-page");DialogueUI.Active.Cancel();yield return null;
        yield return Load("Battle");deadline=Time.realtimeSinceStartup+10;
        while(DialogueUI.IsOpen && Time.realtimeSinceStartup<deadline){yield return new WaitForSecondsRealtime(.22f);DialogueUI.Active.Advance();yield return null;}
        yield return null;TutorialController.Active.Abort("Intentional UI recovery validation");yield return null;
        Check(!DialogueUI.IsOpen && TutorialController.NavigationAllowed && Find<BattleManager>().SelectMuessen(true),"Tutorial error releases its input guard");
        yield return RenderSizes("Battle-tutorial-error-recovery");

    }

    private IEnumerator ScrollToLongMaterial()
    {
        Canvas.ForceUpdateCanvases();
        var label=Object.FindObjectsByType<TMP_Text>().First(t=>t.name=="ItemNameText" && t.text.Length>100);
        var scroll=label.GetComponentInParent<ScrollRect>();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);Canvas.ForceUpdateCanvases();scroll.StopMovement();
        var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,label.transform.parent);
        var position=scroll.content.anchoredPosition;
        position.y=Mathf.Clamp(position.y+scroll.viewport.rect.yMax-8-bounds.max.y,0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height));scroll.content.anchoredPosition=position;
        yield return null;yield return null;Canvas.ForceUpdateCanvases();label.ForceMeshUpdate();
        Check(!label.isTextOverflowing,"Long dynamic row wraps without truncating the label");
    }

    private IEnumerator RenderSizes(string label)
    {
        foreach (Vector2Int size in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1600,1200), new Vector2Int(2560,1080) })
        {
            Camera camera = Camera.main;
            var texture = new RenderTexture(size.x,size.y,24); texture.Create();
            camera.targetTexture = texture;
            var canvases = Object.FindObjectsByType<Canvas>().Where(c => c.isRootCanvas).ToArray();
            foreach (var canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
            }
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (var canvas in canvases) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
            Canvas.ForceUpdateCanvases();
            yield return null;
            foreach (var text in Object.FindObjectsByType<TMP_Text>()) text.ForceMeshUpdate();
            InspectGeometry(label, size, camera);
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
            var old = RenderTexture.active; RenderTexture.active = texture;
            var png = new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
            png.ReadPixels(new Rect(0,0,size.x,size.y),0,0); png.Apply();
            File.WriteAllBytes(Path.Combine(output, $"{label}-{size.x}x{size.y}.png"), png.EncodeToPNG());
            RenderTexture.active = old; Destroy(png);
            camera.targetTexture = null;
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
            texture.Release(); Destroy(texture);
            yield return null;
        }
    }

    private void InspectGeometry(string label, Vector2Int size, Camera camera)
    {
        string context = $"{label} {size.x}x{size.y}";
        var detailView = Find<CharacterDetailManager>();
        if (detailView != null)
        {
            var identityText = Field<TMP_Text>(detailView, "characterInfoText");
            Check(identityText.text.Contains("Individual ") || identityText.text.Contains("read only"), context + " identifies instance or read-only species");
            Check(!identityText.isTextOverflowing, context + " identity and stats are not truncated");
        }
        foreach (var button in Object.FindObjectsByType<Button>())
        {
            if (button.name == "Image") continue; // intentionally full-screen dismiss target
            var rect = (RectTransform)button.transform;
            var scroll = button.GetComponentInParent<ScrollRect>();
            if (scroll != null) continue; // content is intentionally clipped; inspect the list separately
            Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var points = corners.Select(camera.WorldToViewportPoint).ToArray();
            Check(points.All(p => p.x >= -.002 && p.x <= 1.002 && p.y >= -.002 && p.y <= 1.002), context + " button inside screen: " + button.name);
            Check(rect.rect.height >= 64, context + " button hit height >=64: " + button.name);
        }
        foreach (var button in Object.FindObjectsByType<Button>().Where(b => b.interactable &&
            (Find<TutorialChoiceUI>()==null || b.transform.IsChildOf(Find<TutorialChoiceUI>().transform)) &&
            (Find<DialogueUI>()==null || b.transform.IsChildOf(Find<DialogueUI>().transform)) &&
            (b.name.StartsWith("TutorialOption") || b.name == "TutorialChoiceClose" || b.name == "DialogueNext" || b.name == "NextInstanceButton" || b.name == "PreviousInstanceButton" || b.name == "FutureButton" || b.name == "MuessenButton" || b.name.StartsWith("CraftDialog") || b.name == "CraftNextIndividual" || b.name == "CraftPreviousIndividual" || b.name == "RetryRewardSaveButton" || b.name.StartsWith("AddCoins") || b.name == "CloseCoinDialog" ||
             (b.name == "CoinsPlusButton" && (Find<CurrencyHUD>() == null || !Field<GameObject>(Find<CurrencyHUD>(),"additionDialog").activeSelf)))))
        {
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, context + " pointer reaches " + button.name);
        }
        var tutorialView = TutorialController.Active;
        if(tutorialView!=null && !tutorialView.Failed && !tutorialView.ReadyForCommit && (tutorialView.CurrentStep==TutorialStepType.Highlight || tutorialView.CurrentStep==TutorialStepType.WaitForSkillSelection))
        {
            var battle=Find<BattleManager>();var target=battle.SkillButton(tutorialView.SkillId);var rect=(RectTransform)target.transform;
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<Button>()==target,context+" highlighted original skill receives pointer input");
            foreach(var other in Field<GameObject[]>(battle,"skillButtons").Where(o=>o.activeInHierarchy && o!=target.gameObject))
            {
                var otherRect=(RectTransform)other.transform;pointer.position=RectTransformUtility.WorldToScreenPoint(camera,otherRect.TransformPoint(otherRect.rect.center));hits.Clear();EventSystem.current.RaycastAll(pointer,hits);
                Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<Button>()!=other.GetComponent<Button>(),context+" shade rejects other skill buttons");
            }
        }
        var dialogueView=Find<DialogueUI>();
        if(dialogueView!=null)
        {
            var textBox=Field<Button>(dialogueView,"textBox");var textBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(dialogueView.transform,textBox.transform);
            foreach(var portrait in new[]{Field<Image>(dialogueView,"leftPortrait"),Field<Image>(dialogueView,"rightPortrait")})
            {
                var portraitBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(dialogueView.transform,portrait.transform);
                Check(portraitBounds.min.y>textBounds.max.y,context+" portrait stays above dialogue text");
            }
            Check(Field<TMP_Text>(dialogueView,"body").textInfo.pageCount>=1,context+" long dialogue has readable pages");
        }
        var resultView = Find<ResultManager>();
        if(resultView != null && Field<GameObject>(resultView,"rewardPanel").activeSelf)
        {
            var viewport = Field<GameObject>(resultView,"rewardPanel").GetComponent<ScrollRect>();
            Check(viewport.content.rect.width > 0 && viewport.content.rect.height > 0,context + " reward text has a readable layout");
            foreach(var nav in Object.FindObjectsByType<Button>())
            {
                var pointer = new PointerEventData(EventSystem.current) { position=RectTransformUtility.WorldToScreenPoint(camera,((RectTransform)nav.transform).TransformPoint(((RectTransform)nav.transform).rect.center)) };
                var hits = new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<Button>()==nav,context + " rewards leave navigation clickable " + nav.name);
            }
        }
        var currencyHUD = Find<CurrencyHUD>();
        if (currencyHUD != null)
        {
            var value = Field<TMP_Text>(currencyHUD,"balanceText");
            Check(value.text == EconomySettings.Price(CurrencyService.Balance),context + " current coin balance");
            Check(!value.isTextOverflowing,context + " coin amount fits including long values");
        }
        var selectView = Find<CharacterSelectManager>();
        if (selectView != null)
        {
            var preview = Field<TMP_Text>(selectView,"selectedCharacterText");
            Check(!preview.isTextOverflowing, context + " individual equipment and stats fit");
            Check(!Field<TMP_Text>(selectView,"instancePositionText").isTextOverflowing, context + " individual ordinal fits");
        }
        foreach (var scroll in Object.FindObjectsByType<ScrollRect>())
        {
            if (scroll.content == null || scroll.viewport == null) { failures.Add(context + " missing scroll content/viewport"); continue; }
            Check(scroll.content.IsChildOf(scroll.viewport), context + " scroll content is inside its viewport");
            var children = scroll.content.Cast<Transform>().Where(t => t.gameObject.activeSelf).OfType<RectTransform>().ToArray();
            for (int i=0;i<children.Length;i++)
            {
                var child=children[i];
                Check(child.rect.width > 0 && child.rect.height >= 64, context + " positive sized list row " + i);
                if (i>0)
                {
                    Bounds a=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.content, children[i-1]);
                    Bounds b=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.content,child);
                    bool overlaps = Mathf.Min(a.max.x,b.max.x)-Mathf.Max(a.min.x,b.min.x)>1 && Mathf.Min(a.max.y,b.max.y)-Mathf.Max(a.min.y,b.min.y)>1;
                    Check(!overlaps,context+" list rows do not overlap " + i);
                }
            }
        }
        foreach (var text in Object.FindObjectsByType<TMP_Text>())
        {
            if (text.isTextOverflowing && text.overflowMode != TextOverflowModes.Page) warnings.Add(context + " text truncated: " + text.name + " = " + text.text);
        }
        passes.Add(context + " rendered");
    }
}
#endif

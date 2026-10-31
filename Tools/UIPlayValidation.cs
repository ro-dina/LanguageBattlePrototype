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
        CharacterInstances.Create("nacht");
        CharacterInstances.Create("hund"); CharacterInstances.Create("hund");
        var discoveredFire = CharacterInstances.Create("feuer"); CharacterInstances.Remove(discoveredFire.instanceId);
        foreach (var item in JsonUtility.FromJson<GachaItemDatabase>(Resources.Load<TextAsset>("Data/gacha_items").text).items)
            InventoryManager.AddItem(item.id, 5);

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
                Call(crafting, "SelectMaterial", "word_wasser");
                Call(crafting, "SelectMaterial", "word_pistole");
            }
            yield return RenderSizes(scene);
        }

        yield return Load("CharacterSelect");
        var copies = CharacterInstances.GetByCharacterId("nacht");
        var select = Find<CharacterSelectManager>();
        var list = Field<Transform>(select, "characterButtonParent");
        Check(list.GetComponentsInChildren<Button>().Length == CharacterInstances.GetAll().Count,
            "Selection shows every owned instance rather than unique species");
        Check(list.GetComponentsInChildren<Button>().Select(b => b.GetComponentInChildren<TMP_Text>().text).Distinct().Count() == CharacterInstances.GetAll().Count,
            "Duplicate species have distinct short identity labels");
        var scroll = list.GetComponentInParent<ScrollRect>();
        if (scroll != null) scroll.verticalNormalizedPosition = 0;
        yield return null; yield return null;
        Click("CharacterButton_" + copies[1].instanceId);
        Check(GameManager.SelectedCharacterInstanceId == copies[1].instanceId, "Pointer selects the second Nacht identity");
        yield return RenderSizes("CharacterSelect-instanceB");
        Click("CharacterListButton"); yield return null; yield return null;
        var secondDetail = Find<CharacterDetailManager>();
        Call(secondDetail, "EquipCharacterEquipment", "schnell");
        yield return RenderSizes("CharacterDetail-instanceB");
        secondDetail.Back(); yield return null; yield return null; Click("SaveButton"); yield return null; yield return null;
        Check(CharacterLoadoutStore.GetLoadout(copies[1].instanceId).adjectiveId == "schnell" &&
            string.IsNullOrEmpty(CharacterLoadoutStore.GetLoadout(copies[0].instanceId).adjectiveId),
            "Saving the second Nacht leaves the first Nacht equipment unchanged");

        yield return Load("Inventory");
        var inventory = Find<InventoryUIManager>();
        var content = Field<Transform>(inventory, "content");
        inventory.RefreshInventory(); inventory.RefreshInventory();
        yield return null;
        Check(content != null && content.parent.name == "Viewport", "Inventory content survives repeated refresh and remains below Viewport");
        Check(ActiveChildren(content) == InventoryManager.GetAllItems().Count, "Inventory repeated refresh has one row per item");
        Click("BackButton"); yield return null;
        Check(SceneManager.GetActiveScene().name == "GermanyHome", "Inventory Back returns home");

        yield return Load("Crafting");
        var craft = Find<CraftingUIManager>();
        var materials = Field<Transform>(craft, "inventoryContent");
        craft.RefreshInventory(); craft.RefreshInventory(); yield return null;
        Check(materials.parent.name == "Viewport" && ActiveChildren(materials) == InventoryManager.GetAllItems().Count, "Crafting refresh preserves viewport and avoids duplicate rows");
        int waterBefore = InventoryManager.GetItemCount("word_wasser");
        int pistolBefore = InventoryManager.GetItemCount("word_pistole");
        Call(craft, "SelectMaterial", "word_wasser"); Call(craft, "SelectMaterial", "word_pistole");
        Click("ClearButton");
        Check(Field<TMP_Text>(craft, "selectedMaterialText").text == "", "Clear empties selected crafting materials");
        Call(craft, "SelectMaterial", "word_wasser"); Call(craft, "SelectMaterial", "word_pistole");
        Click("CraftButton"); yield return null;
        Check(InventoryManager.GetItemCount("word_wasser") == waterBefore - 1 && InventoryManager.GetItemCount("word_pistole") == pistolBefore - 1 && OwnedCharacters.IsCharacterOwned("wasserpistole"), "Craft consumes the original recipe and unlocks its character");

        Call(craft, "SelectMaterial", "word_wasser"); Call(craft, "SelectMaterial", "word_pistole");
        Click("CraftButton"); yield return null;
        Check(CharacterInstances.Count("wasserpistole") == 2 &&
            CharacterInstances.GetByCharacterId("wasserpistole").Select(i => i.instanceId).Distinct().Count() == 2,
            "Repeated Wasserpistole crafting creates a second distinct instance");
        yield return RenderSizes("Crafting-repeat");

        yield return Load("CharacterCollection");
        var collection = Find<CharacterCollectionManager>();
        var collectionLabels = Field<Transform>(collection, "characterButtonParent").GetComponentsInChildren<Button>().Select(b => b.GetComponentInChildren<TMP_Text>().text).ToArray();
        Check(collectionLabels.Contains("Nacht [Owned: 2]") && collectionLabels.Contains("Feuer [Discovered • 0 owned]") &&
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
        Check(Find<BattleManager>().Selection.Tense == BattleTense.Future && !ButtonNamed("MuessenButton").interactable,
            "Future selection disables müssen");
        yield return RenderSizes("Battle-future-selected");
        Click("NoTenseButton"); Click("MuessenButton");
        Check(Find<BattleManager>().Selection.Modal == BattleModal.Muessen && !ButtonNamed("FutureButton").interactable,
            "Müssen selection disables future");
        yield return RenderSizes("Battle-muessen-selected");
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
            Check(identityText.text.Contains("Instance: ") || identityText.text.Contains("read only"), context + " identifies instance or read-only species");
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
            if (text.isTextOverflowing) warnings.Add(context + " text truncated: " + text.name + " = " + text.text);
        }
        passes.Add(context + " rendered");
    }
}
#endif

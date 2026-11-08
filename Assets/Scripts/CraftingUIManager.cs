using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CraftingUIManager : MonoBehaviour
{
    [SerializeField] private Transform inventoryContent;
    [SerializeField] private GameObject craftingMaterialPrefab;
    [SerializeField] private TMP_Text selectedMaterialText;
    [SerializeField] private TMP_Text combinedWordText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button craftButton;
    [Header("Individual selection and consumption confirmation")]
    [SerializeField] private GameObject characterDialog;
    [SerializeField] private TMP_Text dialogTitle;
    [SerializeField] private TMP_Text dialogDetails;
    [SerializeField] private Button dialogPrevious;
    [SerializeField] private Button dialogNext;
    [SerializeField] private Button dialogAccept;
    [SerializeField] private Button dialogCancel;

    private readonly List<CraftingElement> selection = new List<CraftingElement>();
    private readonly List<GameObject> spawnedMaterialObjects = new List<GameObject>();
    private string pickerInstanceId;
    private CraftingElement[] pendingCraft;
    private bool started;
    private PlayerOperation pendingOperation;
    private bool crafting;
    private void OnEnable() { PlayerStateChanges.Changed += OnOwnershipChanged; CurrencyService.BalanceChanged += OnBalanceChanged; }
    private void OnDisable() { PlayerStateChanges.Changed -= OnOwnershipChanged; CurrencyService.BalanceChanged -= OnBalanceChanged; }
    private void OnBalanceChanged(long _) { if (started) { UpdateSelectionTexts(); RefreshConfirmationAvailability(); } }
    private void RefreshConfirmationAvailability()
    {
        if (pendingCraft == null || !characterDialog.activeSelf) return;
        var result = CraftingManager.Preview(pendingCraft);
        LocalizedText.SetProvider(dialogTitle,()=>result==null?Localization.Text("ui.inputs.unavailable"):Localization.Text("ui.craft.confirm",result.characterName));
        var individuals = pendingCraft.Where(e => e.Kind == CraftingElementKind.CharacterInstance).ToArray();
        LocalizedText.SetProvider(dialogDetails,()=>Localization.Text("ui.craft.confirm.details",
            individuals.Length==0?"":string.Join("\n\n",individuals.Select(e=>Describe(e.Id)))+"\n\n"+Localization.Text("ui.consume.warning")+"\n",
            string.Join(" + ",pendingCraft.Where(e=>e.Kind==CraftingElementKind.Material).Select(e=>ItemDisplayNames.Get(e.Id))),
            EconomySettings.Price(EconomySettings.Current.craftingCost),EconomySettings.Price(CurrencyService.Balance),result?.characterName ?? Localization.Text("ui.unavailable"))+
            (CurrencyService.CanSpend(EconomySettings.Current.craftingCost)?"":"\n"+Localization.Text("ui.not.enough")+" "+EconomySettings.Current.currencyDisplayName+"."));
        dialogAccept.interactable = CraftingManager.CanCraft(pendingCraft);

    }
    private void OnOwnershipChanged()
    {
        if (!started) return;
        RefreshInventory(); UpdateSelectionTexts();
        if (characterDialog.activeSelf && pendingCraft == null) RefreshPicker();
        RefreshConfirmationAvailability();
    }
    private void Start()
    {
        LocalizedText.Learning(selectedMaterialText); LocalizedText.Learning(combinedWordText);
        started = true;
        dialogPrevious.onClick.AddListener(() => CyclePicker(-1));
        dialogNext.onClick.AddListener(() => CyclePicker(1));
        dialogAccept.onClick.AddListener(AcceptDialog);
        dialogCancel.onClick.AddListener(CancelDialog);
        characterDialog.SetActive(false);
        RefreshInventory(); UpdateSelectionTexts();
    }
    public void RefreshInventory()
    {
        foreach (var row in spawnedMaterialObjects) { if (row != null) { row.SetActive(false); Destroy(row); } }
        spawnedMaterialObjects.Clear();
        foreach (var entry in InventoryManager.GetAllItems())
        {
            string id = entry.itemId;
            AddRow("Material_" + id,"[Material] " + ItemDisplayNames.Get(id),"×" + entry.count,() => SelectMaterial(id));
        }
        foreach (var group in CharacterInstances.GetAll().GroupBy(i => i.characterId).OrderBy(g => GameDatabase.Instance.GetCharacter(g.Key).characterName))
        {
            string id = group.Key;
            AddRow("CraftCharacter_" + id,"[Character] " + GameDatabase.Instance.GetCharacter(id).characterName,"×" + group.Count(),() => OpenIndividualPicker(id));
        }
    }
    private void AddRow(string name,string label,string count,UnityEngine.Events.UnityAction click)
    {
        var row = Instantiate(craftingMaterialPrefab,inventoryContent); row.name = name; row.SetActive(true);
        spawnedMaterialObjects.Add(row);
        LocalizedText.Set(FindChildText(row,"ItemNameText"),name.StartsWith("Material_")?"ui.row.material":"ui.row.character",label.Substring(label.IndexOf(']')+2)); FindChildText(row,"ItemCountText").text = count;
        if (name.StartsWith("Material_" + DynamicMaterials.Prefix,System.StringComparison.Ordinal)) MaterialRowLayout.Configure(row,FindChildText(row,"ItemNameText"));
        var button = row.GetComponent<Button>(); button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(click);
    }
    private void SelectMaterial(string itemId)
    {
        if (WordCharacterRewards.Character(itemId) != null || WordCharacterRewards.Ambiguous(itemId) ||
            selection.Count(e => e.Kind == CraftingElementKind.Material && e.Id == itemId) >= InventoryManager.GetItemCount(itemId)) return;
        selection.Add(CraftingElement.Material(itemId)); UpdateSelectionTexts();
    }
    public void SelectCharacterInstance(string instanceId)
    {
        if (!CharacterInstances.CanConsume(instanceId) || selection.Any(e => e.Kind == CraftingElementKind.CharacterInstance && e.Id == instanceId)) return;
        selection.Add(CraftingElement.Character(instanceId)); UpdateSelectionTexts();
    }
    public void UndoLast()
    { if (selection.Count > 0) selection.RemoveAt(selection.Count-1); UpdateSelectionTexts(); }
    public void ClearSelection() { CancelDialog(); selection.Clear(); UpdateSelectionTexts(); }
    private static string Describe(string instanceId)
    {
        var instance = CharacterInstances.Get(instanceId);
        if (instance == null) return "Unavailable individual";
        var loadout = CharacterLoadoutStore.GetLoadout(instanceId);
        string adjective = string.IsNullOrEmpty(loadout.adjectiveId) ? "none" : GameDatabase.Instance.GetEquipment(loadout.adjectiveId).equipmentName;
        string article = string.IsNullOrEmpty(loadout.articleId) ? "default" : GameDatabase.Instance.GetEquipment(loadout.articleId).equipmentName;
        string skills = string.Join(", ",loadout.skillSlots.Select(s => GameDatabase.Instance.GetSkill(s.skillId).skillName));
        return Localization.Text("ui.individual.description",CharacterInstances.DisplayName(instanceId),string.IsNullOrEmpty(loadout.adjectiveId)?Localization.Text("ui.lower.none"):adjective,string.IsNullOrEmpty(loadout.articleId)?Localization.Text("ui.default"):article,skills);
    }
    public void OpenIndividualPicker(string characterId)
    {
        var individuals = CharacterInstances.GetByCharacterId(characterId);
        if (individuals.Count == 0) return;
        pendingCraft = null; pickerInstanceId = individuals[0].instanceId; characterDialog.SetActive(true); RefreshPicker();
    }
    private void CyclePicker(int step)
    {
        var current = CharacterInstances.Get(pickerInstanceId); if (current == null || pendingCraft != null) return;
        var list = CharacterInstances.GetByCharacterId(current.characterId);
        int index = list.FindIndex(i => i.instanceId == pickerInstanceId);
        pickerInstanceId = list[(index + step + list.Count) % list.Count].instanceId; RefreshPicker();
    }
    private void RefreshPicker()
    {
        var current = CharacterInstances.Get(pickerInstanceId);
        dialogTitle.text = current == null ? "Individual unavailable" : "Choose an individual • " + CharacterInstances.DisplayBadge(pickerInstanceId);
        string selectedPicker=pickerInstanceId;LocalizedText.SetProvider(dialogDetails,()=>Describe(selectedPicker)+"\n\n"+Localization.Text("ui.picker.warning"));
        dialogPrevious.gameObject.SetActive(true); dialogNext.gameObject.SetActive(true);
        dialogPrevious.interactable = dialogNext.interactable = current != null && CharacterInstances.Count(current.characterId) > 1;
        dialogAccept.interactable = CharacterInstances.CanConsume(pickerInstanceId) && !selection.Any(e => e.Kind == CraftingElementKind.CharacterInstance && e.Id == pickerInstanceId);
        dialogAccept.GetComponentInChildren<TMP_Text>().text = "Add individual";
    }
    public void CraftSelectedMaterials()
    {
        if (crafting || characterDialog.activeSelf) return;
        if (!CraftingManager.CanCraft(selection))
        { SetResultText(CurrencyService.CanSpend(EconomySettings.Current.craftingCost) ? "No matching character, or inputs unavailable." : "Not enough " + EconomySettings.Current.currencyDisplayName + ". Need " + EconomySettings.Price(EconomySettings.Current.craftingCost)); return; }
        pendingCraft = selection.ToArray(); pendingOperation = new PlayerOperation(); characterDialog.SetActive(true);
        RefreshConfirmationAvailability();
        dialogPrevious.gameObject.SetActive(false); dialogNext.gameObject.SetActive(false);
        dialogAccept.GetComponentInChildren<TMP_Text>().text = "Craft & consume";
    }
    public void AcceptDialog()
    {
        if (!characterDialog.activeSelf) return;
        if (pendingCraft != null) { var inputs = pendingCraft; var operation = pendingOperation; CancelDialog(); ExecuteCraft(inputs,operation); }
        else if (dialogAccept.interactable) { SelectCharacterInstance(pickerInstanceId); CancelDialog(); }
    }
    public void CancelDialog() { dialogDetails.text=""; characterDialog.SetActive(false); pendingCraft = null; pendingOperation = null; pickerInstanceId = null; }
    private void ExecuteCraft(CraftingElement[] inputs,PlayerOperation operation)
    {
        if (crafting) return;
        crafting = true;
        bool success = CraftingManager.TryCraft(inputs,out var created,out string error,operation);
        crafting = false;
        if (!success) { UpdateSelectionTexts(); SetResultText("Craft failed; nothing consumed. " + Localization.Error(error)); return; }
        selection.Clear(); UpdateSelectionTexts(); RefreshInventory();
        LocalizedText.Set(resultText,"ui.craft.success",GameDatabase.Instance.GetCharacter(created.characterId).characterName,CharacterInstances.Count(created.characterId));
    }
    private void UpdateSelectionTexts()
    {
        selectedMaterialText.text = string.Join(" + ",selection.Select(e => e.Kind == CraftingElementKind.Material ? ItemDisplayNames.Get(e.Id) : CharacterInstances.DisplayName(e.Id)));
        var character = CraftingManager.Preview(selection);
        combinedWordText.text = character?.characterName ?? CraftingManager.CombinedWord(selection) ?? "";
        craftButton.interactable = CraftingManager.CanCraft(selection);
        LocalizedText.SetProvider(resultText,()=> selection.Count==0 ? Localization.Text("ui.select.materials")+"\n"+Localization.Text("ui.cost.prefix")+" "+EconomySettings.Price(EconomySettings.Current.craftingCost) : character==null ? Localization.Text("ui.no.match") :
            character.characterName+" "+Localization.Text("ui.character.kind")+"\n"+(craftButton.interactable?Localization.Text("ui.cost.prefix")+" "+EconomySettings.Price(EconomySettings.Current.craftingCost):CurrencyService.CanSpend(EconomySettings.Current.craftingCost)?Localization.Text("ui.inputs.unavailable"):Localization.Text("ui.not.enough")+" "+EconomySettings.Current.currencyDisplayName+". "+Localization.Text("ui.need")+" "+EconomySettings.Price(EconomySettings.Current.craftingCost)));

    }
    private static TMP_Text FindChildText(GameObject parent,string childName) => parent.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == childName);
    private void SetResultText(string message) { if (resultText != null) resultText.text = message; }
    public void BackHome() => SceneManager.LoadScene("GermanyHome");
}

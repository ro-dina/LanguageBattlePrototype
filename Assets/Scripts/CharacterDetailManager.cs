using TMPro;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CharacterDetailManager : MonoBehaviour
{
    [Header("Character View")]
    [SerializeField] private Image characterImage;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text characterInfoText;

    [Header("Character Equipment")]
    [SerializeField] private TMP_Text characterEquipmentText;
    [SerializeField] private GameObject characterEquipmentPanel;
    [SerializeField] private Transform characterEquipmentButtonParent;
    [SerializeField] private GameObject equipmentButtonPrefab;

    [Header("Skill Equipment")]
    [SerializeField] private TMP_Text skillEquipmentText;
    [SerializeField] private Button[] skillSlotButtons;
    [SerializeField] private TMP_Text[] skillSlotButtonTexts;
    [SerializeField] private GameObject skillEquipmentPanel;
    [SerializeField] private Transform skillEquipmentButtonParent;
    [SerializeField] private GameObject battleButton;

    public string ViewedInstanceId=>viewedInstanceId;
    public string DraftAdjectiveId=>tempCharacterEquipmentId;
    public bool IsAdjectiveListOpen=>characterEquipmentPanel!=null && characterEquipmentPanel.activeSelf && currentCharacterEquipmentMode=="adjective";
    public Button BattleButton=>battleButton.GetComponent<Button>();
    public Button AdjectiveButton=>Object.FindObjectsByType<Button>(FindObjectsInactive.Include).FirstOrDefault(b=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentTarget(i)==this && b.onClick.GetPersistentMethodName(i)=="OpenAdjectiveEquipmentList"));
    public Button EquipmentButton(string id)=>characterEquipmentButtonParent.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="CharacterEquipmentButton_"+id);
    private int selectedSkillIndex = -1;
    private string viewedInstanceId;
    private bool CanEdit => character != null && CharacterInstances.Get(viewedInstanceId)?.characterId == character.id;
    private bool started;
    private void OnEnable() => PlayerStateChanges.Changed += RefreshOwnership;
    private void OnDisable() => PlayerStateChanges.Changed -= RefreshOwnership;
    private void RefreshOwnership()
    {
        if (!started) return;
        if (characterNameText != null) characterNameText.text = character.characterName; LocalizedText.Learning(characterNameText);
        UpdateCharacterInfoText();
        foreach (var button in GetComponentsInChildren<Button>(true))
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                string method = button.onClick.GetPersistentMethodName(i);
                if (method == "OpenAdjectiveEquipmentList" || method == "OpenArticleEquipmentList" || method == "OpenCharacterEquipmentList") button.interactable = CanEdit;
            }
        if (skillSlotButtons != null) foreach (var button in skillSlotButtons) if (button != null) button.interactable = CanEdit;
        SetupSceneMode();
    }

    private EquipmentData selectedArticleEquipment;
    private CharacterData character;
    private EquipmentData selectedCharacterEquipment;
    private string currentCharacterEquipmentMode = "adjective";
    private string tempArticleEquipmentId;
    private string tempCharacterEquipmentId;
    private string[] tempSkillEquipmentIds = new string[4] { "", "", "", "" };
    private bool hasUnsavedChanges = false;

    [Header("Unsaved Changes")]
    [SerializeField] private GameObject unsavedConfirmPanel;

    private void Start()
    {
        if (GameDatabase.Instance == null)
        {
            Debug.LogError("Character detail cannot be displayed because GameDatabase.Instance is null.");
            return;
        }

        viewedInstanceId = GameManager.SelectedCharacterInstanceId;
        var master = GameDatabase.Instance.GetCharacter(GameManager.SelectedCharacterId);
        character = CharacterLoadoutStore.GetCharacterForBattle(viewedInstanceId) ??
            (master == null ? null : JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(master)));

        if (character == null)
        {
            Debug.LogError($"Character detail cannot be displayed because character id was not found: {GameManager.SelectedCharacterId}");
            return;
        }

        SetupTemporaryEquipmentState();
        LoadCharacter();
        SetupCharacterEquipment();

        if (characterEquipmentPanel != null)
        {
            characterEquipmentPanel.SetActive(false);
        }

        SetupSkillEquipmentButtons();

        if (skillEquipmentPanel != null)
        {
            skillEquipmentPanel.SetActive(false);
        }

        started = true;
        RefreshOwnership();

        if (unsavedConfirmPanel != null)
        {
            unsavedConfirmPanel.SetActive(false);
        }
        TutorialController.AttachPreparation(this);
    }

    private void SetupTemporaryEquipmentState()
    {
        CharacterLoadout loadout = CharacterLoadoutStore.GetLoadout(viewedInstanceId) ?? CharacterLoadoutStore.GetDefaultLoadout(character.id);
        tempCharacterEquipmentId = loadout.adjectiveId;
        tempArticleEquipmentId = loadout.articleId;
        tempSkillEquipmentIds = new string[4] { "", "", "", "" };
        for (int i = 0; i < loadout.skillSlots.Length && i < tempSkillEquipmentIds.Length; i++)
            tempSkillEquipmentIds[i] = loadout.skillSlots[i].equippedAdverbId;
    }

    private void LoadCharacter()
    {
        if (character == null) return;

        if (characterNameText != null)
        {
            characterNameText.text = character.characterName; LocalizedText.Learning(characterNameText);
        }

        Sprite sprite =
            Resources.Load<Sprite>(
                $"Images/Characters/{character.illustration}");

        if (sprite != null && characterImage != null)
        {
            characterImage.sprite = sprite;

            AspectRatioFitter fitter =
                characterImage.GetComponent<AspectRatioFitter>();

            if(fitter != null)
            {
                fitter.aspectRatio =
                    (float)sprite.rect.width /
                    sprite.rect.height;
            }
        }

        UpdateCharacterInfoText();
    }

    private void SetupCharacterEquipment()
    {
        selectedCharacterEquipment = GameDatabase.Instance.GetEquipment(tempCharacterEquipmentId);
        selectedArticleEquipment = GameDatabase.Instance.GetEquipment(tempArticleEquipmentId);
        UpdateCharacterInfoText();
        UpdateCharacterEquipmentText();
    }

    private void UpdateCharacterInfoText()
    {
        if (character == null || characterInfoText == null) return;

        int attack = character.attack + DamageCalculator.GetCharacterAttackBonus(selectedCharacterEquipment);
        int defense = character.defense + DamageCalculator.GetCharacterDefenseBonus(selectedCharacterEquipment);
        int speed = character.speed + DamageCalculator.GetCharacterSpeedBonus(selectedCharacterEquipment);

        characterInfoText.text =
            (CanEdit ? "Individual " + CharacterInstances.DisplayBadge(viewedInstanceId) : CharacterCollection.StatusLabel(character.id) + " • read only") + "\n" +
            $"Gender: {character.grammaticalGender}\n" +
            $"HP: {character.hp}\n" +
            $"ATK: {attack}\n" +
            $"DEF: {defense}\n" +
            $"SPD: {speed}";
    }

    private void UpdateCharacterEquipmentText()
    {
        if (characterEquipmentText == null) return;

        string adjectiveText = "None";
        if (selectedCharacterEquipment != null)
        {
            string suffix = DamageCalculator.AppliesToCharacter(selectedCharacterEquipment) ? "" : " (no effect)";
            adjectiveText = selectedCharacterEquipment.equipmentName + suffix;
        }

        string articleText = "None";
        if (selectedArticleEquipment != null)
        {
            articleText = selectedArticleEquipment.equipmentName;
        }

        characterEquipmentText.text =
            $"Article: {articleText}\n" +
            $"Adjective: {adjectiveText}";
    }

    public void OpenCharacterEquipmentList()
    {
        OpenAdjectiveEquipmentList();
    }

    public void OpenAdjectiveEquipmentList()
    {
        if(!TutorialController.DetailAllows(TutorialInput.EquipmentOpen,"adjective"))return;
        if (!CanEdit || GameDatabase.Instance == null) return;

        currentCharacterEquipmentMode = "adjective";

        CloseSkillEquipmentList();

        if (characterEquipmentPanel == null) return;
        if (characterEquipmentButtonParent == null) return;
        if (equipmentButtonPrefab == null) return;

        characterEquipmentPanel.SetActive(true);
        CreateCharacterEquipmentButtons();
        if(TutorialController.Active?.IsPreparation==true)TutorialController.Active.EquipmentOpened();
    }

    public void OpenArticleEquipmentList()
    {
        if(!TutorialController.DetailAllows(TutorialInput.EquipmentOpen,"article"))return;
        if (!CanEdit || GameDatabase.Instance == null) return;

        currentCharacterEquipmentMode = "article";

        CloseSkillEquipmentList();

        if (characterEquipmentPanel == null) return;
        if (characterEquipmentButtonParent == null) return;
        if (equipmentButtonPrefab == null) return;

        characterEquipmentPanel.SetActive(true);
        CreateCharacterEquipmentButtons();
    }

    public void CloseCharacterEquipmentList()
    {
        if(!TutorialController.DetailAllows(TutorialInput.Navigate))return;
        if (characterEquipmentPanel != null)
        {
            characterEquipmentPanel.SetActive(false);
        }
    }

    public void CloseSkillEquipmentList()
    {
        if (skillEquipmentPanel != null)
        {
            skillEquipmentPanel.SetActive(false);
        }
    }

    public void OnCharacterEquipmentBackgroundClicked()
    {
        CloseCharacterEquipmentList();
    }

    public void OnSkillEquipmentBackgroundClicked()
    {
        CloseSkillEquipmentList();
    }

    private void CreateCharacterEquipmentButtons()
    {
        foreach (Transform child in characterEquipmentButtonParent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        CreateEquipmentButton("None", "");

        EquipmentData[] equipments = GameDatabase.Instance.GetAllEquipments();

        foreach (EquipmentData equipment in equipments)
        {
            if (!ShouldShowInCurrentCharacterEquipmentList(equipment))
            {
                continue;
            }

            string label = equipment.equipmentName;

            if (currentCharacterEquipmentMode == "article")
            {
                label += " [Article]";
            }

            if (currentCharacterEquipmentMode == "adjective" && !DamageCalculator.AppliesToCharacter(equipment))
            {
                label += " (no effect)";
            }

            CreateEquipmentButton(label, equipment.id);
        }
    }

    private bool ShouldShowInCurrentCharacterEquipmentList(EquipmentData equipment)
    {
        if (equipment == null) return false;

        if (currentCharacterEquipmentMode == "article")
        {
            return equipment.equipmentCategory == "article" || equipment.partOfSpeech == "article";
        }

        if (currentCharacterEquipmentMode == "adjective")
        {
            bool isAdjective =
                equipment.partOfSpeech == "adjective" ||
                equipment.partOfSpeech == "adjective_adverb";

            bool isCharacterModifier =
                equipment.equipmentCategory == "modifier" &&
                (equipment.targetRole == "character" || equipment.targetRole == "both");

            return isAdjective || isCharacterModifier;
        }

        return false;
    }

    private void CreateEquipmentButton(string label, string equipmentId)
    {
        GameObject buttonObj = Instantiate(equipmentButtonPrefab, characterEquipmentButtonParent);
        buttonObj.SetActive(true);
        buttonObj.name="CharacterEquipmentButton_"+equipmentId;

        TMP_Text buttonText = buttonObj.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            if(string.IsNullOrEmpty(equipmentId))LocalizedText.Set(buttonText,"ui.none");
            else if(label.EndsWith(" (no effect)"))LocalizedText.Set(buttonText,"ui.no.effect.named",label.Substring(0,label.Length-12));
            else {buttonText.text=label;LocalizedText.Learning(buttonText);}
        }

        Button button = buttonObj.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => EquipCharacterEquipment(equipmentId));
        }
    }

    private void EquipCharacterEquipment(string equipmentId)
    {
        if(!TutorialController.DetailAllows(TutorialInput.EquipmentSelect,equipmentId))return;
        if (!CanEdit) return;
        EquipmentData equipment = GameDatabase.Instance.GetEquipment(equipmentId);

        if (currentCharacterEquipmentMode == "article")
        {
            tempArticleEquipmentId = equipmentId;
            selectedArticleEquipment = equipment;
        }
        else
        {
            tempCharacterEquipmentId = equipmentId;
            selectedCharacterEquipment = equipment;
        }

        hasUnsavedChanges = true;

        UpdateCharacterInfoText();
        UpdateCharacterEquipmentText();
        if(TutorialController.Active?.IsPreparation==true)
        {characterEquipmentPanel.SetActive(false);TutorialController.Active.EquipmentSelected(equipmentId);}
        else CloseCharacterEquipmentList();
    }

    public void Back()
    {
        if(TutorialController.Active?.IsPreparation==true)TutorialController.Active.Abort("Equipment preparation interrupted.");
        if (hasUnsavedChanges)
        {
            if (unsavedConfirmPanel != null)
            {
                unsavedConfirmPanel.SetActive(true);
            }
            else
            {
                Debug.LogWarning("Unsaved equipment changes. Press Confirm to save or add an UnsavedConfirmPanel to discard/cancel.");
            }
            return;
        }

        ReturnToPreviousScene();
    }

    public void ConfirmEquipment()
    {
        if(!TutorialController.DetailAllows(TutorialInput.EquipmentConfirm))return;
        if (!CanEdit) { ReturnToPreviousScene(); return; }
        if (!ApplyEquipmentChanges()) return;
        if(TutorialController.Active?.IsPreparation==true && !TutorialController.Active.EquipmentConfirmed(viewedInstanceId))return;
        hasUnsavedChanges = false;
        ReturnToPreviousScene();
    }

    private bool ApplyEquipmentChanges()
    {
        if (!CanEdit) return false;
        var slots = new CharacterSkillSlotData[character.skillIds.Length];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = new CharacterSkillSlotData {
                skillId = character.skillIds[i],
                equippedAdverbId = tempSkillEquipmentIds[i]
            };
        var draft = new CharacterLoadout {
            instanceId = viewedInstanceId,
            characterId = character.id,
            adjectiveId = tempCharacterEquipmentId,
            articleId = tempArticleEquipmentId,
            skillSlots = slots
        };
        bool preparation = TutorialController.Active?.IsPreparation == true;
        // Preparation crosses scenes only after the existing atomic save accepts the loadout.
        bool committed = preparation ? PlayerStateTransactions.TryChange(data => {
            data.characterEquipment.characters.RemoveAll(saved => saved.instanceId == viewedInstanceId);
            data.characterEquipment.characters.Add(draft);
            return true;
        }, out _) : CharacterLoadoutStore.Commit(draft);
        if (!committed)
        {
            if (preparation) TutorialController.Active.EquipmentSaveFailed();
            else Debug.LogError("Equipment changes could not be committed.", this);
        }
        return committed;
    }

    public void DiscardChangesAndBack()
    {
        if(TutorialController.Active?.IsPreparation==true)TutorialController.Active.Abort("Equipment draft discarded.");
        hasUnsavedChanges = false;

        if (unsavedConfirmPanel != null)
        {
            unsavedConfirmPanel.SetActive(false);
        }

        ReturnToPreviousScene();
    }

    public void CancelBack()
    {
        if (unsavedConfirmPanel != null)
        {
            unsavedConfirmPanel.SetActive(false);
        }
    }

    private void ReturnToPreviousScene()
    {
        if (GameManager.PreviousSceneName == "CharacterCollection")
        {
            SceneManager.LoadScene("CharacterCollection");
        }
        else
        {
            SceneManager.LoadScene("CharacterSelect");
        }
    }

    public void StartBattle()
    {
        if(!TutorialController.DetailAllows(TutorialInput.EquipmentConfirm))return;
        if (!CanEdit)
        {
            Debug.LogWarning("Only owned characters can enter battle.", this);
            return;
        }
        if(!StageSession.ValidateBattle(out string error,false)){TutorialChoiceUI.ShowStageError(error);return;}
        if (!ApplyEquipmentChanges()) return;
        if(TutorialController.Active?.IsPreparation==true && !TutorialController.Active.EquipmentConfirmed(viewedInstanceId))return;
        if(!StageSession.ValidateBattle(out error)){TutorialChoiceUI.ShowStageError(error);return;}
        GameManager.SelectedCharacterInstanceId = viewedInstanceId;
        hasUnsavedChanges = false;
        SceneManager.LoadScene("Battle");
    }

    private void SetupSkillEquipmentButtons()
    {
        if (character == null || character.skillIds == null)
        {
            return;
        }

        if (skillSlotButtons == null || skillSlotButtons.Length == 0)
        {
            Debug.LogError("Skill Slot Buttons are not assigned in CharacterDetailManager Inspector.");
            return;
        }

        if (skillSlotButtonTexts == null || skillSlotButtonTexts.Length == 0)
        {
            Debug.LogError("Skill Slot Button Texts are not assigned in CharacterDetailManager Inspector.");
            return;
        }

        for (int i = 0; i < skillSlotButtons.Length; i++)
        {
            if (skillSlotButtons[i] == null)
            {
                Debug.LogError($"Skill Slot Button at index {i} is not assigned.");
                continue;
            }

            bool hasSkill = i < character.skillIds.Length;

            skillSlotButtons[i].gameObject.SetActive(hasSkill);

            if (!hasSkill) continue;

            if (i >= skillSlotButtonTexts.Length || skillSlotButtonTexts[i] == null)
            {
                Debug.LogError($"Skill Slot Button Text at index {i} is not assigned.");
                continue;
            }

            SkillData skill = GameDatabase.Instance.GetSkill(character.skillIds[i]);
            if (skill == null)
            {
                continue;
            }

            EquipmentData equipment = null;
            if (tempSkillEquipmentIds != null && i < tempSkillEquipmentIds.Length)
            {
                equipment = GameDatabase.Instance.GetEquipment(tempSkillEquipmentIds[i]);
            }

            string prefix = equipment != null ? equipment.equipmentName + " " : "";
            string noEffectSuffix = equipment != null && !DamageCalculator.AppliesToSkill(equipment) ? " (no effect)" : "";

            skillSlotButtonTexts[i].text = prefix + skill.skillName + noEffectSuffix;

            int index = i;
            skillSlotButtons[i].onClick.RemoveAllListeners();
            skillSlotButtons[i].onClick.AddListener(() => OpenSkillEquipmentList(index));
        }

        UpdateSkillEquipmentText();
    }

    public void OpenSkillEquipmentList(int skillIndex)
    {
        if(!TutorialController.DetailAllows(TutorialInput.EquipmentOpen,"skill"))return;
        if (!CanEdit || character.skillIds == null ||
            skillIndex < 0 || skillIndex >= character.skillIds.Length ||
            tempSkillEquipmentIds == null || skillIndex >= tempSkillEquipmentIds.Length ||
            GameDatabase.Instance == null || equipmentButtonPrefab == null ||
            skillEquipmentButtonParent == null || skillEquipmentPanel == null)
        {
            return;
        }

        selectedSkillIndex = skillIndex;

        CloseCharacterEquipmentList();

        if (skillEquipmentPanel == null) return;

        skillEquipmentPanel.SetActive(true);
        CreateSkillEquipmentButtons();
    }

    private void CreateSkillEquipmentButtons()
    {
        if (skillEquipmentButtonParent == null) return;

        foreach (Transform child in skillEquipmentButtonParent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        CreateSkillEquipmentButton("None", "");

        EquipmentData[] equipments = GameDatabase.Instance.GetAllEquipments();

        foreach (EquipmentData equipment in equipments)
        {
            if (!ShouldShowInSkillEquipmentList(equipment))
            {
                continue;
            }

            string label = equipment.equipmentName;

            if (!DamageCalculator.AppliesToSkill(equipment))
            {
                label += " (no effect)";
            }

            CreateSkillEquipmentButton(label, equipment.id);
        }
    }

    private bool ShouldShowInSkillEquipmentList(EquipmentData equipment)
    {
        if (equipment == null) return false;

        return equipment.partOfSpeech == "adverb" ||
               equipment.partOfSpeech == "adjective_adverb" ||
               equipment.targetRole == "skill" ||
               equipment.targetRole == "both";
    }

    private void CreateSkillEquipmentButton(string label, string equipmentId)
    {
        GameObject buttonObj = Instantiate(equipmentButtonPrefab, skillEquipmentButtonParent);
        buttonObj.name = $"SkillEquipmentButton_{label}";
        buttonObj.SetActive(true);

        TMP_Text buttonText = buttonObj.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            if(string.IsNullOrEmpty(equipmentId))LocalizedText.Set(buttonText,"ui.none");
            else if(label.EndsWith(" (no effect)"))LocalizedText.Set(buttonText,"ui.no.effect.named",label.Substring(0,label.Length-12));
            else {buttonText.text=label;LocalizedText.Learning(buttonText);}
        }

        Button button = buttonObj.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => EquipSkillEquipment(equipmentId));
        }
    }

    private void EquipSkillEquipment(string equipmentId)
    {
        if(!TutorialController.DetailAllows(TutorialInput.EquipmentSelect,equipmentId))return;
        if (!CanEdit) return;
        if (selectedSkillIndex < 0) return;

        if (tempSkillEquipmentIds == null || tempSkillEquipmentIds.Length < 4)
        {
            tempSkillEquipmentIds = new string[4] { "", "", "", "" };
        }

        if (selectedSkillIndex >= tempSkillEquipmentIds.Length) return;

        tempSkillEquipmentIds[selectedSkillIndex] = equipmentId;
        hasUnsavedChanges = true;

        SetupSkillEquipmentButtons();

        if (skillEquipmentPanel != null)
        {
            skillEquipmentPanel.SetActive(false);
        }
    }

    private void UpdateSkillEquipmentText()
    {
        if (skillEquipmentText == null)
        {
            Debug.LogError("Skill Equipment Text is not assigned in CharacterDetailManager Inspector.");
            return;
        }

        if (character == null || character.skillIds == null)
        {
            skillEquipmentText.text = "Skill Equipments:\nNone";
            return;
        }

        skillEquipmentText.text = "Skill Equipments:\n";

        for (int i = 0; i < character.skillIds.Length; i++)
        {
            SkillData skill = GameDatabase.Instance.GetSkill(character.skillIds[i]);
            if (skill == null)
            {
                continue;
            }

            EquipmentData equipment = null;
            if (tempSkillEquipmentIds != null && i < tempSkillEquipmentIds.Length)
            {
                equipment = GameDatabase.Instance.GetEquipment(tempSkillEquipmentIds[i]);
            }

            string equipmentName = "None";
            if (equipment != null)
            {
                equipmentName = equipment.equipmentName;

                if (!DamageCalculator.AppliesToSkill(equipment))
                {
                    equipmentName += " (no effect)";
                }
            }

            skillEquipmentText.text += $"{skill.skillName}: {equipmentName}\n";
        }
    }

    private void SetupSceneMode()
    {
        if (battleButton == null) return;

        Button button = battleButton.GetComponent<Button>();
        TMP_Text buttonText = battleButton.GetComponentInChildren<TMP_Text>();

        if (button != null)
        {
            // This button has a scene-authored StartBattle callback. RemoveAllListeners
            // only removes runtime listeners, so replace the event when choosing its mode.
            button.onClick = new Button.ButtonClickedEvent();
        }

        if (GameManager.PreviousSceneName == "CharacterCollection")
        {
            battleButton.SetActive(true);

            if (buttonText != null)
            {
                buttonText.text = "Confirm";
            }

            if (button != null)
            {
                button.onClick.AddListener(ConfirmEquipment);
            }
        }
        else
        {
            battleButton.SetActive(true);

            if (buttonText != null)
            {
                buttonText.text = CanEdit ? "Battle" : "Not owned";
            }

            if (button != null)
            {
                button.interactable = CanEdit;
                button.onClick.AddListener(StartBattle);
            }
        }
    }
}

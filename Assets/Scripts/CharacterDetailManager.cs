using TMPro;
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

    private int selectedSkillIndex = -1;

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
        character =
            GameDatabase.Instance.GetCharacter(
                GameManager.SelectedCharacterId);

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

        SetupSceneMode();

        if (unsavedConfirmPanel != null)
        {
            unsavedConfirmPanel.SetActive(false);
        }
    }

    private void SetupTemporaryEquipmentState()
    {
        tempCharacterEquipmentId = GameManager.SelectedCharacterEquipmentId;

        if (string.IsNullOrEmpty(tempCharacterEquipmentId) && character != null)
        {
            tempCharacterEquipmentId = character.equippedAdjectiveId;
        }

        tempArticleEquipmentId = "";
        if (character != null)
        {
            tempArticleEquipmentId = character.equippedArticleId;
            if (string.IsNullOrEmpty(tempArticleEquipmentId))
            {
                tempArticleEquipmentId = character.equippedCaseMarkerId;
            }
        }

        tempSkillEquipmentIds = new string[4] { "", "", "", "" };

        if (GameManager.PreviousSceneName == "CharacterCollection")
        {
            LoadSkillEquipmentsFromCharacter();
        }
        else if (GameManager.SelectedSkillEquipmentIds != null)
        {
            int count = Mathf.Min(tempSkillEquipmentIds.Length, GameManager.SelectedSkillEquipmentIds.Length);
            for (int i = 0; i < count; i++)
            {
                tempSkillEquipmentIds[i] = GameManager.SelectedSkillEquipmentIds[i];
            }
        }
        else
        {
            LoadSkillEquipmentsFromCharacter();
        }
    }

    private void LoadSkillEquipmentsFromCharacter()
    {
        if (character == null || character.skillIds == null || character.skillSlots == null)
        {
            return;
        }

        int count = Mathf.Min(tempSkillEquipmentIds.Length, character.skillIds.Length);

        for (int i = 0; i < count; i++)
        {
            string skillId = character.skillIds[i];

            for (int j = 0; j < character.skillSlots.Length; j++)
            {
                CharacterSkillSlotData slot = character.skillSlots[j];
                if (slot != null && slot.skillId == skillId)
                {
                    tempSkillEquipmentIds[i] = slot.equippedAdverbId;
                    break;
                }
            }
        }
    }

    private void LoadCharacter()
    {
        if (character == null) return;

        characterNameText.text =
            character.characterName;

        Sprite sprite =
            Resources.Load<Sprite>(
                $"Images/Characters/{character.illustration}");

        if(sprite != null)
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
        string equipmentId = GameManager.SelectedCharacterEquipmentId;

        if (string.IsNullOrEmpty(equipmentId))
        {
            equipmentId = character.equippedAdjectiveId;
        }

        tempCharacterEquipmentId = equipmentId;
        selectedCharacterEquipment = GameDatabase.Instance.GetEquipment(tempCharacterEquipmentId);

        string articleId = character.equippedArticleId;
        if (string.IsNullOrEmpty(articleId))
        {
            articleId = character.equippedCaseMarkerId;
        }

        tempArticleEquipmentId = articleId;
        selectedArticleEquipment = GameDatabase.Instance.GetEquipment(tempArticleEquipmentId);

        UpdateCharacterInfoText();
        UpdateCharacterEquipmentText();
    }

    private void UpdateCharacterInfoText()
    {
        if (character == null) return;

        int attack = character.attack + DamageCalculator.GetCharacterAttackBonus(selectedCharacterEquipment);
        int defense = character.defense + DamageCalculator.GetCharacterDefenseBonus(selectedCharacterEquipment);
        int speed = character.speed + DamageCalculator.GetCharacterSpeedBonus(selectedCharacterEquipment);

        characterInfoText.text =
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
        currentCharacterEquipmentMode = "adjective";

        CloseSkillEquipmentList();

        if (characterEquipmentPanel == null) return;
        if (characterEquipmentButtonParent == null) return;
        if (equipmentButtonPrefab == null) return;

        characterEquipmentPanel.SetActive(true);
        CreateCharacterEquipmentButtons();
    }

    public void OpenArticleEquipmentList()
    {
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

        TMP_Text buttonText = buttonObj.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            buttonText.text = label;
        }

        Button button = buttonObj.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => EquipCharacterEquipment(equipmentId));
        }
    }

    private void EquipCharacterEquipment(string equipmentId)
    {
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
        CloseCharacterEquipmentList();
    }

    public void Back()
    {
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
        ApplyEquipmentChanges();
        hasUnsavedChanges = false;
        ReturnToPreviousScene();
    }

    private void ApplyEquipmentChanges()
    {
        GameManager.SelectedCharacterEquipmentId = tempCharacterEquipmentId;

        if (character != null)
        {
            character.equippedArticleId = tempArticleEquipmentId;
            character.equippedCaseMarkerId = tempArticleEquipmentId;
            character.equippedAdjectiveId = tempCharacterEquipmentId;
        }

        if (tempSkillEquipmentIds != null)
        {
            for (int i = 0; i < tempSkillEquipmentIds.Length; i++)
            {
                GameManager.SetSkillEquipment(i, tempSkillEquipmentIds[i]);
            }
        }

        SaveSkillEquipmentsToCharacter();
    }

    private void SaveSkillEquipmentsToCharacter()
    {
        if (character == null || character.skillIds == null || tempSkillEquipmentIds == null)
        {
            return;
        }

        int count = Mathf.Min(character.skillIds.Length, tempSkillEquipmentIds.Length);
        character.skillSlots = new CharacterSkillSlotData[count];

        for (int i = 0; i < count; i++)
        {
            character.skillSlots[i] = new CharacterSkillSlotData
            {
                skillId = character.skillIds[i],
                equippedAdverbId = tempSkillEquipmentIds[i]
            };
        }
    }

    public void DiscardChangesAndBack()
    {
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
        ApplyEquipmentChanges();
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
            buttonText.text = label;
        }

        Button button = buttonObj.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => EquipSkillEquipment(equipmentId));
        }
    }

    private void EquipSkillEquipment(string equipmentId)
    {
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
            button.onClick.RemoveAllListeners();
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
                buttonText.text = "Battle";
            }

            if (button != null)
            {
                button.onClick.AddListener(StartBattle);
            }
        }
    }
}
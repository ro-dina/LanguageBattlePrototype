using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CharacterSelectManager : MonoBehaviour
{
    [Header("List")]
    [SerializeField] private Transform characterButtonParent;
    [SerializeField] private GameObject characterButtonPrefab;

    [Header("Preview")]
    [SerializeField] private TMP_Text selectedCharacterText;
    [SerializeField] private Image selectedCharacterImage;
    [SerializeField] private AspectRatioFitter selectedCharacterAspectFitter;

    [Header("Individual selection")]
    [SerializeField] private Button previousInstanceButton;
    [SerializeField] private Button nextInstanceButton;
    [SerializeField] private TMP_Text instancePositionText;

    private string selectedId;
    private bool started;
    private void OnEnable() => PlayerStateChanges.Changed += RefreshOwnedList;
    private void OnDisable() => PlayerStateChanges.Changed -= RefreshOwnedList;
    private void RefreshOwnedList() { if (started) { CreateCharacterButtons(); SelectInitialInstance(); } }
    private void SelectInitialInstance()
    {
        var instances = GetSelectableCharacters();
        if (instances.Length > 0)
            SelectCharacter(instances.Any(i => i.instanceId == GameManager.SelectedCharacterInstanceId)
                ? GameManager.SelectedCharacterInstanceId : instances[0].instanceId);
        else { selectedId = null; if (selectedCharacterText != null) selectedCharacterText.text = "No owned characters."; if (selectedCharacterImage != null) selectedCharacterImage.enabled = false; RefreshInstanceControls(); }
    }

    private void Start()
    {
        if (GameDatabase.Instance == null)
        {
            Debug.LogError("Character selection requires a GameDatabase in the scene.", this);
            return;
        }

        previousInstanceButton.onClick.AddListener(PreviousInstance);
        nextInstanceButton.onClick.AddListener(NextInstance);
        started = true;
        CreateCharacterButtons();
        SelectInitialInstance();
    }

    private CharacterInstance[] GetSelectableCharacters() => CharacterInstances.GetAll()
        .Where(i => GameDatabase.Instance.ContainsCharacter(i.characterId))
        .OrderBy(i => GameDatabase.Instance.GetCharacter(i.characterId).characterName).ToArray();

    private void CreateCharacterButtons()
    {
        CharacterInstance[] characters = GetSelectableCharacters().GroupBy(i => i.characterId).Select(g => g.First()).ToArray();

        if (characterButtonParent != null && characterButtonParent.name == "Scroll View")
        {
            Transform content = characterButtonParent.Find("Viewport/Content");
            if (content != null)
            {
                Debug.LogWarning("Character Button Parent was set to Scroll View. Using Viewport/Content instead.");
                characterButtonParent = content;
            }
        }

        if (characterButtonParent == null)
        {
            Debug.LogError("Character Button Parent is not assigned.");
            return;
        }

        if (characterButtonPrefab == null)
        {
            Debug.LogError("Character Button Prefab is not assigned.");
            return;
        }

        foreach (Transform child in characterButtonParent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        Debug.Log($"Creating character buttons: {characters.Length}");

        foreach (CharacterInstance instance in characters)
        {
            CharacterData character = GameDatabase.Instance.GetCharacter(instance.characterId);
            GameObject buttonObj = Instantiate(characterButtonPrefab, characterButtonParent);
            buttonObj.name = $"CharacterButton_{character.id}";
            buttonObj.SetActive(true);

            TMP_Text buttonText = buttonObj.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = character.characterName + " ×" + CharacterInstances.Count(character.id); LocalizedText.Learning(buttonText);
            }
            else
            {
                Debug.LogError($"TMP_Text was not found in character button prefab: {character.id}");
            }

            Transform iconTransform = buttonObj.transform.Find("CharacterIcon");
            Image characterIcon = iconTransform != null
                ? iconTransform.GetComponent<Image>()
                : null;

            if (characterIcon != null)
            {
                Sprite iconSprite = Resources.Load<Sprite>(
                    $"Images/Characters/{character.illustration}");
                characterIcon.sprite = iconSprite;
                characterIcon.preserveAspect = true;
                characterIcon.gameObject.SetActive(iconSprite != null);
            }

            Button button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                string id = instance.characterId;
                // List entries only update the selection preview. Replacing the
                // event also prevents prefab callbacks from causing navigation.
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => SelectSpecies(id));
            }
            else
            {
                Debug.LogError($"Button component was not found in character button prefab: {character.id}");
            }
        }
    }

    private void SelectSpecies(string characterId)
    {
        var instances = CharacterInstances.GetByCharacterId(characterId);
        if (instances.Count == 0) return;
        string current = GameManager.SelectedCharacterInstanceId;
        SelectCharacter(instances.Any(i => i.instanceId == current) ? current : instances[0].instanceId);
    }

    public void PreviousInstance() => CycleInstance(-1);
    public void NextInstance() => CycleInstance(1);
    private void CycleInstance(int direction)
    {
        var current = CharacterInstances.Get(selectedId);
        if (current == null) { SelectInitialInstance(); return; }
        var instances = CharacterInstances.GetByCharacterId(current.characterId);
        int index = instances.FindIndex(i => i.instanceId == selectedId);
        SelectCharacter(instances[(index + direction + instances.Count) % instances.Count].instanceId);
    }
    private void RefreshInstanceControls()
    {
        var current = CharacterInstances.Get(selectedId);
        int count = current == null ? 0 : CharacterInstances.Count(current.characterId);
        previousInstanceButton.interactable = nextInstanceButton.interactable = count > 1;
        instancePositionText.text = current == null ? "No individual" : "Individual " + CharacterInstances.DisplayBadge(selectedId);
    }

    private void SelectCharacter(string instanceId)
    {
        if (!CharacterInstances.Owns(instanceId)) return;
        selectedId = instanceId;
        GameManager.SelectedCharacterInstanceId = instanceId;

        CharacterData character = CharacterLoadoutStore.GetCharacterForBattle(instanceId);

        if (character == null)
        {
            if (selectedCharacterText != null)
            {
                selectedCharacterText.text = "Character unavailable.";
            }
            return;
        }

        if (characterButtonParent != null)
            foreach (var button in characterButtonParent.GetComponentsInChildren<Button>())
                if (button.image != null) button.image.color = button.name == "CharacterButton_" + character.id ? new Color(0.65f, 0.84f, 1f) : Color.white;
        EquipmentData equipment = GameDatabase.Instance.GetEquipment(character.equippedAdjectiveId);
        int attack = character.attack + DamageCalculator.GetCharacterAttackBonus(equipment);
        int defense = character.defense + DamageCalculator.GetCharacterDefenseBonus(equipment);
        int speed = character.speed + DamageCalculator.GetCharacterSpeedBonus(equipment);
        if (selectedCharacterText != null)
        {
            var loadout = CharacterLoadoutStore.GetLoadout(instanceId);
            string adjective = string.IsNullOrEmpty(loadout.adjectiveId) ? "none" : GameDatabase.Instance.GetEquipment(loadout.adjectiveId).equipmentName;
            string article = string.IsNullOrEmpty(loadout.articleId) ? "default" : GameDatabase.Instance.GetEquipment(loadout.articleId).equipmentName;
            string skills = string.Join("\n", loadout.skillSlots.Select(slot =>
                GameDatabase.Instance.GetSkill(slot.skillId).skillName + (string.IsNullOrEmpty(slot.equippedAdverbId) ? "" :
                " + " + GameDatabase.Instance.GetEquipment(slot.equippedAdverbId).equipmentName)));
            LocalizedText.SetProvider(selectedCharacterText, () => Localization.Text("ui.character.preview",
                character.characterName, CharacterInstances.Count(character.id), Localization.Present(character.grammaticalGender),
                character.hp, attack, defense, speed, string.IsNullOrEmpty(loadout.adjectiveId) ? Localization.Text("ui.lower.none") : adjective,
                string.IsNullOrEmpty(loadout.articleId) ? Localization.Text("ui.default") : article, skills));
        }

        RefreshInstanceControls();

        if (selectedCharacterImage == null)
        {
            Debug.LogError("Selected Character Image is not assigned.");
            return;
        }

        selectedCharacterImage.sprite = null;
        selectedCharacterImage.enabled = false;

        Sprite sprite = Resources.Load<Sprite>(
            $"Images/Characters/{character.illustration}"
        );

        if (sprite != null)
        {
            selectedCharacterImage.sprite = sprite;
            selectedCharacterImage.enabled = true;

            if (selectedCharacterAspectFitter != null)
            {
                selectedCharacterAspectFitter.aspectRatio =
                    sprite.rect.width / sprite.rect.height;
            }
        }
        else
        {
            Debug.LogWarning($"Character artwork is not yet available: {character.characterName}");
        }
    }

    public void StartBattle()
    {
        if (string.IsNullOrEmpty(selectedId) || !CharacterInstances.Owns(selectedId))
        {
            return;
        }

        if(StageSession.NeedsEquipmentPreparation(selectedId))
        {GameManager.SelectedCharacterInstanceId=selectedId;GameManager.PreviousSceneName="CharacterSelect";SceneManager.LoadScene("CharacterDetail");return;}
        if(!StageSession.ValidateBattle(out string error)){TutorialChoiceUI.ShowStageError(error);return;}
        GameManager.SelectedCharacterInstanceId = selectedId;
        SceneManager.LoadScene("Battle");
    }

    public void CharacterDetail()
    {
        if (string.IsNullOrEmpty(selectedId) || !CharacterInstances.Owns(selectedId))
        {
            return;
        }

        StageSession.NeedsEquipmentPreparation(selectedId);
        GameManager.SelectedCharacterInstanceId = selectedId;
        GameManager.PreviousSceneName = "CharacterSelect";
        SceneManager.LoadScene("CharacterDetail");
    }
}

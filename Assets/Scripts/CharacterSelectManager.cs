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
        else { selectedId = null; if (selectedCharacterText != null) selectedCharacterText.text = "No owned characters."; if (selectedCharacterImage != null) selectedCharacterImage.enabled = false; }
    }

    private void Start()
    {
        if (GameDatabase.Instance == null)
        {
            Debug.LogError("Character selection requires a GameDatabase in the scene.", this);
            return;
        }

        started = true;
        CreateCharacterButtons();
        SelectInitialInstance();
    }

    private CharacterInstance[] GetSelectableCharacters() => CharacterInstances.GetAll()
        .Where(i => GameDatabase.Instance.ContainsCharacter(i.characterId))
        .OrderBy(i => GameDatabase.Instance.GetCharacter(i.characterId).characterName).ThenBy(i => i.instanceId).ToArray();

    private void CreateCharacterButtons()
    {
        CharacterInstance[] characters = GetSelectableCharacters();

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
            buttonObj.name = $"CharacterButton_{instance.instanceId}";
            buttonObj.SetActive(true);

            TMP_Text buttonText = buttonObj.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = CharacterInstances.DisplayName(instance.instanceId) + "\nOwned: " + CharacterInstances.Count(character.id);
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
                string id = instance.instanceId;
                // List entries only update the selection preview. Replacing the
                // event also prevents prefab callbacks from causing navigation.
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => SelectCharacter(id));
            }
            else
            {
                Debug.LogError($"Button component was not found in character button prefab: {character.id}");
            }
        }
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
                if (button.image != null) button.image.color = button.name == "CharacterButton_" + instanceId ? new Color(0.65f, 0.84f, 1f) : Color.white;
        EquipmentData equipment = GameDatabase.Instance.GetEquipment(character.equippedAdjectiveId);
        int attack = character.attack + DamageCalculator.GetCharacterAttackBonus(equipment);
        int defense = character.defense + DamageCalculator.GetCharacterDefenseBonus(equipment);
        int speed = character.speed + DamageCalculator.GetCharacterSpeedBonus(equipment);
        if (selectedCharacterText != null)
        {
            selectedCharacterText.text =
                $"{CharacterInstances.DisplayName(instanceId)}\nOwned: {CharacterInstances.Count(character.id)}\n" +
                $"Gender:\n{character.grammaticalGender}\n" +
                $"HP: {character.hp}\n" +
                $"ATK: {attack} /\nDEF: {defense} /\nSPD: {speed}";
        }

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
            Debug.LogError($"Character select sprite not found: {character.illustration}");
        }
    }

    public void StartBattle()
    {
        if (string.IsNullOrEmpty(selectedId) || !CharacterInstances.Owns(selectedId))
        {
            return;
        }

        GameManager.SelectedCharacterInstanceId = selectedId;
        SceneManager.LoadScene("Battle");
    }

    public void CharacterDetail()
    {
        if (string.IsNullOrEmpty(selectedId) || !CharacterInstances.Owns(selectedId))
        {
            return;
        }

        GameManager.SelectedCharacterInstanceId = selectedId;
        GameManager.PreviousSceneName = "CharacterSelect";
        SceneManager.LoadScene("CharacterDetail");
    }
}

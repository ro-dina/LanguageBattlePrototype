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

    private void Start()
    {
        if (GameDatabase.Instance == null)
        {
            Debug.LogError("Character selection requires a GameDatabase in the scene.", this);
            return;
        }

        CreateCharacterButtons();

        CharacterData[] characters = GetSelectableCharacters();
        if (characters.Length > 0)
        {
            string initial = characters.Any(c => c.id == GameManager.SelectedCharacterId)
                ? GameManager.SelectedCharacterId : characters[0].id;
            SelectCharacter(initial);
        }
    }

    private CharacterData[] GetSelectableCharacters() => GameDatabase.Instance.GetAllCharacters()
        .Where(c => c != null && OwnedCharacters.IsCharacterOwned(c.id)).ToArray();

    private void CreateCharacterButtons()
    {
        CharacterData[] characters = GetSelectableCharacters();

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

        foreach (CharacterData character in characters)
        {
            GameObject buttonObj = Instantiate(characterButtonPrefab, characterButtonParent);
            buttonObj.name = $"CharacterButton_{character.id}";
            buttonObj.SetActive(true);

            TMP_Text buttonText = buttonObj.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = character.characterName;
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
                string id = character.id;
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

    private void SelectCharacter(string characterId)
    {
        if (!OwnedCharacters.IsCharacterOwned(characterId)) return;
        selectedId = characterId;
        GameManager.SelectedCharacterId = characterId;

        CharacterData character = CharacterLoadoutStore.GetCharacterForBattle(characterId);

        if (character == null)
        {
            if (selectedCharacterText != null)
            {
                selectedCharacterText.text = $"Selected: {characterId}";
            }
            return;
        }

        EquipmentData equipment = GameDatabase.Instance.GetEquipment(character.equippedAdjectiveId);
        int attack = character.attack + DamageCalculator.GetCharacterAttackBonus(equipment);
        int defense = character.defense + DamageCalculator.GetCharacterDefenseBonus(equipment);
        int speed = character.speed + DamageCalculator.GetCharacterSpeedBonus(equipment);
        if (selectedCharacterText != null)
        {
            selectedCharacterText.text =
                $"{character.characterName}\n" +
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
        if (string.IsNullOrEmpty(selectedId) || !OwnedCharacters.IsCharacterOwned(selectedId))
        {
            return;
        }

        GameManager.SelectedCharacterId = selectedId;
        SceneManager.LoadScene("Battle");
    }

    public void CharacterDetail()
    {
        if (string.IsNullOrEmpty(selectedId) || !OwnedCharacters.IsCharacterOwned(selectedId))
        {
            return;
        }

        GameManager.SelectedCharacterId = selectedId;
        GameManager.PreviousSceneName = "CharacterSelect";
        SceneManager.LoadScene("CharacterDetail");
    }
}

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
        CreateCharacterButtons();

        CharacterData[] characters = GameDatabase.Instance.GetAllCharacters();
        if (characters.Length > 0)
        {
            SelectCharacter(characters[0].id);
        }
    }

    private void CreateCharacterButtons()
    {
        CharacterData[] characters = GameDatabase.Instance.GetAllCharacters();

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

            Button button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                string id = character.id;
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
        selectedId = characterId;
        GameManager.SelectedCharacterId = characterId;

        CharacterData character = GameDatabase.Instance.GetCharacter(characterId);

        if (character == null)
        {
            selectedCharacterText.text = $"Selected: {characterId}";
            return;
        }

        selectedCharacterText.text =
            $"{character.characterName}\n" +
            $"Gender:\n{character.grammaticalGender}\n" +
            $"HP: {character.hp}\n" +
            $"ATK: {character.attack} /\nDEF: {character.defense} /\nSPD: {character.speed}";

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
        if (string.IsNullOrEmpty(selectedId))
        {
            return;
        }

        GameManager.SelectedCharacterId = selectedId;
        SceneManager.LoadScene("Battle");
    }

    public void CharacterDetail()
    {
        if (string.IsNullOrEmpty(selectedId))
        {
            return;
        }

        GameManager.SelectedCharacterId = selectedId;
        GameManager.PreviousSceneName = "CharacterSelect";
        SceneManager.LoadScene("CharacterDetail");
    }
}
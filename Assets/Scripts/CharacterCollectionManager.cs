using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class CharacterCollectionManager : MonoBehaviour
{
    [SerializeField] private Transform characterButtonParent;
    [SerializeField] private GameObject characterButtonPrefab;
    [SerializeField] private TMP_Text selectedCharacterText;

    [SerializeField] private TMP_InputField searchInputField;
    [SerializeField] private TMP_Dropdown genderDropdown;
    [SerializeField] private TMP_Dropdown sortDropdown;

    private CharacterData[] allCharacters;

    private string selectedCharacterId = "";

    private void Start()
    {
        if (selectedCharacterText != null)
        {
            selectedCharacterText.text = "Select a character";
        }

        if (GameDatabase.Instance == null)
        {
            Debug.LogError("Character collection requires a GameDatabase in the scene.", this);
            return;
        }

        allCharacters = GameDatabase.Instance.GetAllCharacters();

        if (searchInputField != null)
        {
            searchInputField.onValueChanged.AddListener(OnSearchChanged);
        }

        if (genderDropdown != null)
        {
            genderDropdown.onValueChanged.AddListener(OnFilterChanged);
        }

        if (sortDropdown != null)
        {
            sortDropdown.onValueChanged.AddListener(OnFilterChanged);
        }

        RefreshCharacterButtons();
    }

    private void OnSearchChanged(string _) => RefreshCharacterButtons();
    private void OnFilterChanged(int _) => RefreshCharacterButtons();

    private void OnDestroy()
    {
        if (searchInputField != null)
            searchInputField.onValueChanged.RemoveListener(OnSearchChanged);
        if (genderDropdown != null)
            genderDropdown.onValueChanged.RemoveListener(OnFilterChanged);
        if (sortDropdown != null)
            sortDropdown.onValueChanged.RemoveListener(OnFilterChanged);
    }

    private void RefreshCharacterButtons()
    {
        if (characterButtonParent == null)
        {
            Debug.LogError("Character Button Parent is not assigned. Set it to Scroll View / Viewport / Content in the CharacterCollectionManager Inspector.");
            return;
        }

        if (characterButtonPrefab == null)
        {
            Debug.LogError("Character Button Prefab is not assigned. Set it to CharacterCardButton prefab in the CharacterCollectionManager Inspector.");
            return;
        }

        if (GameDatabase.Instance == null)
        {
            Debug.LogError("GameDatabase.Instance is null. Put GameDatabase in this scene or make it DontDestroyOnLoad from the first scene.");
            return;
        }

        foreach (Transform child in characterButtonParent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        List<CharacterData> result = (allCharacters ?? new CharacterData[0])
            .Where(character => character != null).ToList();

        string keyword = searchInputField != null ? searchInputField.text.ToLower() : "";

        if (!string.IsNullOrEmpty(keyword))
        {
            result = result
                .Where(c => (c.characterName ?? "").ToLower().Contains(keyword))
                .ToList();
        }

        if (genderDropdown != null && genderDropdown.value >= 0 &&
            genderDropdown.value < genderDropdown.options.Count)
        {
            string gender = genderDropdown.options[genderDropdown.value].text;

            if (gender != "All")
            {
                result = result
                    .Where(c => c.grammaticalGender == gender)
                    .ToList();
            }
        }

        if (sortDropdown != null && sortDropdown.value >= 0 &&
            sortDropdown.value < sortDropdown.options.Count)
        {
            string sort = sortDropdown.options[sortDropdown.value].text;

            if (sort == "Name")
            {
                result = result.OrderBy(c => c.characterName).ToList();
            }
            else if (sort == "Rarity")
            {
                result = result.OrderByDescending(c => c.rarity).ToList();
            }
            else if (sort == "HP")
            {
                result = result.OrderByDescending(c => c.hp).ToList();
            }
        }

        Debug.Log($"Creating collection character buttons: {result.Count}");

        foreach (CharacterData character in result)
        {
            GameObject buttonObj = Instantiate(characterButtonPrefab, characterButtonParent);
            buttonObj.SetActive(true);

            TMP_Text text = buttonObj.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = character.characterName + (OwnedCharacters.IsCharacterOwned(character.id) ? "" : " [Not owned]");
            }

            Image icon = null;
            Transform iconTransform = buttonObj.transform.Find("CharacterIcon");
            if (iconTransform != null)
            {
                icon = iconTransform.GetComponent<Image>();
            }

            if (icon != null)
            {
                Sprite sprite = Resources.Load<Sprite>($"Images/Characters/{character.illustration}");

                if (sprite != null)
                {
                    icon.sprite = sprite;
                    icon.preserveAspect = true;
                    icon.gameObject.SetActive(true);
                }
                else
                {
                    icon.gameObject.SetActive(false);
                }
            }

            Button button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                string id = character.id;
                string name = character.characterName;
                button.onClick.AddListener(() => SelectCharacter(id, name));
            }
        }
    }

    private void SelectCharacter(string characterId, string characterName)
    {
        selectedCharacterId = characterId;
        GameManager.SelectedCharacterId = characterId;

        if (selectedCharacterText != null)
        {
            selectedCharacterText.text = characterName + (OwnedCharacters.IsCharacterOwned(characterId) ? "" : " [Not owned]");
        }
    }

    public void OpenSelectedCharacterDetail()
    {
        if (string.IsNullOrEmpty(selectedCharacterId))
        {
            Debug.LogWarning("No character selected.");
            return;
        }

        GameManager.SelectedCharacterId = selectedCharacterId;
        GameManager.PreviousSceneName = "CharacterCollection";
        SceneManager.LoadScene("CharacterDetail");
    }

    public void BackToHome()
    {
        SceneManager.LoadScene("GermanyHome");
    }
}

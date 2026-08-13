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

        allCharacters = GameDatabase.Instance.GetAllCharacters();

        if (searchInputField != null)
        {
            searchInputField.onValueChanged.AddListener(_ => RefreshCharacterButtons());
        }

        if (genderDropdown != null)
        {
            genderDropdown.onValueChanged.AddListener(_ => RefreshCharacterButtons());
        }

        if (sortDropdown != null)
        {
            sortDropdown.onValueChanged.AddListener(_ => RefreshCharacterButtons());
        }

        RefreshCharacterButtons();
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
            Destroy(child.gameObject);
        }

        List<CharacterData> result = new List<CharacterData>(allCharacters);

        string keyword = searchInputField != null ? searchInputField.text.ToLower() : "";

        if (!string.IsNullOrEmpty(keyword))
        {
            result = result
                .Where(c => c.characterName.ToLower().Contains(keyword))
                .ToList();
        }

        if (genderDropdown != null)
        {
            string gender = genderDropdown.options[genderDropdown.value].text;

            if (gender != "All")
            {
                result = result
                    .Where(c => c.grammaticalGender == gender)
                    .ToList();
            }
        }

        if (sortDropdown != null)
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
                text.text = character.characterName;
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
            selectedCharacterText.text = characterName;
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
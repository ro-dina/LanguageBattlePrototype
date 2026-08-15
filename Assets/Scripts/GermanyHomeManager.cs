using UnityEngine;
using UnityEngine.SceneManagement;

public class GermanyHomeManager : MonoBehaviour
{
    public void GoStageSelect()
    {
        SceneManager.LoadScene("StageSelect");
    }

    public void GoCharacters()
    {
        SceneManager.LoadScene("CharacterCollection");
    }

    public void GoGacha()
    {
        SceneManager.LoadScene("Gacha");
    }

    public void GoInventory()
    {
        SceneManager.LoadScene("Inventory");
    }

    public void GoCrafting()
    {
        SceneManager.LoadScene("Crafting");
    }

    public void GoMissions()
    {
        Debug.Log("Missions are coming soon.");
    }

    public void GoDictionary()
    {
        Debug.Log("Dictionary is coming soon.");
    }

    public void GoSettings()
    {
        Debug.Log("Settings is coming soon.");
    }

    public void BackToEuropeMap()
    {
        SceneManager.LoadScene("EuropeMap");
    }
}

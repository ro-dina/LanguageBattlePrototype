using UnityEngine;
using UnityEngine.SceneManagement;

public class GermanyHomeManager : MonoBehaviour
{
    private void Start()
    {
        StageSession.Clear();
        TitleScreenController.InstallHomeEntry(transform);
    }
    public void GoTitle()
    {
        if (SettingsUI.IsOpen || DialogueUI.IsOpen || ProgressResetUI.Active != null || CharacterInstances.HasActiveBattle) return;
        SceneManager.LoadScene(GameStartService.TitleScene);
    }
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
        SettingsUI.Show();
    }

    public void BackToEuropeMap()
    {
        SceneManager.LoadScene("EuropeMap");
    }
}

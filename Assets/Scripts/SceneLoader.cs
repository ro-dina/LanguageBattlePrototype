using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadBattle()
    {
        SceneManager.LoadScene("CharacterSelect");
    }

    public void LoadBattleDirect()
    {
        SceneManager.LoadScene("Battle");
    }

    public void LoadStage1()
    {
        GameManager.EnemyCharacterId = "feuer";
        SceneManager.LoadScene("CharacterSelect");
    }

    public void LoadClear()
    {
        GameManager.IsWin = true;
        SceneManager.LoadScene("Result");
    }

    public void LoadStageSelect()
    {
        SceneManager.LoadScene("StageSelect");
    }
}

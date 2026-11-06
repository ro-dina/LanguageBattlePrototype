using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadHome()
    { if (TutorialController.NavigationAllowed) SceneManager.LoadScene("GermanyHome"); }
    public void LoadBattle()
    {
        if(!TutorialController.NavigationAllowed)return;
        if(!StageSession.Select("feuer_practice",false,out _))return;
        SceneManager.LoadScene("CharacterSelect");
    }

    public void LoadBattleDirect()
    {
        if(!TutorialController.NavigationAllowed)return;
        if(!StageSession.ValidateBattle(out string error)){TutorialChoiceUI.ShowStageError(error);return;}
        SceneManager.LoadScene("Battle");
    }

    public void LoadStage1()
    {
        TutorialChoiceUI.Request("stage_1",false);
    }

    public void LoadClear()
    {
        if(!TutorialController.NavigationAllowed)return;
        GameManager.IsWin = true;
        SceneManager.LoadScene("Result");
    }

    public void LoadStageSelect()
    {
        if(!TutorialController.NavigationAllowed)return;
        SceneManager.LoadScene("StageSelect");
    }
}

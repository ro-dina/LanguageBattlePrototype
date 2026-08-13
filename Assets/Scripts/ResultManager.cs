using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ResultManager : MonoBehaviour
{
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Image backgroundImage;

    [SerializeField] private Sprite winBackground;
    [SerializeField] private Sprite loseBackground;

    void Start()
    {
        if (GameManager.IsWin)
        {
            resultText.text = "YOU WIN!";

            if (backgroundImage != null && winBackground != null)
            {
                backgroundImage.sprite = winBackground;
            }
        }
        else
        {
            resultText.text = "YOU LOSE...";

            if (backgroundImage != null && loseBackground != null)
            {
                backgroundImage.sprite = loseBackground;
            }
        }
    }

    public void GoStageSelect()
    {
        SceneManager.LoadScene("StageSelect");
    }

    public void GoHome()
    {
        SceneManager.LoadScene("GermanyHome");
    }
}
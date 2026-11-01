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
    [SerializeField] private GameObject rewardPanel;
    [SerializeField] private TMP_Text rewardText;
    public string RewardSummary => rewardText != null ? rewardText.text : "";

    void Start()
    {
        var receipt = BattleRewardService.Latest;
        bool completed = receipt != null && receipt.status != BattleRewardStatus.Pending;
        bool victory = completed ? receipt.status == BattleRewardStatus.Victory : GameManager.IsWin;
        if (rewardPanel != null) rewardPanel.SetActive(victory && completed);
        if (rewardText != null)
        {
            rewardText.text = victory && completed ? FormatRewards(receipt) : "";
            rewardText.rectTransform.anchoredPosition = Vector2.zero;
        }
        if (victory)
        {
            if (resultText != null)
            {
                resultText.text = "YOU WIN!";
            }

            if (backgroundImage != null && winBackground != null)
            {
                backgroundImage.sprite = winBackground;
            }
        }
        else
        {
            if (resultText != null)
            {
                resultText.text = "YOU LOSE...";
            }

            if (backgroundImage != null && loseBackground != null)
            {
                backgroundImage.sprite = loseBackground;
            }
        }
    }

    public static string FormatRewards(BattleRewardRecord receipt)
    {
        if (receipt == null || receipt.status != BattleRewardStatus.Victory) return "";
        var text = new System.Text.StringBuilder();
        text.Append(GameDatabase.EnsureAvailable().GetCharacter(receipt.enemyCharacterId).characterName).Append(" defeated!\n\nRewards\n");
        if (receipt.coins > 0) text.Append(EconomySettings.Current.currencyDisplayName).Append(" +").Append(EconomySettings.Amount(receipt.coins)).Append("\n");
        if (receipt.firstClearCoins > 0) text.Append("  Normal: ").Append(EconomySettings.Amount(receipt.coins-receipt.firstClearCoins)).Append(" | First clear: +").Append(EconomySettings.Amount(receipt.firstClearCoins)).Append("\n");
        if (!string.IsNullOrEmpty(receipt.stageId)) text.Append(StageCatalog.Get(receipt.stageId).displayName).Append(" cleared!\n");
        if (receipt.tutorialCompleted) text.Append("Tutorial completed!\n");
        if (receipt.materials.Count > 0)
        {
            text.Append("\nMaterials\n");
            foreach (var entry in receipt.materials) text.Append(entry.text).Append(" ×").Append(entry.count).Append("\n");
        }
        if (!string.IsNullOrEmpty(receipt.characterId)) text.Append("\nCharacter obtained!\n").Append(GameDatabase.Instance.GetCharacter(receipt.characterId).characterName).Append(" ×1\n");
        return text.ToString();
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

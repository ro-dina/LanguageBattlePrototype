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
            LocalizedText.SetProvider(rewardText,()=>victory && completed ? FormatRewards(receipt) : "");
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
        text.Append(GameDatabase.EnsureAvailable().GetCharacter(receipt.enemyCharacterId).characterName).Append(Localization.Text("ui.reward.defeated"));
        if (receipt.coins > 0) text.Append(EconomySettings.Current.currencyDisplayName).Append(" +").Append(EconomySettings.Amount(receipt.coins)).Append("\n");
        if (receipt.firstClearCoins > 0) text.Append(Localization.Text("ui.reward.normal")).Append(EconomySettings.Amount(receipt.coins-receipt.firstClearCoins)).Append(Localization.Text("ui.reward.first")).Append(EconomySettings.Amount(receipt.firstClearCoins)).Append("\n");
        if (!string.IsNullOrEmpty(receipt.stageId)) text.Append(receipt.stageId=="feuer_practice"?Localization.Text("ui.practice.feuer"):StageCatalog.Get(receipt.stageId).displayName).Append(Localization.Text("ui.reward.cleared"));
        if (receipt.tutorialCompleted) text.Append(Localization.Text("ui.reward.tutorial"));
        if (receipt.materials.Count > 0)
        {
            text.Append(Localization.Text("ui.reward.materials"));
            foreach (var entry in receipt.materials) text.Append(entry.text).Append(" ×").Append(entry.count).Append("\n");
        }
        if (!string.IsNullOrEmpty(receipt.characterId)) text.Append(Localization.Text("ui.reward.character")).Append(GameDatabase.Instance.GetCharacter(receipt.characterId).characterName).Append(" ×1\n");
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

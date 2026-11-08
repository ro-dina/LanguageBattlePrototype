using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class StageRowUI:MonoBehaviour
{
    [SerializeField] private TMP_Text information;
    [SerializeField] private Button challenge;
    [SerializeField] private Button review;
    public void Bind(StageDefinition stage)
    {
        bool unlocked=StageProgress.IsUnlocked(stage.stageId);bool cleared=StageProgress.IsCleared(stage.stageId);
        string state=unlocked?(cleared?"Cleared • "+StageProgress.ClearCount(stage.stageId):"Ready"):"Locked";
        string guidance=string.IsNullOrEmpty(stage.tutorialId)?"Free battle":stage.tutorialType+(TutorialCatalog.IsImplemented(stage.tutorialId)?"":" • Guidance coming soon");
        LocalizedText.SetProvider(information,()=>Localization.Text("ui.stage.row",
            Localization.Present(stage.stageNumber==0?"Practice":"Stage "+stage.stageNumber), Localization.Present(stage.displayName), Localization.Present(state),
            GameDatabase.Instance.GetCharacter(stage.enemyCharacterId).characterName, EconomySettings.Price(stage.firstClearCoins), Localization.Present(guidance)));
        challenge.interactable=unlocked;LocalizedText.Set(challenge.GetComponentInChildren<TMP_Text>(),unlocked?(cleared?"ui.retry":"ui.challenge"):"ui.locked");
        challenge.onClick.AddListener(()=>TutorialChoiceUI.Request(stage.stageId,false));
        review.gameObject.SetActive(!string.IsNullOrEmpty(stage.tutorialId));review.interactable=unlocked;
        LocalizedText.Set(review.GetComponentInChildren<TMP_Text>(),"ui.tutorial");review.onClick.AddListener(()=>TutorialChoiceUI.Request(stage.stageId,true));
    }
}

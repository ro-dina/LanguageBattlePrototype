using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ProgressResetDataUI : MonoBehaviour
{
    [SerializeField] private Button newGameButton, tutorialButton, stageButton, developmentNewGameButton;
    [SerializeField] private GameObject developmentGroup;
    [SerializeField] private TMP_Text title, explanation;
    public static void Install(Transform parent)
    {
        foreach (Transform child in parent) child.gameObject.SetActive(false);
        var asset = Resources.Load<GameObject>("UI/ProgressResetDataUI");
        if (asset != null) Instantiate(asset, parent);
    }
    private void Start()
    {
        LocalizedText.Set(title, "reset.data.title"); LocalizedText.Set(explanation, "reset.data.explanation");
        Bind(newGameButton, ProgressResetKind.NewGame, "reset.new.game");
        Bind(tutorialButton, ProgressResetKind.Tutorials, "reset.tutorials");
        Bind(stageButton, ProgressResetKind.Stages, "reset.stages");
        Bind(developmentNewGameButton, ProgressResetKind.DevelopmentNewGame, "reset.development.new.game");
        developmentGroup.SetActive(ProgressResetAccess.DevelopmentEnabled); Refresh();
    }
    private void Update() => Refresh();
    private void Refresh()
    {
        newGameButton.interactable = ProgressResetService.CanReset(ProgressResetKind.NewGame);
        tutorialButton.interactable = ProgressResetService.CanReset(ProgressResetKind.Tutorials);
        stageButton.interactable = ProgressResetService.CanReset(ProgressResetKind.Stages);
        developmentNewGameButton.interactable = ProgressResetService.CanReset(ProgressResetKind.DevelopmentNewGame);
    }
    private void Bind(Button button, ProgressResetKind kind, string key)
    { LocalizedText.Set(button.GetComponentInChildren<TMP_Text>(), key); button.onClick.AddListener(() => ProgressResetUI.Confirm(kind)); }
}

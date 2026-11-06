using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TitleScreenController : MonoBehaviour
{
    [SerializeField] private Button continueButton, newGameButton, languageButton, accountButton, settingsButton, confirmButton, cancelButton;
    [SerializeField] private TMP_Text gameTitle, progressMessage, versionLabel, languageLabel, modalTitle, modalDescription;
    [SerializeField] private GameObject modal;
    [SerializeField] private CanvasGroup menu;
    [SerializeField] private CanvasScaler scaler;
    [SerializeField] private ScrollRect menuScroll, dialogScroll;
    private Vector2 lastScreen;
    private bool confirmingNewGame, processing;
    private string selectedLanguage = "de";
    private string statusKey;
    private GameObject previousSelection;
    public static TitleScreenController Active { get; private set; }
    public static bool IsModalOpen => Active != null && Active.modal.activeSelf;
    public string SelectedLearningLanguage => selectedLanguage;
    public ProgressInspection Progress { get; private set; }
    private void Awake()
    {
        Active = this;
        modal.SetActive(false);
        continueButton.onClick.AddListener(Continue);
        newGameButton.onClick.AddListener(BeginNewGame);
        languageButton.onClick.AddListener(() => SelectLearningLanguage("de"));
        accountButton.onClick.AddListener(ShowAccountNotice);
        settingsButton.onClick.AddListener(() => SettingsUI.Show());
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(CloseModal);
        UserSettings.Changed += Refresh;
        Refresh();
    }
    private void Update() => RefreshLayout(new Vector2(Screen.width, Screen.height));
    private void RefreshLayout(Vector2 screen)
    {
        if (screen == lastScreen) return;
        lastScreen = screen;
        bool narrow = screen.x < 960 || screen.x < screen.y;
        scaler.referenceResolution = narrow ? new Vector2(840,1600) : new Vector2(1920,1080);
        scaler.screenMatchMode = narrow ? CanvasScaler.ScreenMatchMode.MatchWidthOrHeight : CanvasScaler.ScreenMatchMode.Expand;
        scaler.matchWidthOrHeight = 0;
        Canvas.ForceUpdateCanvases();
        PositionPanel(menuScroll);
        PositionPanel(dialogScroll);
        if (modal.activeSelf) menuScroll.enabled = false;
    }
    private static void PositionPanel(ScrollRect scroll)
    {
        float top = Mathf.Max(24, (scroll.viewport.rect.height - scroll.content.sizeDelta.y) / 2);
        scroll.content.anchoredPosition = new Vector2(0,-top);
        scroll.StopMovement();
        scroll.enabled = scroll.content.rect.height > scroll.viewport.rect.height;
    }
    public bool SelectLearningLanguage(string id)
    {
        if (processing || IsModalOpen || SettingsUI.IsOpen || !LearningLanguageCatalog.CanPlay(id)) return false;
        selectedLanguage = id; Refresh(); return true;
    }
    public void Refresh()
    {
        Progress = GameStartService.Inspect();
        LocalizedText.Set(gameTitle, "title.game.name");
        LocalizedText.Set(continueButton.GetComponentInChildren<TMP_Text>(), "title.continue");
        LocalizedText.Set(newGameButton.GetComponentInChildren<TMP_Text>(), "title.new");
        LocalizedText.Set(accountButton.GetComponentInChildren<TMP_Text>(), "title.account");
        LocalizedText.Set(settingsButton.GetComponentInChildren<TMP_Text>(), "ui.settings");
        LocalizedText.Set(languageLabel, "title.language");
        LocalizedText.Set(languageButton.GetComponentInChildren<TMP_Text>(), "title.language.selected", LearningLanguageCatalog.All.Single(l=>l.id==selectedLanguage).displayName);
        LocalizedText.Set(progressMessage, statusKey ?? Progress.MessageKey);
        LocalizedText.Set(versionLabel, "title.version", Application.version);
        continueButton.interactable = !processing && Progress.CanContinue;
        newGameButton.interactable = !processing && Progress.CanStartNew;
        if (modal.activeSelf) RefreshModal();
    }
    public void Continue()
    {
        if (processing || IsModalOpen || SettingsUI.IsOpen) return;
        processing = true;
        if (GameStartService.Continue(out string error)) { SceneManager.LoadScene(GameStartService.HomeScene); return; }
        processing = false; statusKey = error; Refresh();
    }
    public void BeginNewGame()
    {
        if (processing || IsModalOpen || SettingsUI.IsOpen) return;
        Refresh(); if (!Progress.CanStartNew || !LearningLanguageCatalog.CanPlay(selectedLanguage)) return;
        confirmingNewGame = true; OpenModal();
    }
    public void ShowAccountNotice()
    {
        if (processing || IsModalOpen || SettingsUI.IsOpen) return;
        confirmingNewGame = false; OpenModal();
    }
    private void OpenModal()
    {
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        menu.interactable = false; menuScroll.enabled = false; modal.SetActive(true); RefreshModal();
        Canvas.ForceUpdateCanvases(); PositionPanel(dialogScroll);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
    }
    private void RefreshModal()
    {
        LocalizedText.Set(modalTitle, confirmingNewGame ? "title.new.confirm.title" : "title.account");
        LocalizedText.Set(modalDescription, confirmingNewGame ? (Progress.Status == SaveLoadStatus.NewGame ? "title.new.confirm.first" : "title.new.confirm.replace") : "title.account.soon");
        confirmButton.gameObject.SetActive(confirmingNewGame);
        LocalizedText.Set(confirmButton.GetComponentInChildren<TMP_Text>(), "title.start");
        LocalizedText.Set(cancelButton.GetComponentInChildren<TMP_Text>(), confirmingNewGame ? "ui.cancel" : "ui.close");
    }
    public void Confirm()
    {
        if (!confirmingNewGame || !modal.activeSelf || processing) return;
        processing = true; confirmButton.interactable = cancelButton.interactable = false;
        if (GameStartService.StartNew(selectedLanguage, out string error)) { SceneManager.LoadScene(GameStartService.HomeScene); return; }
        processing = false; confirmButton.interactable = cancelButton.interactable = true;
        statusKey = error ?? "title.start.failed"; CloseModal(); Refresh();
    }
    public void CloseModal()
    {
        if (processing) return;
        modal.SetActive(false); menu.interactable = true; menuScroll.enabled = menuScroll.content.rect.height > menuScroll.viewport.rect.height;
        if (EventSystem.current != null && previousSelection != null) EventSystem.current.SetSelectedGameObject(previousSelection);
    }
    public static void InstallHomeEntry(Transform owner)
    {
        var source = Object.FindObjectsByType<Button>().FirstOrDefault(b=>b.name=="SettingsOpen");
        if (source == null || GameObject.Find("HomeTitleButton") != null) return;
        var button = Instantiate(source, source.transform.parent); button.name = "HomeTitleButton";
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(()=>owner.GetComponent<GermanyHomeManager>().GoTitle());
        foreach (Transform child in button.transform) if (child.name == "Gear") Destroy(child.gameObject);
        var text = button.GetComponentInChildren<TMP_Text>(); text.rectTransform.offsetMin = new Vector2(8,0);
        LocalizedText.Set(text,"title.return");
        ((RectTransform)button.transform).anchoredPosition = new Vector2(-774,-16);
    }
    private void OnDestroy()
    {
        UserSettings.Changed -= Refresh;
        if (Active == this) Active = null;
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SettingsUI : MonoBehaviour
{
    [SerializeField] private Button close, generalTab, soundTab, dataTab, japaneseButton, englishButton, tutorialButton, previewButton;
    [SerializeField] private GameObject generalPage, soundPage, dataPage;
    [SerializeField] private Slider masterSlider, bgmSlider, sfxSlider, voiceSlider;
    [SerializeField] private TMP_Text masterValue, bgmValue, sfxValue, voiceValue, message;
    public static SettingsUI Active { get; private set; }
    public static bool IsOpen => Active != null;
    public static bool CanOpen => SettingsInstaller.Supports(SceneManager.GetActiveScene().name) && !DialogueUI.IsOpen && TutorialController.Active == null && ProgressResetUI.Active == null && !TitleScreenController.IsModalOpen;
    private readonly List<(CanvasGroup group, bool interactable, bool added)> blocked = new List<(CanvasGroup, bool, bool)>();
    private GameObject previousSelection;
    private AudioSource preview;
    private AudioClip sample;
    private string statusKey;
    private int tab;
    public static bool Show()
    {
        if (!CanOpen || IsOpen) return false;
        var asset = Resources.Load<GameObject>("UI/SettingsUI"); if (asset == null) return false;
        var view = Instantiate(asset).GetComponent<SettingsUI>(); Active = view; view.Open(); return true;
    }
    private void Open()
    {
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
        {
            if (canvas.rootCanvas != canvas || canvas.gameObject == gameObject) continue;
            var group = canvas.GetComponent<CanvasGroup>(); bool added = group == null;
            if (added) group = canvas.gameObject.AddComponent<CanvasGroup>();
            blocked.Add((group, group.interactable, added)); group.interactable = false;
        }
        close.onClick.AddListener(Close);
        generalTab.onClick.AddListener(() => ShowTab(0)); soundTab.onClick.AddListener(() => ShowTab(1)); dataTab.onClick.AddListener(() => ShowTab(2));
        japaneseButton.onClick.AddListener(() => SetLanguage("ja")); englishButton.onClick.AddListener(() => SetLanguage("en"));
        tutorialButton.onClick.AddListener(() => { Close(); TutorialChoiceUI.ShowSettings(); });
        masterSlider.onValueChanged.AddListener(value => ChangeVolume(0, value)); bgmSlider.onValueChanged.AddListener(value => ChangeVolume(1, value));
        sfxSlider.onValueChanged.AddListener(value => ChangeVolume(2, value)); voiceSlider.onValueChanged.AddListener(value => ChangeVolume(3, value));
        preview = gameObject.AddComponent<AudioSource>(); preview.playOnAwake = false; sample = GameAudio.CharacterClip("feuer");
        previewButton.interactable = sample != null; previewButton.onClick.AddListener(()=>PreviewVoice());
        if (sample == null) statusKey = "settings.preview.missing";
        if (UserSettings.Store.LastError != null) statusKey = UserSettings.Store.LastError;
        // Tutorial/reset preferences belong to progress; the first title visit has not loaded a profile yet.
        if (SceneManager.GetActiveScene().name == GameStartService.TitleScene && PlayerSaveService.Instance == null)
        { tutorialButton.interactable = false; statusKey = statusKey ?? "title.settings.progress.required"; }
        ProgressResetDataUI.Install(dataPage.transform);
        UserSettings.Changed += Refresh; ShowTab(0); Refresh(); LocalizationUI.RefreshAll();
    }
    private void ShowTab(int value)
    {
        tab = value; generalPage.SetActive(value == 0); soundPage.SetActive(value == 1); dataPage.SetActive(value == 2);
        generalTab.image.color = value == 0 ? new Color(.35f,.58f,.8f) : Color.white;
        soundTab.image.color = value == 1 ? new Color(.35f,.58f,.8f) : Color.white;
        dataTab.image.color = value == 2 ? new Color(.35f,.58f,.8f) : Color.white;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject((value == 0 ? japaneseButton : value == 1 ? masterSlider : (Selectable)close).gameObject);
    }
    public bool SetLanguage(string language) { var data = UserSettings.Current; data.uiLanguage = language; return Commit(data); }
    public bool ChangeVolume(int channel, float percent)
    {
        var data = UserSettings.Current; float value = Mathf.Clamp(percent, 0, 100) / 100f;
        switch (channel) { case 0: data.masterVolume = value; break; case 1: data.bgmVolume = value; break; case 2: data.sfxVolume = value; break; case 3: data.voiceVolume = value; break; default: return false; }
        return Commit(data);
    }
    private bool Commit(UserSettingsData data)
    {
        bool success = UserSettings.TryUpdate(data, out string error);
        statusKey = success ? "settings.saved" : error; Refresh(); LocalizationUI.RefreshAll(); return success;
    }
    private void Refresh()
    {
        var data = UserSettings.Current; masterSlider.SetValueWithoutNotify(data.masterVolume * 100); bgmSlider.SetValueWithoutNotify(data.bgmVolume * 100);
        sfxSlider.SetValueWithoutNotify(data.sfxVolume * 100); voiceSlider.SetValueWithoutNotify(data.voiceVolume * 100);
        LocalizedText.Set(masterValue, "settings.percent", Mathf.RoundToInt(data.masterVolume * 100)); LocalizedText.Set(bgmValue, "settings.percent", Mathf.RoundToInt(data.bgmVolume * 100));
        LocalizedText.Set(sfxValue, "settings.percent", Mathf.RoundToInt(data.sfxVolume * 100)); LocalizedText.Set(voiceValue, "settings.percent", Mathf.RoundToInt(data.voiceVolume * 100));
        japaneseButton.image.color = data.uiLanguage == "ja" ? new Color(.35f,.58f,.8f) : Color.white;
        englishButton.image.color = data.uiLanguage == "en" ? new Color(.35f,.58f,.8f) : Color.white;
        bool writable = !UserSettings.Store.WritesBlocked;
        japaneseButton.interactable = englishButton.interactable = masterSlider.interactable = bgmSlider.interactable = sfxSlider.interactable = voiceSlider.interactable = writable;
        if (statusKey != null) LocalizedText.Set(message, statusKey); else message.text = "";
    }
    public bool PreviewVoice()
    {
        if (sample == null || preview == null) { statusKey = "settings.preview.missing"; Refresh(); return false; }
        StopPreview(); GameAudio.PlayOneShot(preview, sample, GameAudioChannel.Voice); return true;
    }
    private void StopPreview(){if(preview!=null)foreach(var audio in preview.GetComponentsInChildren<AudioSource>())audio.Stop();}
    public void Close() { StopPreview(); Restore(); gameObject.SetActive(false); Destroy(gameObject); }
    private void Restore()
    {
        UserSettings.Changed -= Refresh;
        foreach (var item in blocked) if (item.group != null) item.group.interactable = item.interactable;
        blocked.Clear();
        if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy) EventSystem.current.SetSelectedGameObject(previousSelection);
        if (Active == this) Active = null;
    }
    private void OnDestroy() => Restore();
}

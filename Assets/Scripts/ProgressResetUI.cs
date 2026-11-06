using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ProgressResetUI : MonoBehaviour
{
    [SerializeField] private TMP_Text title, description, acknowledgement, message;
    [SerializeField] private Toggle acknowledge;
    [SerializeField] private Button execute, cancel;
    public static ProgressResetUI Active { get; private set; }
    private readonly List<(CanvasGroup group, bool interactable)> blocked = new List<(CanvasGroup, bool)>();
    private GameObject previousSelection;
    private ProgressResetRequest request;
    private bool processing, succeeded;
    private static ProgressResetUI Create()
    {
        if (Active != null) return null;
        var prefab = Resources.Load<GameObject>("UI/ProgressResetUI"); if (prefab == null) return null;
        var view = Instantiate(prefab).GetComponent<ProgressResetUI>(); Active = view;
        view.previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        view.BlockBackground();
        view.cancel.onClick.AddListener(view.Close); view.execute.onClick.AddListener(view.Execute);
        view.acknowledge.isOn = false; view.acknowledge.onValueChanged.AddListener(_ => view.RefreshButton());
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(view.cancel.gameObject);
        return view;
    }
    // A success modal is created in sceneLoaded; cover canvases installed by later sceneLoaded listeners too.
    private void Start() => BlockBackground();
    private void BlockBackground()
    {
        foreach (var canvas in Object.FindObjectsByType<Canvas>())
        {
            if (canvas.rootCanvas != canvas || canvas.gameObject == gameObject) continue;
            var group = canvas.GetComponent<CanvasGroup>();
            if (group == null) group = canvas.gameObject.AddComponent<CanvasGroup>();
            if (blocked.Exists(item => item.group == group)) continue;
            blocked.Add((group, group.interactable)); group.interactable = false;
        }
    }
    public static bool Confirm(ProgressResetKind kind)
    {
        var command = ProgressResetService.Request(kind, out _); if (command == null) return false;
        var ui = Create(); if (ui == null) return false; ui.request = command;
        string prefix = kind == ProgressResetKind.Tutorials ? "reset.confirm.tutorials" : kind == ProgressResetKind.Stages ? "reset.confirm.stages" : "reset.confirm.new.game";
        LocalizedText.Set(ui.title, prefix + ".title"); LocalizedText.Set(ui.description, prefix + ".description");
        LocalizedText.Set(ui.acknowledgement, "reset.acknowledge"); LocalizedText.Set(ui.cancel.GetComponentInChildren<TMP_Text>(), "ui.cancel");
        LocalizedText.Set(ui.execute.GetComponentInChildren<TMP_Text>(), kind == ProgressResetKind.NewGame || kind == ProgressResetKind.DevelopmentNewGame ? "reset.new.game" : "reset.execute");
        ui.RefreshButton(); return true;
    }
    internal static void ShowSuccess(ProgressResetKind kind)
    {
        var ui = Create(); if (ui == null) return; ui.succeeded = true;
        LocalizedText.Set(ui.title, "reset.success.title"); LocalizedText.Set(ui.description, "reset.success.description");
        ui.acknowledge.gameObject.SetActive(false); ui.cancel.gameObject.SetActive(false);
        LocalizedText.Set(ui.execute.GetComponentInChildren<TMP_Text>(), "ui.continue"); ui.execute.interactable = true;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(ui.execute.gameObject);
    }
    private void RefreshButton() => execute.interactable = !processing && (succeeded || request != null && acknowledge.isOn && ProgressResetService.CanReset(request.Kind));
    public void Execute()
    {
        if (succeeded) { Close(); return; }
        if (processing || request == null || !acknowledge.isOn || !ProgressResetService.CanReset(request.Kind)) return;
        processing = true; execute.interactable = cancel.interactable = acknowledge.interactable = false;
        LocalizedText.Set(message, "reset.running"); Canvas.ForceUpdateCanvases();
        if (!ProgressResetService.TryExecute(request, out string error))
        {
            LocalizedText.Set(message, error); processing = false; cancel.interactable = acknowledge.interactable = true; RefreshButton(); return;
        }
        ProgressResetService.ShowCompletionAtHome(request.Kind);
        var loader = gameObject.AddComponent<SceneLoader>(); processing = false; Close(); SettingsUI.Active?.Close(); loader.LoadHome();
    }
    public void Close()
    {
        if (processing) return;
        Restore(); gameObject.SetActive(false); Destroy(gameObject);
    }
    private void Restore()
    {
        foreach (var item in blocked) if (item.group != null) item.group.interactable = item.interactable;
        blocked.Clear();
        if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy) EventSystem.current.SetSelectedGameObject(previousSelection);
        if (Active == this) Active = null;
    }
    private void OnDestroy() => Restore();
}

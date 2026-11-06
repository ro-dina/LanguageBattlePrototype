using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SettingsInstaller
{
    private static readonly HashSet<string> scenes = new HashSet<string> {
        "GermanyHome", "StageSelect", "CharacterSelect", "CharacterCollection", "CharacterDetail", "Inventory", "Gacha", "Crafting", "Result"
    };
    public static bool Supports(string scene) => scenes.Contains(scene);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
    private static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (!Supports(scene.name)) return;
        foreach (var root in scene.GetRootGameObjects()) if (root.name == "SettingsLauncher") return;
        var asset = Resources.Load<GameObject>("UI/SettingsLauncher"); if (asset == null) return;
        var launcher = Object.Instantiate(asset); launcher.name = "SettingsLauncher"; SceneManager.MoveGameObjectToScene(launcher, scene);
        launcher.GetComponentInChildren<Button>().onClick.AddListener(() => SettingsUI.Show());
        if (scene.name == "GermanyHome")
        { var completed = ProgressResetService.TakeCompletion(); if (completed.HasValue) ProgressResetUI.ShowSuccess(completed.Value); }
    }
}

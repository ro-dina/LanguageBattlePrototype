using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// One shared prefab; scene files keep their existing Canvas hierarchy and serialized references.
public static class CurrencyHUDInstaller
{
    private static readonly HashSet<string> Scenes = new HashSet<string> {
        "GermanyHome","Gacha","Crafting","Inventory","CharacterSelect","CharacterDetail","CharacterCollection","StageSelect"
    };
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    { SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded; }
    private static void OnSceneLoaded(Scene scene,LoadSceneMode mode)
    {
        if (!Scenes.Contains(scene.name)) return;
        foreach (var root in scene.GetRootGameObjects()) if (root.GetComponentInChildren<CurrencyHUD>(true) != null) return;
        var prefab = Resources.Load<GameObject>("UI/CurrencyHUD");
        if (prefab == null) { Debug.LogError("CurrencyHUD prefab is missing."); return; }
        var hud = Object.Instantiate(prefab); hud.name = "CurrencyHUD";
        SceneManager.MoveGameObjectToScene(hud,scene);
    }
}

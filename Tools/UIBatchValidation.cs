// Copy this file to Assets/Editor in a disposable validation project.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class UIBatchValidation
{
    static UIBatchValidation()
    {
        EditorApplication.update += () => { if (EditorApplication.isPlaying) { EditorApplication.isPaused = false; EditorApplication.QueuePlayerLoopUpdate(); } };
        EditorApplication.playModeStateChanged += state =>
        {
            Debug.Log("UI_VALIDATE_STATE " + state);
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("UIValidation", false))
            {
                SessionState.SetBool("UIValidation", false);
                Debug.Log("UI_VALIDATE_CREATE_RUNNER");
                new GameObject("UI validation runner").AddComponent<UIPlayValidation>();
            }
        };
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GermanyHome.unity");
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        SessionState.SetBool("UIValidation", true);
        EditorApplication.EnterPlaymode();
    }
}

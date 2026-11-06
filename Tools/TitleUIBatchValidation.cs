using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad] public static class TitleUIBatchValidation
{
    static TitleUIBatchValidation()
    {
        EditorApplication.update+=()=>{if(EditorApplication.isPlaying){EditorApplication.isPaused=false;EditorApplication.QueuePlayerLoopUpdate();}};
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("TitleUIValidation",false)){SessionState.SetBool("TitleUIValidation",false);new GameObject("Title validation runner").AddComponent<TitleUIValidation>();}};
    }
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");Application.runInBackground=true;SessionState.SetBool("TitleUIValidation",true);EditorApplication.EnterPlaymode();}
}

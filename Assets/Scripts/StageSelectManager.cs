using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class StageSelectManager:MonoBehaviour
{
    [SerializeField] private Transform content;
    [SerializeField] private GameObject rowPrefab;
    [SerializeField] private TMP_Text tutorialStatus;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button homeButton;
    private void Start(){StageSession.Clear();settingsButton.onClick.AddListener(()=>SettingsUI.Show());LocalizedText.Set(settingsButton.GetComponentInChildren<TMP_Text>(),"ui.settings");homeButton.onClick.AddListener(()=>SceneManager.LoadScene("GermanyHome"));Refresh();}
    private void OnEnable()=>PlayerStateChanges.Changed+=Refresh;
    private void OnDisable()=>PlayerStateChanges.Changed-=Refresh;
    public void Refresh()
    {
        if(content==null)return;
        foreach(Transform child in content){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        foreach(var stage in StageCatalog.All.OrderBy(s=>s.stageNumber==0?int.MaxValue:s.stageNumber))
        {var row=Instantiate(rowPrefab,content);row.name="StageRow_"+stage.stageId;row.SetActive(true);row.GetComponent<StageRowUI>().Bind(stage);}
        LocalizedText.Set(tutorialStatus,TutorialPreferences.SkipAll?"ui.tut.prompt.skipped":"ui.tut.prompt.enabled");
    }
}

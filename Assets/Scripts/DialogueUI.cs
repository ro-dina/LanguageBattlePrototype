using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[Serializable] public sealed class DialogueLine
{
    public string dialogueId;
    public string speakerName;
    public string speakerSide;
    public string leftPortraitId;
    public string rightPortraitId;
    public string text;
    public string nextDialogueId;
    public string eventOnComplete;
}
[Serializable] public sealed class DialogueDatabase { public DialogueLine[] dialogues; }
public static class DialogueCatalog
{
    private static DialogueLine[] lines;
    public static DialogueLine Get(string id)
    {
        if(lines==null)lines=JsonUtility.FromJson<DialogueDatabase>(Resources.Load<TextAsset>("Data/dialogues").text).dialogues;
        var value=lines.SingleOrDefault(l=>l.dialogueId==id);return value==null?null:JsonUtility.FromJson<DialogueLine>(JsonUtility.ToJson(value));
    }
    public static bool ValidateChain(string id,out string error)
    {
        error=null;var seen=new HashSet<string>();
        while(!string.IsNullOrEmpty(id))
        {
            var line=Get(id);
            if(line==null || !seen.Add(id) || seen.Count>256 || string.IsNullOrWhiteSpace(line.text) || line.text.Length>16000 ||
                (line.speakerSide!="Left" && line.speakerSide!="Right")){error="Invalid dialogue chain.";return false;}
            id=line.nextDialogueId;
        }
        if(seen.Count==0){error="Empty dialogue.";return false;}return true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset()=>lines=null;
}
public sealed class DialogueUI:MonoBehaviour
{
    [SerializeField] private Image leftPortrait;
    [SerializeField] private Image rightPortrait;
    [SerializeField] private TMP_Text leftPlaceholder;
    [SerializeField] private TMP_Text rightPlaceholder;
    [SerializeField] private TMP_Text speaker;
    [SerializeField] private TMP_Text body;
    [SerializeField] private TMP_Text pageLabel;
    [SerializeField] private Button next;
    [SerializeField] private Button textBox;
    public static DialogueUI Active { get; private set; }
    public static bool IsOpen=>Active!=null;
    public DialogueLine Current { get; private set; }
    public string Text=>body.text;
    public string Speaker=>speaker.text;
    private Action<string> completed;
    private Action cancelled;
    private float nextAdvanceTime;
    private int page;
    private bool closed;
    public static bool Play(string id,Action<string> onComplete,Action onCancel,out string error)
    {
        error=null;
        if(IsOpen){error="A dialogue is already playing.";return false;}
        if(!DialogueCatalog.ValidateChain(id,out error))return false;
        var asset=Resources.Load<GameObject>("UI/DialogueUI");
        if(asset==null){error="Dialogue UI is unavailable.";return false;}
        var ui=Instantiate(asset).GetComponent<DialogueUI>();Active=ui;ui.completed=onComplete;ui.cancelled=onCancel;
        ui.next.onClick.AddListener(ui.Advance);ui.textBox.onClick.AddListener(ui.Advance);ui.Show(DialogueCatalog.Get(id));return true;
    }
    public static bool PlayLine(DialogueLine line,Action<string> onComplete,Action onCancel,out string error)
    {
        error=null;
        if(IsOpen || line==null || string.IsNullOrWhiteSpace(line.text) || line.text.Length>16000 || !string.IsNullOrEmpty(line.nextDialogueId) ||
            (line.speakerSide!="Left" && line.speakerSide!="Right")){error="Invalid single dialogue line.";return false;}
        var asset=Resources.Load<GameObject>("UI/DialogueUI");if(asset==null){error="Dialogue UI is unavailable.";return false;}
        var ui=Instantiate(asset).GetComponent<DialogueUI>();Active=ui;ui.completed=onComplete;ui.cancelled=onCancel;
        ui.next.onClick.AddListener(ui.Advance);ui.textBox.onClick.AddListener(ui.Advance);ui.Show(line);return true;
    }
    private void Show(DialogueLine line)
    {
        Current=line;page=1;speaker.text=line.speakerName;
        var database=GameDatabase.EnsureAvailable();
        string skill=database.ContainsSkill(StageSession.TutorialSkillId)?database.GetSkill(StageSession.TutorialSkillId).skillName:"a verb";
        body.text=line.text.Replace("{skill}",skill).Replace("{enemy}",StageSession.Stage?.displayName ?? "the opponent");
        body.pageToDisplay=1;body.ForceMeshUpdate();SetPortrait(leftPortrait,leftPlaceholder,line.leftPortraitId,line.speakerSide=="Left");SetPortrait(rightPortrait,rightPlaceholder,line.rightPortraitId,line.speakerSide=="Right");
        nextAdvanceTime=Time.unscaledTime+.2f;RefreshPage();
        if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(next.gameObject);
    }
    private static void SetPortrait(Image image,TMP_Text placeholder,string id,bool speaking)
    {
        string species=id=="stage_enemy"?StageSession.Stage?.enemyCharacterId:id;
        var database=GameDatabase.EnsureAvailable();var data=database.ContainsCharacter(species)?database.GetCharacter(species):null;
        var sprite=string.IsNullOrEmpty(species)?null:Resources.Load<Sprite>("Images/Characters/"+(data?.illustration ?? species));
        image.sprite=sprite;image.preserveAspect=true;
        image.color=sprite!=null?new Color(1,1,1,speaking?1:.65f):new Color(.12f,.19f,.28f,speaking?.95f:.7f);
        placeholder.text=sprite!=null?"":data?.characterName ?? (id=="guide"?"Guide":"Portrait unavailable");placeholder.gameObject.SetActive(sprite==null);
    }
    private void RefreshPage(){body.ForceMeshUpdate();pageLabel.text=page+" / "+Mathf.Max(1,body.textInfo.pageCount);}
    public void Advance()
    {
        if(closed || Time.unscaledTime<nextAdvanceTime)return;
        nextAdvanceTime=Time.unscaledTime+.2f;body.ForceMeshUpdate();
        if(page<body.textInfo.pageCount){body.pageToDisplay=++page;RefreshPage();return;}
        if(!string.IsNullOrEmpty(Current.nextDialogueId)){Show(DialogueCatalog.Get(Current.nextDialogueId));return;}
        string eventId=Current.eventOnComplete;var callback=completed;Close(false);callback?.Invoke(eventId);
    }
    public void Cancel()=>Close(true);
    private void Close(bool notify)
    {
        if(closed)return;closed=true;if(Active==this)Active=null;gameObject.SetActive(false);Destroy(gameObject);
        if(notify)cancelled?.Invoke();
    }
    private void OnDestroy(){if(Active==this)Active=null;if(!closed){closed=true;cancelled?.Invoke();}}
}

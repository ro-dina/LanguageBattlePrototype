using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Scene-authored controls; no persistent event is replaced or removed.
public sealed class BattleModifierUI : MonoBehaviour
{
    [SerializeField] private Button futureButton;
    [SerializeField] private Button noTenseButton;
    [SerializeField] private Button muessenButton;
    [SerializeField] private Button noModalButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private TMP_Text selectionText;
    [SerializeField] private TMP_Text statusText;
    private BattleManager battle;
    public void Bind(BattleManager manager)
    {
        if (battle != null) return;
        battle = manager;
        futureButton.onClick.AddListener(() => battle.SelectFuture(battle.Selection.Tense != BattleTense.Future));
        noTenseButton.onClick.AddListener(() => battle.SelectFuture(false));
        muessenButton.onClick.AddListener(() => battle.SelectMuessen(battle.Selection.Modal != BattleModal.Muessen));
        noModalButton.onClick.AddListener(() => battle.SelectMuessen(false));
        clearButton.onClick.AddListener(battle.ClearModifiers);
        Refresh();
    }
    public void Refresh()
    {
        if (battle == null) return;
        var selection = battle.Selection;
        var context = battle.Context;
        bool choose = battle.CanChoose && battle.AllowsInput(TutorialInput.Modifier);
        bool german = context?.Player.Character.language == "de";
        futureButton.interactable = choose && german && context.PendingAttack == null;
        muessenButton.interactable = choose && german && !context.MuessenUsed;
        noTenseButton.interactable = noModalButton.interactable = clearButton.interactable = choose;
        Mark(futureButton, selection.Tense == BattleTense.Future);
        Mark(noTenseButton, selection.Tense == BattleTense.Present);
        Mark(muessenButton, selection.Modal == BattleModal.Muessen);
        Mark(noModalButton, selection.Modal == BattleModal.None);
        selectionText.text = "Optional modifiers • choose a skill to attack\n" +
            "Adverb: " + (selection.Get(BattleElementKind.Adverb) ?? "none") +
            "  |  Tense: " + (selection.Tense == BattleTense.Future ? "Futur I" : "default") +
            "  |  Modal: " + (selection.Modal == BattleModal.Muessen ? "müssen" : "none");
        futureButton.GetComponentInChildren<TMP_Text>().text = selection.Tense == BattleTense.Future
            ? "Futur I • ON • ×1.5" : "Futur I • OFF • ×1.5";
        string pending = context?.PendingAttack == null ? "Future: no attack reserved" :
            $"Future: {context.PendingAttack.Action.Skill.skillName} • {context.PendingAttack.Damage} damage next turn";
        statusText.text = pending + "\n" + (context?.MuessenUsed == true ? "müssen: USED (this battle)" : "müssen: available once per battle");
        muessenButton.GetComponentInChildren<TMP_Text>().text = context?.MuessenUsed == true ? "müssen • USED" : selection.Modal == BattleModal.Muessen ? "müssen • ON • ×2" : "müssen • OFF • ×2";
    }
    private static void Mark(Button button, bool selected) => button.image.color = selected ? new Color(0.65f, 0.84f, 1f) : Color.white;
}

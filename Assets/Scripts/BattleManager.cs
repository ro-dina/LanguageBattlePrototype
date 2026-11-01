using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

public static class GameManager
{
    public static bool IsWin;
    private static string selectedInstanceId = "";
    private static string previewCharacterId = "nacht";
    public static string EnemyCharacterId = "feuer";
    public static string SelectedCharacterInstanceId
    {
        get => selectedInstanceId;
        set { selectedInstanceId = value ?? ""; var instance = CharacterInstances.Get(value); if (instance != null) previewCharacterId = instance.characterId; }
    }
    // Species-only compatibility/collection preview. New player selection uses SelectedCharacterInstanceId.
    public static string SelectedCharacterId
    {
        get => CharacterInstances.Get(selectedInstanceId)?.characterId ?? previewCharacterId;
        set => PreviewCharacterType(value);
    }
    public static string PlayerCharacterId { get => SelectedCharacterId; set => PreviewCharacterType(value); }
    public static void PreviewCharacterType(string characterId)
    {
        if (CharacterInstances.Get(selectedInstanceId)?.characterId != characterId)
            selectedInstanceId = CharacterInstances.GetByCharacterId(characterId).FirstOrDefault()?.instanceId ?? "";
        previewCharacterId = characterId;
    }
    public static void EnsureOwnedSelection()
    {
        if (!CharacterInstances.Owns(selectedInstanceId)) SelectedCharacterInstanceId = CharacterInstances.GetAll().FirstOrDefault()?.instanceId ?? "";
    }
    public static void OnInstanceRemoved(string instanceId)
    { if (selectedInstanceId == instanceId || !CharacterInstances.Owns(selectedInstanceId)) EnsureOwnedSelection(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSelection() { selectedInstanceId = ""; previewCharacterId = "nacht"; PreviousSceneName = ""; }
    public static string SelectedCharacterEquipmentId
    {
        get => CharacterLoadoutStore.GetLoadout(SelectedCharacterInstanceId)?.adjectiveId ?? "";
        set { var draft = CharacterLoadoutStore.GetLoadout(SelectedCharacterInstanceId); if (draft == null) return; draft.adjectiveId = value; CharacterLoadoutStore.Commit(draft); }
    }
    public static string[] SelectedSkillEquipmentIds => System.Array.ConvertAll(
        CharacterLoadoutStore.GetLoadout(SelectedCharacterInstanceId)?.skillSlots ?? System.Array.Empty<CharacterSkillSlotData>(), slot => slot.equippedAdverbId);
    public static string PreviousSceneName = "";

    public static void SetBattleCharacters(string playerCharacterId, string enemyCharacterId)
    {
        PlayerCharacterId = playerCharacterId;
        EnemyCharacterId = enemyCharacterId;
    }

    public static void SetCharacterEquipment(string equipmentId)
    {
        SelectedCharacterEquipmentId = equipmentId;
    }

    public static void SetSkillEquipment(int skillIndex, string equipmentId)
    {
        var draft = CharacterLoadoutStore.GetLoadout(SelectedCharacterInstanceId);
        if (draft == null || skillIndex < 0 || skillIndex >= draft.skillSlots.Length) return;
        draft.skillSlots[skillIndex].equippedAdverbId = equipmentId;
        CharacterLoadoutStore.Commit(draft);
    }

}

public class BattleManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject skillPanel;
    [SerializeField] private TMP_Text playerHPText;
    [SerializeField] private TMP_Text enemyHPText;
    [SerializeField] private TMP_Text battleLogText;
    [SerializeField] private GameObject[] skillButtons;
    [SerializeField] private TMP_Text[] skillButtonTexts;


    [SerializeField] private TMP_Text enemyDamageText;
    [SerializeField] private TMP_Text playerDamageText;

    [Header("Characters")]
    [SerializeField] private string playerCharacterId = "hund";
    [SerializeField] private string enemyCharacterId = "nacht";

    [SerializeField] private SpriteRenderer playerRenderer;
    [SerializeField] private SpriteRenderer enemyRenderer;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [SerializeField] private Button[] adverbCardButtons;
    [SerializeField] private TMP_Text[] adverbCardTexts;
    [SerializeField] private Button noAdverbButton;

    private EquipmentData selectedTurnAdverb;
    private EquipmentData enemySelectedTurnAdverb;
    private List<EquipmentData> currentAdverbCards = new List<EquipmentData>();

    private CharacterData playerCharacter;
    private CharacterData enemyCharacter;
    private EquipmentData playerCharacterEquipment;

    private EquipmentData[] playerSkillEquipments;
    private EquipmentData[] enemySkillEquipments;

    private BattleContext battleContext;
    public BattleContext Context => battleContext;
    private int playerHP => battleContext?.Player.HP ?? 0;
    private int enemyHP => battleContext?.Enemy.HP ?? 0;
    private bool isProcessing = false;
    [SerializeField] private BattleModifierUI modifierUI;
    public BattleSelection Selection { get; private set; } = new BattleSelection();
    public BattleAction LastCommittedAction { get; private set; }
    public TutorialController Tutorial { get; internal set; }
    public bool AllowsInput(TutorialInput input,string id=null) => !DialogueUI.IsOpen && (Tutorial==null || Tutorial.Allows(input,id));
    public bool CanChoose => !isProcessing && battleContext != null &&
        battleContext.TurnState == BattleTurnState.AwaitingPlayer && battleContext.Outcome == BattleOutcome.Ongoing;

    public bool SelectFuture(bool selected)
    {
        if (!AllowsInput(TutorialInput.Modifier) || !CanChoose || (selected && (battleContext.PendingAttack != null || playerCharacter.language != "de"))) return false;
        Selection.Select(BattleElementKind.Tense, selected ? "futur1" : null);
        RefreshSelectionUI(); return true;
    }
    public bool SelectMuessen(bool selected)
    {
        if (!AllowsInput(TutorialInput.Modifier) || !CanChoose || (selected && (battleContext.MuessenUsed || playerCharacter.language != "de"))) return false;
        Selection.Select(BattleElementKind.Modal, selected ? "muessen" : null);
        RefreshSelectionUI(); return true;
    }
    public void ClearModifiers()
    {
        if (!AllowsInput(TutorialInput.Modifier) || !CanChoose) return;
        selectedTurnAdverb = null; Selection.ClearModifiers(); RefreshSelectionUI();
    }
    private void RefreshSelectionUI()
    {
        if (modifierUI != null) modifierUI.Refresh();
        if (adverbCardButtons != null)
            for (int i = 0; i < adverbCardButtons.Length; i++)
                if (adverbCardButtons[i] != null && i < currentAdverbCards.Count)
                    adverbCardButtons[i].image.color = selectedTurnAdverb != null && currentAdverbCards[i].id == selectedTurnAdverb.id
                        ? new Color(0.65f, 0.84f, 1f) : Color.white;
        if (noAdverbButton != null) noAdverbButton.image.color = selectedTurnAdverb == null ? new Color(0.65f, 0.84f, 1f) : Color.white;
    }
    private string battleInstanceId;
    private bool ownsBattleLock;
    private string rewardBattleId;
    private bool resultTransitioned;
    private BattleRewardRetryUI rewardRetryUI;
    public string RewardBattleId => rewardBattleId;
    public string PlayerInstanceId => battleInstanceId;
    private void OnDestroy()
    {
        battleContext?.Cancel();
        if (ownsBattleLock) CharacterInstances.EndBattle(battleInstanceId);
        HideRewardSaveFailure();
    }


    private void Start()
    {
        battleInstanceId = GameManager.SelectedCharacterInstanceId;
        playerCharacterId = CharacterInstances.Get(battleInstanceId)?.characterId;
        enemyCharacterId = GameManager.EnemyCharacterId;

        if (GameDatabase.Instance == null)
        {
            Debug.LogError("Battle cannot start because GameDatabase is not available.");
            enabled = false;
            return;
        }

        if (!CharacterInstances.Owns(battleInstanceId))
        {
            Debug.LogWarning("Battle rejected an unowned player character.", this);
            if (battleLogText != null) battleLogText.text = "Character is not owned.";
            isProcessing = true;
            if (skillPanel != null) skillPanel.SetActive(false);
            SetAdverbCardInteraction(false);
            enabled = false;
            return;
        }
        playerCharacter = CharacterLoadoutStore.GetCharacterForBattle(battleInstanceId);
        enemyCharacter = GameDatabase.Instance.GetCharacter(enemyCharacterId);

        if (playerCharacter == null || enemyCharacter == null)
        {
            Debug.LogError("Battle cannot start because character data is missing.");
            enabled = false;
            return;
        }


        if (!StageSession.ValidateBattle(out string stageError))
        { isProcessing=true;enabled=false;if(battleLogText!=null)battleLogText.text=stageError;TutorialChoiceUI.ShowStageError(stageError);return; }
        var selectedStage=StageSession.Stage;
        if(StageSession.Guided && selectedStage?.battleModifiers.guidedEnemyHp>0)
        { enemyCharacter=JsonUtility.FromJson<CharacterData>(JsonUtility.ToJson(enemyCharacter));enemyCharacter.hp=selectedStage.battleModifiers.guidedEnemyHp; }
        ownsBattleLock = CharacterInstances.TryBeginBattle(battleInstanceId);
        if (!ownsBattleLock) { isProcessing = true; enabled = false; return; }
        SetupCharacterEquipment();

        battleContext = new BattleContext(playerCharacter, enemyCharacter, playerCharacterEquipment);
        if (modifierUI != null) modifierUI.Bind(this);

        if (enemyDamageText != null)
        {
            enemyDamageText.text = "";
        }

        if (playerDamageText != null)
        {
            playerDamageText.text = "";
        }

        SetupSkillButtons();
        LoadCharacterImages();
        if (skillPanel != null)
        {
            skillPanel.SetActive(false);
        }
        GenerateAdverbCards();
        UpdateHPText();

        if (battleLogText != null)
        {
            battleLogText.text = "Choose your skill!";
        }
        BeginRewardTicket();
    }

    private void BeginRewardTicket()
    {
        if (!BattleRewardService.Begin(enemyCharacterId,out rewardBattleId,out string error,StageSession.StageId,StageSession.Guided))
        {
            isProcessing = true; SetAdverbCardInteraction(false);
            ShowRewardSaveFailure(error,BeginRewardTicket); return;
        }
        HideRewardSaveFailure(); isProcessing = false; SetAdverbCardInteraction(true); RefreshSelectionUI();
        if(Tutorial==null)TutorialController.Attach(this,StageSession.Stage);
    }
    private void ShowRewardSaveFailure(string error,System.Action retry)
    {
        if (rewardRetryUI == null) rewardRetryUI = Instantiate(Resources.Load<GameObject>("UI/BattleRewardRetryUI")).GetComponent<BattleRewardRetryUI>();
        rewardRetryUI.Bind(error,retry); rewardRetryUI.gameObject.SetActive(true);
    }
    private void HideRewardSaveFailure() { if (rewardRetryUI != null) { Destroy(rewardRetryUI.gameObject); rewardRetryUI = null; } }

    internal void ShowTutorialSkills(){if(skillPanel!=null)skillPanel.SetActive(true);}
    public Button SkillButton(string id)
    {int index=System.Array.IndexOf(playerCharacter?.skillIds ?? System.Array.Empty<string>(),id);return index>=0 && index<skillButtons.Length?skillButtons[index].GetComponent<Button>():null;}
    internal void RefreshTutorialInput(){SetAdverbCardInteraction(CanChoose && AllowsInput(TutorialInput.Adverb));RefreshSelectionUI();}
    internal void ShowTutorialNotice(string text){if(battleLogText!=null)battleLogText.text=text;}

    private void SetupCharacterEquipment()
    {
        playerCharacterEquipment = GameDatabase.Instance.GetEquipment(playerCharacter.equippedAdjectiveId);
        playerSkillEquipments = LoadCharacterSkillEquipments(playerCharacter);
        enemySkillEquipments = LoadCharacterSkillEquipments(enemyCharacter);
    }

    private EquipmentData[] LoadCharacterSkillEquipments(CharacterData character)
    {
        int skillCount = character != null && character.skillIds != null
            ? character.skillIds.Length
            : 0;
        EquipmentData[] result = new EquipmentData[skillCount];

        if (character == null || character.skillSlots == null)
        {
            return result;
        }

        for (int i = 0; i < skillCount; i++)
        {
            string skillId = character.skillIds[i];

            for (int j = 0; j < character.skillSlots.Length; j++)
            {
                CharacterSkillSlotData slot = character.skillSlots[j];
                if (slot == null || slot.skillId != skillId)
                {
                    continue;
                }

                result[i] = GameDatabase.Instance.GetEquipment(slot.equippedAdverbId);
                break;
            }
        }

        return result;
    }

    private void SetupSkillButtons()
    {
        if (skillButtons == null || skillButtons.Length == 0)
        {
            Debug.LogError("Skill Buttons are not assigned in BattleManager Inspector.");
            return;
        }

        if (skillButtonTexts == null || skillButtonTexts.Length == 0)
        {
            Debug.LogError("Skill Button Texts are not assigned in BattleManager Inspector.");
            return;
        }

        for (int i = 0; i < skillButtons.Length; i++)
        {
            if (skillButtons[i] == null)
            {
                Debug.LogError($"Skill button at index {i} is not assigned.");
                continue;
            }

            bool hasSkill = playerCharacter.skillIds != null && i < playerCharacter.skillIds.Length;
            skillButtons[i].SetActive(hasSkill);

            if (!hasSkill)
            {
                continue;
            }

            if (i >= skillButtonTexts.Length || skillButtonTexts[i] == null)
            {
                Debug.LogError($"Skill button text at index {i} is not assigned.");
                continue;
            }

            SkillData skill = GameDatabase.Instance.GetSkill(playerCharacter.skillIds[i]);

            if (skill != null)
            {
                skillButtonTexts[i].text = skill.skillName; LocalizedText.Learning(skillButtonTexts[i]);
            }
        }
    }

    public void OpenSkillPanel()
    {
        if (!AllowsInput(TutorialInput.OpenSkills) || isProcessing) return;
        if (skillPanel != null)
        {
            skillPanel.SetActive(true);
        }
    }

    public void CloseSkillPanel()
    {
        if (!AllowsInput(TutorialInput.CloseSkills) || isProcessing) return;
        if (skillPanel != null)
        {
            skillPanel.SetActive(false);
        }
    }

    public void UseSkill1()
    {
        UsePlayerSkill(0);
    }

    public void UseSkill2()
    {
        UsePlayerSkill(1);
    }

    public void UseSkill3()
    {
        UsePlayerSkill(2);
    }

    public void UseSkill4()
    {
        UsePlayerSkill(3);
    }

    private void UseSkill(SkillData skill, EquipmentData skillEquipment,
        EquipmentData enemySkillEquipment, string adverbContestLog)
    {
        if (!AllowsInput(TutorialInput.Skill,skill?.id) || isProcessing) return;
        BattleActionResult result = battleContext.Resolve(new BattleAction(BattleSide.Player, skill, skillEquipment,
            Selection.Tense, Selection.Modal, Selection.Commit(skill.id)));
        if (!result.CanUse)
        {
            if (battleLogText != null) battleLogText.text = Localization.Error(result.FailureReason);
            return;
        }
        LastCommittedAction = result.Action;
        Tutorial?.SkillAccepted(skill.id);
        isProcessing = true;
        SetAdverbCardInteraction(false);
        RefreshSelectionUI();
        if (skillPanel != null) skillPanel.SetActive(false);
        StartCoroutine(PlayerTurnSequence(result, enemySkillEquipment, adverbContestLog));
    }

    private IEnumerator PlayerTurnSequence(BattleActionResult result,
        EquipmentData enemySkillEquipment, string adverbContestLog)
    {
        if (!string.IsNullOrEmpty(adverbContestLog) && battleLogText != null)
        {
            battleLogText.text = adverbContestLog;
            yield return new WaitForSeconds(0.8f);
        }
        yield return PresentPlayerAttack(result);
        if (!battleContext.Apply(result))
        {
            isProcessing = false;
            SetAdverbCardInteraction(CanChoose);
            RefreshSelectionUI();
            yield break;
        }
        RefreshSelectionUI();
        if (!result.IsReservation) yield return PresentPlayerHit(result.Damage);
        if(Tutorial!=null)yield return Tutorial.AttackResolved();
        if (CompleteBattle()) yield break;
        EnemyAttack(enemySkillEquipment);
        yield return new WaitForSeconds(1f);
        if (CompleteBattle()) yield break;

        // Enemy turn has ended; resolve the frozen reservation before re-enabling any input.
        if (battleContext.TurnState == BattleTurnState.ResolvingPending)
        {
            var pending = battleContext.ResolvePendingAttack();
            yield return PresentPlayerAttack(pending);
            if (battleContext.Apply(pending)) yield return PresentPlayerHit(pending.Damage);
            RefreshSelectionUI();
            if (CompleteBattle()) yield break;
        }
        isProcessing = false;
        GenerateAdverbCards();
    }

    private bool CompleteBattle()
    {
        if (battleContext.Outcome == BattleOutcome.Ongoing) return false;
        if (resultTransitioned) return true;
        isProcessing = true; SetAdverbCardInteraction(false);
        bool win = battleContext.Outcome == BattleOutcome.PlayerWin;
        if(win && Tutorial!=null)Tutorial.PrepareVictory();
        if (!BattleRewardService.Complete(rewardBattleId,win,out string error,tutorialCompleted:Tutorial?.ReadyForCommit==true))
        { ShowRewardSaveFailure(error,() => CompleteBattle()); return true; }
        HideRewardSaveFailure(); resultTransitioned = true;
        GameManager.IsWin = win;
        SceneManager.LoadScene("Result");
        return true;
    }

    private IEnumerator PresentPlayerHit(int damage)
    {
        PlaySoundEffect("slash");
        UpdateHPText();
        StartCoroutine(ShowEnemyDamage(damage));
        StartCoroutine(ShakeRenderer(enemyRenderer));
        yield return new WaitForSeconds(0.4f);
    }

    private IEnumerator PresentPlayerAttack(BattleActionResult result)
    {
        var sentence = result.Sentence;
        if (battleLogText != null)
        {
            string status = result.IsReservation ? $"Reserved: {result.ReservedDamage} damage next turn (0 now)" :
                result.IsPendingAttack ? $"Reserved attack: {result.Damage} damage!" : $"{result.Damage} damage!";
            LocalizedText.SetProvider(battleLogText,()=>sentence.Text + "\n→ " + sentence.Target + "\n" + Localization.Present(status) +
                (result.Action.Modal == BattleModal.Muessen ? " (müssen ×2)" : ""));
        }
        // Sentence and fallback words were captured with damage at reservation time.
        yield return AttackAudioPlayback.Play(audioSource, sentence);
        yield return new WaitForSeconds(0.8f);
    }

    private string GetCharacterEquipmentEffectText(EquipmentData equipment)
    {
        if (equipment == null)
        {
            return "";
        }

        if (!DamageCalculator.AppliesToCharacter(equipment))
        {
            return " (no effect)";
        }

        string text = "";

        if (equipment.attackBonus != 0)
        {
            text += $"ATK {(equipment.attackBonus > 0 ? "+" : "")}{equipment.attackBonus} ";
        }

        if (equipment.defenseBonus != 0)
        {
            text += $"DEF {(equipment.defenseBonus > 0 ? "+" : "")}{equipment.defenseBonus} ";
        }

        if (equipment.speedBonus != 0)
        {
            text += $"SPD {(equipment.speedBonus > 0 ? "+" : "")}{equipment.speedBonus} ";
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        return " <color=#ff5555>(" + text.TrimEnd() + ")</color>";
    }

    private string GetSkillEquipmentEffectText(EquipmentData equipment)
    {
        if (equipment == null)
        {
            return "";
        }

        if (!DamageCalculator.AppliesToSkill(equipment))
        {
            return " (no effect)";
        }

        string text = "";

        if (equipment.powerBonus != 0)
        {
            text += $"Power {(equipment.powerBonus > 0 ? "+" : "")}{equipment.powerBonus} ";
        }

        if (equipment.accuracyBonus != 0)
        {
            text += $"Accuracy {(equipment.accuracyBonus > 0 ? "+" : "")}{equipment.accuracyBonus} ";
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        return " <color=#ff5555>(" + text.TrimEnd() + ")</color>";
    }

    private void UpdateHPText()
    {
        string playerName =
            playerCharacter.characterName;

        if (playerCharacterEquipment != null)
        {
            playerName =
                "<color=#66ccff>"
                + playerCharacterEquipment.equipmentName
                + "</color> "
                + playerName;
        }

        if (playerHPText != null)
        {
            playerHPText.text = $"{playerName} HP:\n{playerHP}"; LocalizedText.Learning(playerHPText);
        }

        if (enemyHPText != null)
        {
            enemyHPText.text = $"{enemyCharacter.characterName} HP:\n{enemyHP}"; LocalizedText.Learning(enemyHPText);
        }
    }

    private IEnumerator ShowEnemyDamage(int damage)
    {
        if (enemyDamageText == null) yield break;

        enemyDamageText.text = "-" + damage;

        yield return new WaitForSeconds(1f);

        enemyDamageText.text = "";
    }

    private IEnumerator ShowPlayerDamage(int damage)
    {
        if (playerDamageText == null) yield break;

        playerDamageText.text = "-" + damage;

        yield return new WaitForSeconds(1f);

        playerDamageText.text = "";
    }

    private void UsePlayerSkill(int index)
    {
        if (isProcessing) return;
        if (playerCharacter == null || playerCharacter.skillIds == null) return;
        if (index < 0 || index >= playerCharacter.skillIds.Length) return;

        string skillId = playerCharacter.skillIds[index];
        if(!AllowsInput(TutorialInput.Skill,skillId))return;
        SkillData skill = GameDatabase.Instance.GetSkill(skillId);
        if (skill == null) return;

        ResolveTurnAdverbContest(
            out EquipmentData playerAdverb,
            out EquipmentData enemyAdverb,
            out string contestLog);

        UseSkill(skill, playerAdverb, enemyAdverb, contestLog);
    }

    private void ResolveTurnAdverbContest(out EquipmentData playerAdverb,
        out EquipmentData enemyAdverb, out string contestLog)
    {
        int playerSpeed = playerCharacter.speed + DamageCalculator.GetCharacterSpeedBonus(playerCharacterEquipment);
        int enemySpeed = enemyCharacter.speed;
        var contest = AdverbContestResolver.Resolve(selectedTurnAdverb, enemySelectedTurnAdverb,
            playerSpeed, enemySpeed, () => Random.value < 0.5f);
        playerAdverb = contest.PlayerAdverb;
        enemyAdverb = contest.EnemyAdverb;
        contestLog = "";
        if (!contest.Contested) return;
        string name = selectedTurnAdverb.equipmentName;
        string winner = contest.PlayerWon ? playerCharacter.characterName : enemyCharacter.characterName;
        if (playerSpeed == enemySpeed)
            contestLog = Localization.Text("ui.contest.tie",name,playerSpeed,Localization.Present(winner));
        else
        {
            int faster = contest.PlayerWon ? playerSpeed : enemySpeed;
            int slower = contest.PlayerWon ? enemySpeed : playerSpeed;
            contestLog = Localization.Text("ui.contest.speed",name,Localization.Present(winner),faster,slower);
        }
    }

    private void EnemyAttack(EquipmentData skillEquipment = null)
    {
        if(Tutorial?.PassiveEnemy==true){battleContext.SkipEnemyTurn();if(battleLogText!=null)battleLogText.text="Training opponent waits. Choose your next skill.";return;}
        var usableSkills = new List<SkillData>();
        foreach (string skillId in enemyCharacter.skillIds ?? System.Array.Empty<string>())
        {
            SkillData candidate = GameDatabase.Instance.GetSkill(skillId);
            if (candidate != null) usableSkills.Add(candidate);
        }
        if (usableSkills.Count == 0)
        {
            Debug.LogError("Enemy cannot attack because it has no usable skills.");
            if (battleLogText != null) battleLogText.text = "Enemy has no usable skills.";
            battleContext.SkipEnemyTurn();
            return;
        }
        // Preserve the existing random enemy skill choice and its random-number consumption.
        SkillData skill = usableSkills[Random.Range(0, usableSkills.Count)];
        var result = battleContext.Resolve(new BattleAction(BattleSide.Enemy, skill, skillEquipment));
        if (!result.CanUse || !battleContext.Apply(result)) return;
        StartCoroutine(ShakeRenderer(playerRenderer));
        PlaySoundEffect("slash");
        UpdateHPText();
        var sentence = SentenceGenerator.Generate(new SentenceContext {
            Subject = enemyCharacter, Target = playerCharacter, Skill = skill, SkillEquipment = skillEquipment
        });
        string skillEffect = GetSkillEquipmentEffectText(skillEquipment);
        string effectivenessText = GetEffectivenessText(enemyCharacter, playerCharacter);
        if (battleLogText != null)
            LocalizedText.SetProvider(battleLogText,()=>Localization.Text("ui.battle.enemy.action",sentence.EquippedVerb,Localization.Present(skillEffect),sentence.Text,result.Damage,Localization.Present(effectivenessText)));
        StartCoroutine(ShowPlayerDamage(result.Damage));
    }

    private void LoadCharacterImages()
    {
        if (playerRenderer == null)
        {
            Debug.LogError("Player Renderer is not assigned in BattleManager Inspector.");
            return;
        }

        if (enemyRenderer == null)
        {
            Debug.LogError("Enemy Renderer is not assigned in BattleManager Inspector.");
            return;
        }

        Sprite playerSprite = Resources.Load<Sprite>(
            $"Images/Characters/{playerCharacter.illustration}");

        Sprite enemySprite = Resources.Load<Sprite>(
            $"Images/Characters/{enemyCharacter.illustration}");

        if (playerSprite == null)
        {
            playerRenderer.sprite = null;
            ShowMissingPortrait(playerRenderer,playerCharacter);
            Debug.LogWarning($"Character artwork is not yet available: {playerCharacter.characterName}");
        }
        else
        {
            playerRenderer.sprite = playerSprite;
            Debug.Log($"Player sprite loaded: {playerCharacter.illustration}");
        }

        if (enemySprite == null)
        {
            enemyRenderer.sprite = null;
            ShowMissingPortrait(enemyRenderer,enemyCharacter);
            Debug.LogWarning($"Character artwork is not yet available: {enemyCharacter.characterName}");
        }
        else
        {
            enemyRenderer.sprite = enemySprite;
            Debug.Log($"Enemy sprite loaded: {enemyCharacter.illustration}");
        }
    }

    private void ShowMissingPortrait(SpriteRenderer renderer,CharacterData character)
    {
        if(!StageCatalog.IsTutorialEnemy(character.id) || playerHPText==null || Camera.main==null)return;
        var point=Camera.main.WorldToViewportPoint(renderer.transform.position);
        var root=new GameObject("PortraitPlaceholder_"+character.id,typeof(RectTransform),typeof(Image));root.transform.SetParent(playerHPText.canvas.transform,false);root.transform.SetAsFirstSibling();
        var rect=(RectTransform)root.transform;rect.anchorMin=rect.anchorMax=new Vector2(Mathf.Clamp(point.x,.24f,.76f),Mathf.Clamp(point.y,.35f,.65f));rect.sizeDelta=new Vector2(260,360);rect.anchoredPosition=Vector2.zero;
        var image=root.GetComponent<Image>();image.color=new Color(.08f,.13f,.21f,.85f);image.raycastTarget=false;
        var label=new GameObject("PortraitPlaceholderName",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(root.transform,false);
        var lr=(RectTransform)label.transform;lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=new Vector2(16,16);lr.offsetMax=new Vector2(-16,-16);
        var text=label.GetComponent<TextMeshProUGUI>();text.font=playerHPText.font;text.fontSharedMaterial=playerHPText.fontSharedMaterial;LocalizedText.Set(text,"ui.portrait.named",character.characterName);text.enableAutoSizing=true;text.fontSizeMin=24;text.fontSizeMax=34;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
    }

    private IEnumerator ShakeRenderer(SpriteRenderer targetRenderer)
    {
        if (targetRenderer == null) yield break;

        Transform target = targetRenderer.transform;
        Vector3 originalPosition = target.localPosition;

        float duration = 0.2f;
        float elapsed = 0f;
        float strength = 0.15f;

        while (elapsed < duration)
        {
            float offsetX = Random.Range(-strength, strength);
            target.localPosition = originalPosition + new Vector3(offsetX, 0f, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        target.localPosition = originalPosition;
    }

    private string GetEffectivenessText(CharacterData attacker, CharacterData defender)
    {
        float multiplier = DamageCalculator.GetGenderMultiplierForLog(
            attacker.grammaticalGender,
            defender.grammaticalGender
        );

        if (multiplier > 1.0f)
        {
            return "<color=#ff5555>Effective!</color>";
        }

        if (multiplier < 1.0f)
        {
            return "<color=#888888>Not effective...</color>";
        }

        return "";
    }

    private void PlaySoundEffect(string effectName)
    {
        if (audioSource == null) return;

        AudioClip clip =
            Resources.Load<AudioClip>(
                $"Audio/SoundEffects/{effectName}");

        if (clip != null)
        {
            GameAudio.PlayOneShot(audioSource, clip, GameAudioChannel.Sfx);
        }
        else
        {
            Debug.LogWarning(
                $"Sound effect not found: {effectName}");
        }
    }

    private void GenerateAdverbCards()
    {
        Selection = new BattleSelection();
        selectedTurnAdverb = null;
        enemySelectedTurnAdverb = null;
        currentAdverbCards.Clear();

        if (adverbCardButtons == null || adverbCardButtons.Length == 0)
        {
            return;
        }

        List<EquipmentData> candidates = GetBattleAdverbCandidates();

        for (int i = 0; i < adverbCardButtons.Length; i++)
        {
            if (adverbCardButtons[i] == null)
            {
                continue;
            }

            if (adverbCardTexts == null ||
                i >= adverbCardTexts.Length ||
                adverbCardTexts[i] == null)
            {
                adverbCardButtons[i].gameObject.SetActive(false);
                continue;
            }

            if (candidates.Count == 0)
            {
                adverbCardButtons[i].gameObject.SetActive(false);
                continue;
            }

            EquipmentData picked = candidates[Random.Range(0, candidates.Count)];
            candidates.Remove(picked);
            currentAdverbCards.Add(picked);

            adverbCardButtons[i].gameObject.SetActive(true);
            adverbCardTexts[i].text = picked.equipmentName; LocalizedText.Learning(adverbCardTexts[i]);

            int index = currentAdverbCards.Count - 1;
            adverbCardButtons[i].onClick.RemoveAllListeners();
            adverbCardButtons[i].onClick.AddListener(() => SelectAdverbCard(index));
        }

        if (noAdverbButton != null)
        {
            noAdverbButton.onClick.RemoveAllListeners();
            noAdverbButton.onClick.AddListener(SelectNoAdverb);
        }

        ChooseEnemyAdverb();
        SetAdverbCardInteraction(true);
        RefreshSelectionUI();
    }

    private List<EquipmentData> GetBattleAdverbCandidates()
    {
        EquipmentData[] allEquipments = GameDatabase.Instance.GetAllEquipments();
        Dictionary<string, EquipmentData> validAdverbsById = allEquipments
            .Where(IsBattleAdverb)
            .GroupBy(e => e.id)
            .Where(group => !string.IsNullOrEmpty(group.Key))
            .ToDictionary(group => group.Key, group => group.First());

        List<EquipmentData> playerCollocations = GetCharacterCollocationAdverbs(
            playerCharacter,
            playerSkillEquipments,
            validAdverbsById);
        List<EquipmentData> enemyCollocations = GetCharacterCollocationAdverbs(
            enemyCharacter,
            enemySkillEquipments,
            validAdverbsById);

        // Keep one entry per character. If both characters collocate with the
        // same adverb, two entries intentionally remain and may both be shown.
        List<EquipmentData> result = new List<EquipmentData>();
        result.AddRange(playerCollocations);
        result.AddRange(enemyCollocations);
        return result;
    }

    private List<EquipmentData> GetCharacterCollocationAdverbs(
        CharacterData character,
        EquipmentData[] equippedSkillAdverbs,
        Dictionary<string, EquipmentData> validAdverbsById)
    {
        List<EquipmentData> result = new List<EquipmentData>();
        HashSet<string> addedIds = new HashSet<string>();

        if (character != null && character.collocationAdverbIds != null)
        {
            foreach (string adverbId in character.collocationAdverbIds)
            {
                AddCollocationAdverb(adverbId, validAdverbsById, addedIds, result);
            }
        }

        if (result.Count > 0)
        {
            return result;
        }

        if (equippedSkillAdverbs != null)
        {
            foreach (EquipmentData adverb in equippedSkillAdverbs)
            {
                if (adverb != null)
                {
                    AddCollocationAdverb(adverb.id, validAdverbsById, addedIds, result);
                }
            }
        }

        if (result.Count > 0)
        {
            return result;
        }

        // Existing character data has no collocation field yet. Use compatible
        // battle adverbs as a safe default so battles remain playable.
        foreach (EquipmentData adverb in validAdverbsById.Values)
        {
            if (character == null ||
                string.IsNullOrEmpty(character.language) ||
                string.IsNullOrEmpty(adverb.language) ||
                character.language == adverb.language)
            {
                AddCollocationAdverb(adverb.id, validAdverbsById, addedIds, result);
            }
        }

        return result;
    }

    private void AddCollocationAdverb(
        string adverbId,
        Dictionary<string, EquipmentData> validAdverbsById,
        HashSet<string> addedIds,
        List<EquipmentData> result)
    {
        if (string.IsNullOrEmpty(adverbId) || !addedIds.Add(adverbId))
        {
            return;
        }

        if (validAdverbsById.TryGetValue(adverbId, out EquipmentData adverb))
        {
            result.Add(adverb);
        }
        else
        {
            Debug.LogWarning($"Collocation adverb id is invalid or unusable: {adverbId}");
        }
    }

    private bool IsBattleAdverb(EquipmentData equipment)
    {
        if (equipment == null)
        {
            return false;
        }

        bool isAdverb =
            equipment.partOfSpeech == "adverb" ||
            equipment.partOfSpeech == "adjective_adverb";

        return isAdverb && DamageCalculator.AppliesToSkill(equipment);
    }

    private void ChooseEnemyAdverb()
    {
        if (currentAdverbCards.Count == 0)
        {
            return;
        }

        int selectedIndex = Random.Range(0, currentAdverbCards.Count);
        enemySelectedTurnAdverb = currentAdverbCards[selectedIndex];
        Debug.Log("Enemy selected adverb: " + enemySelectedTurnAdverb.equipmentName);
    }

    private void SetAdverbCardInteraction(bool interactable)
    {
        if (adverbCardButtons != null)
        {
            foreach (Button button in adverbCardButtons)
            {
                if (button != null)
                {
                    button.interactable = interactable;
                }
            }
        }

        if (noAdverbButton != null)
        {
            noAdverbButton.interactable = interactable;
        }
    }

    private void SelectAdverbCard(int index)
    {
        if (!AllowsInput(TutorialInput.Adverb) || isProcessing) return;
        if (index < 0 || index >= currentAdverbCards.Count) return;

        selectedTurnAdverb = currentAdverbCards[index];
        Selection.Select(BattleElementKind.Adverb, selectedTurnAdverb.id);
        RefreshSelectionUI();

        if (battleLogText != null)
        {
            LocalizedText.Set(battleLogText,"ui.battle.adverb.selected",selectedTurnAdverb.equipmentName);
        }

        Debug.Log("Selected adverb: " + selectedTurnAdverb.equipmentName);
    }

    private void SelectNoAdverb()
    {
        if (!AllowsInput(TutorialInput.Adverb) || isProcessing) return;

        selectedTurnAdverb = null;
        Selection.Select(BattleElementKind.Adverb, null);
        RefreshSelectionUI();

        if (battleLogText != null)
        {
            battleLogText.text = "No adverb selected.";
        }

        Debug.Log("No adverb selected.");
    }
}

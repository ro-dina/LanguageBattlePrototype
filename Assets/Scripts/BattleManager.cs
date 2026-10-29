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
    public static string PlayerCharacterId = "nacht";
    public static string EnemyCharacterId = "feuer";
    public static string SelectedCharacterId = "nacht";
    // Compatibility accessors route old callers to the selected character's committed loadout.
    public static string SelectedCharacterEquipmentId
    {
        get => CharacterLoadoutStore.GetLoadout(SelectedCharacterId)?.adjectiveId ?? "";
        set
        {
            var draft = CharacterLoadoutStore.GetLoadout(SelectedCharacterId);
            if (draft == null) return;
            draft.adjectiveId = value;
            CharacterLoadoutStore.Commit(draft);
        }
    }
    public static string[] SelectedSkillEquipmentIds => System.Array.ConvertAll(
        CharacterLoadoutStore.GetLoadout(SelectedCharacterId)?.skillSlots ?? System.Array.Empty<CharacterSkillSlotData>(),
        slot => slot.equippedAdverbId);
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
        var draft = CharacterLoadoutStore.GetLoadout(SelectedCharacterId);
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

    private void Start()
    {
        playerCharacterId = GameManager.SelectedCharacterId;
        enemyCharacterId = GameManager.EnemyCharacterId;

        if (GameDatabase.Instance == null)
        {
            Debug.LogError("Battle cannot start because GameDatabase is not available.");
            enabled = false;
            return;
        }

        if (!OwnedCharacters.IsCharacterOwned(playerCharacterId))
        {
            Debug.LogWarning("Battle rejected an unowned player character.", this);
            if (battleLogText != null) battleLogText.text = "Character is not owned.";
            isProcessing = true;
            if (skillPanel != null) skillPanel.SetActive(false);
            SetAdverbCardInteraction(false);
            enabled = false;
            return;
        }
        playerCharacter = CharacterLoadoutStore.GetCharacterForBattle(playerCharacterId);
        enemyCharacter = GameDatabase.Instance.GetCharacter(enemyCharacterId);

        if (playerCharacter == null || enemyCharacter == null)
        {
            Debug.LogError("Battle cannot start because character data is missing.");
            enabled = false;
            return;
        }


        SetupCharacterEquipment();

        battleContext = new BattleContext(playerCharacter, enemyCharacter, playerCharacterEquipment);

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
    }

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
                skillButtonTexts[i].text = skill.skillName;
            }
        }
    }

    public void OpenSkillPanel()
    {
        if (isProcessing) return;
        if (skillPanel != null)
        {
            skillPanel.SetActive(true);
        }
    }

    public void CloseSkillPanel()
    {
        if (isProcessing) return;
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
        if (isProcessing) return;
        BattleActionResult result = battleContext.Resolve(new BattleAction(BattleSide.Player, skill, skillEquipment));
        if (!result.CanUse)
        {
            if (battleLogText != null) battleLogText.text = result.FailureReason;
            return;
        }
        isProcessing = true;
        SetAdverbCardInteraction(false);
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
        if (battleLogText != null)
            yield return StartCoroutine(ShowPlayerAttackLog(result.Action.Skill, result.Action.SkillEquipment, result.Damage));
        if (!battleContext.Apply(result))
        {
            isProcessing = false;
            SetAdverbCardInteraction(true);
            yield break;
        }
        PlaySoundEffect("slash");
        UpdateHPText();
        StartCoroutine(ShowEnemyDamage(result.Damage));
        StartCoroutine(ShakeRenderer(enemyRenderer));
        yield return new WaitForSeconds(0.4f);
        if (battleContext.Outcome == BattleOutcome.PlayerWin)
        {
            GameManager.IsWin = true;
            SceneManager.LoadScene("Result");
            yield break;
        }
        EnemyAttack(enemySkillEquipment);
        yield return new WaitForSeconds(1f);
        if (battleContext.Outcome == BattleOutcome.EnemyWin)
        {
            GameManager.IsWin = false;
            SceneManager.LoadScene("Result");
            yield break;
        }
        isProcessing = false;
        GenerateAdverbCards();
    }

    private IEnumerator ShowPlayerAttackLog(SkillData skill, EquipmentData skillEquipment, int damage)
    {
        BattleSentence sentence = SentenceGenerator.Generate(new SentenceContext {
            Subject = playerCharacter, Target = enemyCharacter, Skill = skill,
            CharacterEquipment = playerCharacterEquipment, SkillEquipment = skillEquipment
        });
        string characterEffect = GetCharacterEquipmentEffectText(playerCharacterEquipment);
        string skillEffect = GetSkillEquipmentEffectText(skillEquipment);
        string effectivenessText = GetEffectivenessText(playerCharacter, enemyCharacter);
        Coroutine audio = StartCoroutine(AttackAudioPlayback.Play(audioSource, sentence));
        battleLogText.text = sentence.BaseSubject;
        yield return new WaitForSeconds(0.4f);
        battleLogText.text = sentence.EquippedSubject + characterEffect;
        yield return new WaitForSeconds(0.4f);
        battleLogText.text = sentence.EquippedSubject + characterEffect + "\n" + sentence.BaseVerb;
        yield return new WaitForSeconds(0.3f);
        battleLogText.text = sentence.EquippedSubject + characterEffect + "\n" + sentence.EquippedVerb + skillEffect;
        yield return new WaitForSeconds(0.3f);
        battleLogText.text = sentence.EquippedSubject + characterEffect + "\n" +
            sentence.EquippedVerb + skillEffect + "\n" + "→ " + sentence.Target + "\n" +
            "<color=#ff5555>" + damage + " damage!</color> " + effectivenessText;
        if (audio != null) yield return audio;
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
            playerHPText.text = $"{playerName} HP:\n{playerHP}";
        }

        if (enemyHPText != null)
        {
            enemyHPText.text = $"{enemyCharacter.characterName} HP:\n{enemyHP}";
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
            contestLog = $"Both chose {name} and tied at SPD {playerSpeed}. {winner} won the draw!";
        else
        {
            int faster = contest.PlayerWon ? playerSpeed : enemySpeed;
            int slower = contest.PlayerWon ? enemySpeed : playerSpeed;
            contestLog = $"Both chose {name}. {winner} won it (SPD {faster} > {slower})!";
        }
    }

    private void EnemyAttack(EquipmentData skillEquipment = null)
    {
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
            battleLogText.text = $"Enemy used {sentence.EquippedVerb}{skillEffect}! {result.Damage} damage! {effectivenessText}";
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
            Debug.LogError($"Player sprite not found: Images/Characters/{playerCharacter.illustration}");
        }
        else
        {
            playerRenderer.sprite = playerSprite;
            Debug.Log($"Player sprite loaded: {playerCharacter.illustration}");
        }

        if (enemySprite == null)
        {
            Debug.LogError($"Enemy sprite not found: Images/Characters/{enemyCharacter.illustration}");
        }
        else
        {
            enemyRenderer.sprite = enemySprite;
            Debug.Log($"Enemy sprite loaded: {enemyCharacter.illustration}");
        }
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
            audioSource.PlayOneShot(clip);
        }
        else
        {
            Debug.LogWarning(
                $"Sound effect not found: {effectName}");
        }
    }

    private void GenerateAdverbCards()
    {
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
            adverbCardTexts[i].text = picked.equipmentName;

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
        if (isProcessing) return;
        if (index < 0 || index >= currentAdverbCards.Count) return;

        selectedTurnAdverb = currentAdverbCards[index];

        if (battleLogText != null)
        {
            battleLogText.text = "Selected adverb: " + selectedTurnAdverb.equipmentName;
        }

        Debug.Log("Selected adverb: " + selectedTurnAdverb.equipmentName);
    }

    private void SelectNoAdverb()
    {
        if (isProcessing) return;

        selectedTurnAdverb = null;

        if (battleLogText != null)
        {
            battleLogText.text = "No adverb selected.";
        }

        Debug.Log("No adverb selected.");
    }
}

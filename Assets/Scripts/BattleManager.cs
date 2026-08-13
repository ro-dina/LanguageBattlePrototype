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
    public static string SelectedCharacterEquipmentId = "";
    public static string[] SelectedSkillEquipmentIds = new string[4] { "", "", "", "" };
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
        if (SelectedSkillEquipmentIds == null)
        {
            SelectedSkillEquipmentIds = new string[4] { "", "", "", "" };
        }

        if (skillIndex < 0 || skillIndex >= SelectedSkillEquipmentIds.Length)
        {
            return;
        }

        SelectedSkillEquipmentIds[skillIndex] = equipmentId;
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
    private List<EquipmentData> currentAdverbCards = new List<EquipmentData>();

    private CharacterData playerCharacter;
    private CharacterData enemyCharacter;
    private EquipmentData playerCharacterEquipment;

    private EquipmentData[] playerSkillEquipments;

    private int playerHP;
    private int enemyHP;
    private bool isProcessing = false;

    private void Start()
    {
        playerCharacterId = GameManager.SelectedCharacterId;
        enemyCharacterId = GameManager.EnemyCharacterId;

        playerCharacter = GameDatabase.Instance.GetCharacter(playerCharacterId);
        enemyCharacter = GameDatabase.Instance.GetCharacter(enemyCharacterId);

        if (playerCharacter == null || enemyCharacter == null)
        {
            Debug.LogError("Battle cannot start because character data is missing.");
            return;
        }


        SetupCharacterEquipment();

        playerHP = playerCharacter.hp;
        enemyHP = enemyCharacter.hp;

        enemyDamageText.text = "";
        playerDamageText.text = "";

        SetupSkillButtons();
        LoadCharacterImages();
        skillPanel.SetActive(false);
        GenerateAdverbCards();
        UpdateHPText();

        if (battleLogText != null)
        {
            battleLogText.text = "Choose your skill!";
        }
    }

    private void SetupCharacterEquipment()
    {
        string equipmentId = GameManager.SelectedCharacterEquipmentId;

        if (string.IsNullOrEmpty(equipmentId))
        {
            equipmentId = playerCharacter.equippedAdjectiveId;
        }

        playerCharacterEquipment = GameDatabase.Instance.GetEquipment(equipmentId);

        if (playerCharacterEquipment != null)
        {
            Debug.Log($"Player character equipment loaded: {playerCharacterEquipment.equipmentName}");
        }

        playerSkillEquipments = new EquipmentData[playerCharacter.skillIds.Length];

        for (int i = 0; i < playerSkillEquipments.Length; i++)
        {
            string skillEquipmentId = "";

            if (GameManager.SelectedSkillEquipmentIds != null && i < GameManager.SelectedSkillEquipmentIds.Length)
            {
                skillEquipmentId = GameManager.SelectedSkillEquipmentIds[i];
            }

            if (string.IsNullOrEmpty(skillEquipmentId) && playerCharacter.skillSlots != null && i < playerCharacter.skillSlots.Length)
            {
                skillEquipmentId = playerCharacter.skillSlots[i].equippedAdverbId;
            }

            playerSkillEquipments[i] = GameDatabase.Instance.GetEquipment(skillEquipmentId);
        }
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
        skillPanel.SetActive(true);
    }

    public void CloseSkillPanel()
    {
        if (isProcessing) return;
        skillPanel.SetActive(false);
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

    private void UseSkill(SkillData skill,EquipmentData skillEquipment)
    {
        if (isProcessing) return;
        isProcessing = true;

        int damage =
            DamageCalculator.CalculateDamage(
                playerCharacter,
                enemyCharacter,
                skill,
                playerCharacterEquipment,
                skillEquipment,
                null);

        skillPanel.SetActive(false);
        StartCoroutine(PlayerTurnSequence(skill, skillEquipment, damage));
    }

    private IEnumerator PlayerTurnSequence(SkillData skill, EquipmentData skillEquipment, int damage)
    {
        if (battleLogText != null)
        {
            yield return StartCoroutine(ShowPlayerAttackLog(skill, skillEquipment, damage));
        }

        enemyHP -= damage;

        if (enemyHP < 0)
        {
            enemyHP = 0;
        }

        PlaySoundEffect("slash");

        UpdateHPText();
        StartCoroutine(ShowEnemyDamage(damage));
        StartCoroutine(ShakeRenderer(enemyRenderer));

        yield return new WaitForSeconds(0.4f);

        if (enemyHP <= 0)
        {
            GameManager.IsWin = true;
            SceneManager.LoadScene("Result");
            yield break;
        }

        EnemyAttack();

        yield return new WaitForSeconds(1f);

        if (playerHP <= 0)
        {
            GameManager.IsWin = false;
            SceneManager.LoadScene("Result");
            yield break;
        }
        GenerateAdverbCards();
        isProcessing = false;
    }

    private IEnumerator ShowPlayerAttackLog(SkillData skill, EquipmentData skillEquipment, int damage)
    {
        string baseNoun = playerCharacter.characterName;
        string equippedNoun = GetEquippedCharacterName(playerCharacter, playerCharacterEquipment);
        string characterEffect = GetCharacterEquipmentEffectText(playerCharacterEquipment);

        string baseVerb = GetConjugatedSkillName(skill, playerCharacter);
        string equippedVerb = GetEquippedSkillName(baseVerb, skillEquipment);
        string skillEffect = GetSkillEquipmentEffectText(skillEquipment);

        string target = enemyCharacter.characterName;
        string effectivenessText = GetEffectivenessText(playerCharacter, enemyCharacter);
        Coroutine attackAudioCoroutine = StartCoroutine(PlayAttackAudio(skill, skillEquipment));

        battleLogText.text = baseNoun;
        yield return new WaitForSeconds(0.4f);

        battleLogText.text = equippedNoun + characterEffect;
        yield return new WaitForSeconds(0.4f);

        battleLogText.text = equippedNoun + characterEffect + "\n" + baseVerb;
        yield return new WaitForSeconds(0.3f);

        battleLogText.text = equippedNoun + characterEffect + "\n" + equippedVerb + skillEffect;
        yield return new WaitForSeconds(0.3f);

        battleLogText.text =
            equippedNoun + characterEffect + "\n" +
            equippedVerb + skillEffect + "\n" +
            "→ " + target + "\n" +
            "<color=#ff5555>" + damage + " damage!</color> " + effectivenessText;

        if (attackAudioCoroutine != null)
        {
            yield return attackAudioCoroutine;
        }

        yield return new WaitForSeconds(0.8f);
    }

    private string GetEquippedCharacterName(CharacterData character, EquipmentData equipment)
    {
        if (equipment == null)
        {
            return character.characterName;
        }

        return "<color=#66ccff>" + equipment.equipmentName + "</color> " + character.characterName;
    }

    private string GetEquippedSkillName(string skillName, EquipmentData equipment)
    {
        if (equipment == null)
        {
            return skillName;
        }

        return "<color=#66ccff>" + equipment.equipmentName + "</color> " + skillName;
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

        playerHPText.text =
            $"{playerName} HP:\n{playerHP}";
        enemyHPText.text = $"{enemyCharacter.characterName} HP:\n{enemyHP}";
    }

    private IEnumerator ShowEnemyDamage(int damage)
    {
        enemyDamageText.text = "-" + damage;

        yield return new WaitForSeconds(1f);

        enemyDamageText.text = "";
    }

    private IEnumerator ShowPlayerDamage(int damage)
    {
        playerDamageText.text = "-" + damage;

        yield return new WaitForSeconds(1f);

        playerDamageText.text = "";
    }

    private IEnumerator BattleSequence()
    {
        isProcessing = true;
        // プレイヤーの攻撃ログを見る時間
        yield return new WaitForSeconds(1f);

        EnemyAttack();

        // 敵攻撃ログを見る時間
        yield return new WaitForSeconds(1f);

        if(playerHP <= 0)
        {
            GameManager.IsWin = false;
            SceneManager.LoadScene("Result");
        }
        isProcessing = false;
    }

    private void UsePlayerSkill(int index)
    {
        if (isProcessing) return;
        if (index < 0 || index >= playerCharacter.skillIds.Length) return;

        string skillId = playerCharacter.skillIds[index];
        SkillData skill = GameDatabase.Instance.GetSkill(skillId);
        if (skill == null) return;

        EquipmentData skillEquipment = selectedTurnAdverb;
        UseSkill(skill, skillEquipment);
    }

    private void EnemyAttack()
    {
        string skillId = enemyCharacter.skillIds[Random.Range(0, enemyCharacter.skillIds.Length)];
        SkillData skill = GameDatabase.Instance.GetSkill(skillId);
        if (skill == null) return;

        int damage =
            DamageCalculator.CalculateDamage(
                enemyCharacter,
                playerCharacter,
                skill,
                null,
                null,
                playerCharacterEquipment);

        playerHP -= damage;
        StartCoroutine(ShakeRenderer(playerRenderer));

        PlaySoundEffect("slash");

        if(playerHP < 0)
            playerHP = 0;

        UpdateHPText();

        string effectivenessText = GetEffectivenessText(enemyCharacter, playerCharacter);

        string enemySkillName = GetConjugatedSkillName(skill, enemyCharacter);
        battleLogText.text = $"Enemy used {enemySkillName}! {damage} damage! {effectivenessText}";
        StartCoroutine(ShowPlayerDamage(damage));
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

    private string GetConjugatedSkillName(SkillData skill, CharacterData subject)
    {
        if (skill == null) return "";

        if (skill.conjugations == null)
        {
            return skill.skillName;
        }

        if (subject != null && subject.grammaticalNumber == "plural")
        {
            if (!string.IsNullOrEmpty(skill.conjugations.thirdPersonPlural))
            {
                return skill.conjugations.thirdPersonPlural;
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(skill.conjugations.thirdPersonSingular))
            {
                return skill.conjugations.thirdPersonSingular;
            }
        }

        return skill.skillName;
    }

    private string BuildAttackSentenceAudioKey(SkillData skill, EquipmentData skillEquipment)
    {
        string subjectPart = playerCharacter.characterName;
        if (playerCharacterEquipment != null)
        {
            subjectPart = playerCharacterEquipment.equipmentName + " " + subjectPart;
        }

        string verbPart = GetConjugatedSkillName(skill, playerCharacter);
        if (skillEquipment != null)
        {
            verbPart = skillEquipment.equipmentName + " " + verbPart;
        }

        string sentence = subjectPart + " " + verbPart + " " + enemyCharacter.characterName;
        return BuildAudioKey(sentence);
    }

    private IEnumerator PlayAttackAudio(SkillData skill, EquipmentData skillEquipment)
    {
        if (audioSource == null) yield break;

        string sentenceKey = BuildAttackSentenceAudioKey(skill, skillEquipment);
        AudioClip sentenceClip = Resources.Load<AudioClip>($"Audio/Sentences/{sentenceKey}");

        if (sentenceClip != null)
        {
            audioSource.PlayOneShot(sentenceClip);
            yield return new WaitForSeconds(sentenceClip.length + 0.05f);
            yield break;
        }

        Debug.LogWarning($"Sentence audio not found: Audio/Sentences/{sentenceKey}. Falling back to word audio.");
        yield return StartCoroutine(PlayAttackWordSequence(skill, skillEquipment));
    }

    private IEnumerator PlayAttackWordSequence(SkillData skill, EquipmentData skillEquipment)
    {
        if (audioSource == null) yield break;

        if (playerCharacterEquipment != null)
        {
            yield return StartCoroutine(PlayWordAudioAndWait(playerCharacterEquipment.equipmentName));
        }

        yield return StartCoroutine(PlayWordAudioAndWait(playerCharacter.characterName));

        if (skillEquipment != null)
        {
            yield return StartCoroutine(PlayWordAudioAndWait(skillEquipment.equipmentName));
        }

        string verb = GetConjugatedSkillName(skill, playerCharacter);
        yield return StartCoroutine(PlayWordAudioAndWait(verb));

        yield return StartCoroutine(PlayWordAudioAndWait(enemyCharacter.characterName));
    }
    private IEnumerator PlayWordAudioAndWait(string word)
    {
        if (audioSource == null || string.IsNullOrEmpty(word))
        {
            yield break;
        }

        string key = BuildAudioKey(word);
        AudioClip clip = Resources.Load<AudioClip>($"Audio/Words/{key}");

        if (clip == null)
        {
            Debug.LogWarning($"Word audio not found: Audio/Words/{key}");
            yield return new WaitForSeconds(0.15f);
            yield break;
        }

        audioSource.PlayOneShot(clip);
        yield return new WaitForSeconds(clip.length + 0.05f);
    }

    private string BuildAudioKey(string text)
    {
        return text
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("<color=#66ccff>", "")
            .Replace("<color=#ff5555>", "")
            .Replace("</color>", "")
            .Replace(".", "")
            .Replace(",", "")
            .Replace("!", "")
            .Replace("?", "");
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

    private void PlayWordAudio(string word)
    {
        if (audioSource == null) return;

        AudioClip clip =
            Resources.Load<AudioClip>(
                $"Audio/Words/{word}");

        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void GenerateAdverbCards()
    {
        selectedTurnAdverb = null;
        currentAdverbCards.Clear();

        if (adverbCardButtons == null || adverbCardButtons.Length == 0)
        {
            return;
        }

        EquipmentData[] allEquipments = GameDatabase.Instance.GetAllEquipments();

        List<EquipmentData> candidates = allEquipments
            .Where(e => e != null &&
                (e.partOfSpeech == "adverb" ||
                 e.partOfSpeech == "adjective_adverb" ||
                 e.targetRole == "skill" ||
                 e.targetRole == "both"))
            .ToList();

        for (int i = 0; i < adverbCardButtons.Length; i++)
        {
            if (adverbCardButtons[i] == null)
            {
                continue;
            }

            if (i >= adverbCardTexts.Length || adverbCardTexts[i] == null)
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
            currentAdverbCards.Add(picked);

            adverbCardButtons[i].gameObject.SetActive(true);
            adverbCardTexts[i].text = picked.equipmentName;

            int index = i;
            adverbCardButtons[i].onClick.RemoveAllListeners();
            adverbCardButtons[i].onClick.AddListener(() => SelectAdverbCard(index));
        }

        if (noAdverbButton != null)
        {
            noAdverbButton.onClick.RemoveAllListeners();
            noAdverbButton.onClick.AddListener(SelectNoAdverb);
        }
    }

    private void SelectAdverbCard(int index)
    {
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
        selectedTurnAdverb = null;

        if (battleLogText != null)
        {
            battleLogText.text = "No adverb selected.";
        }

        Debug.Log("No adverb selected.");
    }
}
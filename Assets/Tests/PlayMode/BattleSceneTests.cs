using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

[PrebuildSetup(typeof(TestSaveIsolation))]
[PostBuildCleanup(typeof(TestSaveIsolation))]
public class BattleSceneTests
{
    private static string Instance(string species)
    {
        var instances = CharacterInstances.GetByCharacterId(species);
        return instances.Count > 0 ? instances[0].instanceId : CharacterInstances.Create(species).instanceId;
    }

    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);

    [UnitySetUp]
    public IEnumerator Setup()
    {
        SceneManager.LoadScene("GermanyHome"); yield return null; yield return null;
        GameDatabase.EnsureAvailable();
        var owned = new OwnedCharactersState(); owned.characterIds.Add("nacht");
        OwnedCharacters.RestoreState(owned);
        CharacterLoadoutStore.RestoreState(new CharacterLoadoutState());
        GameManager.SelectedCharacterId = "nacht";
        GameManager.EnemyCharacterId = "feuer";
        SceneManager.LoadScene("GermanyHome");
        yield return null;
    }

    private IEnumerator LoadBattle()
    {
        SceneManager.LoadScene("Battle");
        yield return null; yield return null;
    }

    private IEnumerator WaitForTurn(BattleManager battle)
    {
        float timeout = Time.realtimeSinceStartup + 25;
        while (battle != null && Field<bool>(battle, "isProcessing") && Time.realtimeSinceStartup < timeout) yield return null;
        Assert.That(battle == null || !Field<bool>(battle, "isProcessing"), Is.True, "Battle turn completes.");
    }

    [UnityTest]
    public IEnumerator NormalCommandRunsPlayerThenEnemyAndReturnsToSelection()
    {
        yield return LoadBattle();
        var battle = Object.FindAnyObjectByType<BattleManager>();
        Call(battle, "SelectNoAdverb");
        battle.OpenSkillPanel();
        Assert.That(Field<GameObject>(battle, "skillPanel").activeSelf, Is.True);
        int expected=DamageCalculator.CalculateDamage(battle.Context.Player.Character,battle.Context.Enemy.Character,GameDatabase.Instance.GetSkill("sein"),battle.Context.Player.Equipment);
        battle.UseSkill1();
        yield return WaitForTurn(battle);
        Assert.That(battle.Context.Enemy.HP, Is.EqualTo(18-expected));
        Assert.That(battle.Context.Player.HP, Is.LessThan(23).And.GreaterThan(0));
        Assert.That(battle.Context.Round, Is.EqualTo(2));
        Assert.That(battle.Context.TurnState, Is.EqualTo(BattleTurnState.AwaitingPlayer));
        Assert.That(Field<TMP_Text>(battle, "battleLogText").text, Does.Contain("Enemy used"));
    }

    [UnityTest]
    public IEnumerator ChosenAdverbAndCommittedAdjectiveProduceWinResult()
    {
        var draft = CharacterLoadoutStore.GetLoadout(Instance("nacht"));
        draft.adjectiveId = "schwarz";
        CharacterLoadoutStore.Commit(draft);
        yield return LoadBattle();
        var battle = Object.FindAnyObjectByType<BattleManager>();
        var cards = Field<System.Collections.Generic.List<EquipmentData>>(battle, "currentAdverbCards");
        int index = cards.FindIndex(e => e.id == "sehr");
        Assert.That(index, Is.GreaterThanOrEqualTo(0));
        Call(battle, "SelectAdverbCard", index);
        Assert.That(Field<EquipmentData>(battle, "selectedTurnAdverb").id, Is.EqualTo("sehr"));
        var context = battle.Context;
        battle.UseSkill1();
        yield return WaitForTurn(battle);
        // Affinity now follows the target corpus rather than gender. Finish with ordinary skills if needed.
        while(context.Outcome==BattleOutcome.Ongoing){battle.UseSkill1();yield return WaitForTurn(battle);}
        Assert.That(context.Outcome, Is.EqualTo(BattleOutcome.PlayerWin));
        Assert.That(GameManager.IsWin, Is.True);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Result"));
    }

    [UnityTest]
    public IEnumerator EnemyKnockoutReachesLossResult()
    {
        yield return LoadBattle();
        var battle = Object.FindAnyObjectByType<BattleManager>();
        var context = battle.Context;
        typeof(BattleCombatant).GetProperty("HP").SetValue(context.Player, 1);
        Call(battle, "SelectNoAdverb");
        battle.UseSkill1();
        yield return WaitForTurn(battle);
        Assert.That(context.Outcome, Is.EqualTo(BattleOutcome.EnemyWin));
        Assert.That(GameManager.IsWin, Is.False);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Result"));
    }

    [UnityTest]
    public IEnumerator SentenceAudioAndWordFallbackFinishUsingExistingResources()
    {
        var db = GameDatabase.Instance;
        // The existing recording remains playable, even though its legacy word order is no longer generated.
        var sentence = new BattleSentence { SentenceAudioKey = "schwarz_jahr_sehr_ist_feuer" };
        Assert.That(Resources.Load<AudioClip>("Audio/Sentences/" + sentence.SentenceAudioKey), Is.Not.Null);
        var audioObject = new GameObject("Test audio");
        var source = audioObject.AddComponent<AudioSource>();
        yield return AttackAudioPlayback.Play(source, sentence);
        var fallback = SentenceGenerator.Generate(new SentenceContext {
            Subject = db.GetCharacter("jahr"), Target = db.GetCharacter("feuer"), Skill = db.GetSkill("sein")
        });
        Assert.That(Resources.Load<AudioClip>("Audio/Sentences/" + fallback.SentenceAudioKey), Is.Null);
        Assert.That(fallback.SpeechWords.Any(word => Resources.Load<AudioClip>("Audio/Words/" + SentenceGenerator.AudioKey(word)) != null), Is.True);
        Assert.That(fallback.SpeechWords.Any(word => Resources.Load<AudioClip>("Audio/Words/" + SentenceGenerator.AudioKey(word)) == null), Is.True,
            "Newly generated grammar also exercises the safe missing-word fallback.");
        yield return AttackAudioPlayback.Play(source, fallback);
        Assert.That(AttackAudioPlayback.Play(null, sentence).MoveNext(), Is.False);
        Object.Destroy(audioObject);
    }
}

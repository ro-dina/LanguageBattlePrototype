using NUnit.Framework;

public class GermanSentenceTests
{
    private SentenceContext Context(bool plural = false) => new SentenceContext {
        Subject = new CharacterData { characterName = plural ? "Hunde" : "Hund", language = "de", grammaticalGender = "masculine", grammaticalNumber = plural ? "plural" : "singular" },
        Target = new CharacterData { characterName = "Feuer" },
        Skill = new SkillData { skillName = "angreifen", infinitive = "angreifen", separablePrefix = "an",
            conjugations = new SkillConjugations { thirdPersonSingular = "greift an", thirdPersonPlural = "greifen an" } },
        SkillEquipment = new EquipmentData { equipmentName = "schnell" }
    };
    [TestCase(false, "Der Hund greift schnell an.")][TestCase(true, "Die Hunde greifen schnell an.")]
    public void PresentSeparatesTheParticleAfterTheAdverb(bool plural, string expected) =>
        Assert.That(SentenceGenerator.Generate(Context(plural)).Text, Is.EqualTo(expected));
    [TestCase(false, "Der Hund wird schnell angreifen.")][TestCase(true, "Die Hunde werden schnell angreifen.")]
    public void FutureConjugatesWerdenAndUsesJoinedInfinitive(bool plural, string expected)
    {
        var context = Context(plural); context.Tense = BattleTense.Future;
        var sentence = SentenceGenerator.Generate(context); Assert.That(sentence.Text, Is.EqualTo(expected));
        Assert.That(string.Join(" ", sentence.SpeechWords) + ".", Is.EqualTo(expected));
        Assert.That(sentence.SentenceAudioKey, Is.EqualTo(SentenceGenerator.AudioKey(expected)));
    }
    [TestCase(false, "Der Hund muss schnell angreifen.")][TestCase(true, "Die Hunde müssen schnell angreifen.")]
    public void MuessenConjugatesAndUsesJoinedInfinitive(bool plural, string expected)
    {
        var context = Context(plural); context.Modal = BattleModal.Muessen;
        Assert.That(SentenceGenerator.Generate(context).Text, Is.EqualTo(expected));
    }
    [Test] public void NoAdverbAndUnsplitConjugationMetadataAreSupported()
    {
        var context = Context(); context.SkillEquipment = null;
        context.Skill.conjugations.thirdPersonSingular = "greift";
        Assert.That(SentenceGenerator.Generate(context).Text, Is.EqualTo("Der Hund greift an."));
        context.Skill.skillName = "laufen"; context.Skill.infinitive = null; context.Skill.separablePrefix = null;
        context.Skill.conjugations.thirdPersonSingular = "läuft";
        Assert.That(SentenceGenerator.Generate(context).Text, Is.EqualTo("Der Hund läuft."));
        context.Tense = BattleTense.Future;
        Assert.That(SentenceGenerator.Generate(context).Text, Is.EqualTo("Der Hund wird laufen."));
    }
    [Test] public void CompoundModifiersAreExplicitlyUnsupported()
    {
        var context = Context(); context.Tense = BattleTense.Future; context.Modal = BattleModal.Muessen;
        Assert.Throws<System.NotSupportedException>(() => SentenceGenerator.Generate(context));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

public sealed class SentenceContext
{
    public CharacterData Subject;
    public CharacterData Target;
    public SkillData Skill;
    public EquipmentData CharacterEquipment;
    public EquipmentData SkillEquipment;
    public BattleTense Tense;
    public BattleModal Modal;
    public string Language => Subject?.language ?? Skill?.language;
}

public sealed class BattleSentence
{
    public string BaseSubject;
    public string EquippedSubject;
    public string BaseVerb;
    public string EquippedVerb;
    public string Target;
    public string Text;
    public string[] SpeechWords;
    public string SentenceAudioKey;
}

public static class SentenceGenerator
{
    public static BattleSentence Generate(SentenceContext context)
    {
        if (context?.Subject == null || context.Target == null || context.Skill == null)
            throw new ArgumentException("A subject, target and skill are required.", nameof(context));
        if (context.Tense == BattleTense.Future && context.Modal != BattleModal.None)
            throw new NotSupportedException("Compound modal constructions are not enabled yet.");
        bool german = context.Language == "de";
        if (!german && (context.Tense != BattleTense.Present || context.Modal != BattleModal.None))
            throw new NotSupportedException("German modifiers require German sentence data.");
        bool plural = context.Subject.grammaticalNumber == "plural";
        string subject = german ? GermanSubject(context.Subject, context.CharacterEquipment) :
            PlainDecoration(context.Subject.characterName, context.CharacterEquipment);
        string finite = Conjugate(context.Skill, context.Subject);
        string particle = "";
        if (german) SplitSeparable(context.Skill, ref finite, out particle);
        string infinitive = string.IsNullOrEmpty(context.Skill.infinitive) ? context.Skill.skillName : context.Skill.infinitive;
        string tail = particle;
        if (context.Tense == BattleTense.Future)
        { finite = plural ? "werden" : "wird"; tail = infinitive; }
        else if (context.Modal == BattleModal.Muessen)
        { finite = plural ? "müssen" : "muss"; tail = infinitive; }
        var parts = new List<string> { subject, finite };
        if (context.SkillEquipment != null) parts.Add(context.SkillEquipment.equipmentName);
        if (!string.IsNullOrEmpty(context.Skill.sentenceComplement)) parts.Add(context.Skill.sentenceComplement);
        if (!string.IsNullOrEmpty(tail)) parts.Add(tail);
        string text = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return new BattleSentence {
            BaseSubject = context.Subject.characterName,
            EquippedSubject = subject,
            BaseVerb = finite,
            EquippedVerb = finite + (context.SkillEquipment == null ? "" : " " + context.SkillEquipment.equipmentName),
            Target = context.Target.characterName,
            Text = text + ".",
            SpeechWords = words,
            SentenceAudioKey = AudioKey(text)
        };
    }

    private static string GermanSubject(CharacterData subject, EquipmentData adjective)
    {
        string name = subject.characterName;
        // Explicitly supplied subject phrases are accepted without adding another article.
        if (name.StartsWith("Der ", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Die ", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Das ", StringComparison.OrdinalIgnoreCase))
            return adjective == null ? name : name.Insert(name.IndexOf(' ') + 1, adjective.equipmentName + (subject.grammaticalNumber == "plural" ? "en " : "e "));
        bool plural = subject.grammaticalNumber == "plural";
        string article = plural || subject.grammaticalGender == "feminine" ? "Die" :
            subject.grammaticalGender == "masculine" ? "Der" : subject.grammaticalGender == "neuter" ? "Das" : "";
        if (article == "") return PlainDecoration(name, adjective);
        return article + " " + (adjective == null ? "" : adjective.equipmentName + (plural ? "en " : "e ")) + name;
    }

    private static void SplitSeparable(SkillData skill, ref string finite, out string particle)
    {
        particle = skill.separablePrefix ?? "";
        // Conjugation tables commonly already contain 'greift an'. Normalize once.
        int space = finite.LastIndexOf(' ');
        if (space > 0 && (particle.Length == 0 || finite.Substring(space + 1) == particle))
        { particle = finite.Substring(space + 1); finite = finite.Substring(0, space); }
    }

    public static string Conjugate(SkillData skill, CharacterData subject)
    {
        if (skill == null) return "";
        string conjugated = subject?.grammaticalNumber == "plural"
            ? skill.conjugations?.thirdPersonPlural : skill.conjugations?.thirdPersonSingular;
        return string.IsNullOrEmpty(conjugated) ? skill.skillName : conjugated;
    }
    private static string PlainDecoration(string text, EquipmentData equipment) => equipment == null ? text : equipment.equipmentName + " " + text;
    public static string Decorate(string text, EquipmentData equipment) => equipment == null
        ? text : "<color=#66ccff>" + equipment.equipmentName + "</color> " + text;
    public static string AudioKey(string text) => text.Trim().ToLowerInvariant()
        .Replace(" ", "_").Replace("<color=#66ccff>", "").Replace("<color=#ff5555>", "")
        .Replace("</color>", "").Replace(".", "").Replace(",", "").Replace("!", "").Replace("?", "");
}

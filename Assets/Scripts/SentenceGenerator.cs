using System;
using System.Collections.Generic;

public sealed class SentenceContext
{
    public CharacterData Subject;
    public CharacterData Target;
    public SkillData Skill;
    public EquipmentData CharacterEquipment;
    public EquipmentData SkillEquipment;
    public string Language => Subject?.language ?? Skill?.language;
}

public sealed class BattleSentence
{
    public string BaseSubject;
    public string EquippedSubject;
    public string BaseVerb;
    public string EquippedVerb;
    public string Target;
    public string[] SpeechWords;
    public string SentenceAudioKey;
}

public static class SentenceGenerator
{
    public static BattleSentence Generate(SentenceContext context)
    {
        if (context?.Subject == null || context.Target == null || context.Skill == null)
            throw new ArgumentException("A subject, target and skill are required.", nameof(context));
        // Existing German-compatible word order is retained. Future language rules dispatch here.
        string verb = Conjugate(context.Skill, context.Subject);
        string subject = context.Subject.characterName;
        string target = context.Target.characterName;
        var words = new List<string>();
        if (context.CharacterEquipment != null) words.Add(context.CharacterEquipment.equipmentName);
        words.Add(subject);
        if (context.SkillEquipment != null) words.Add(context.SkillEquipment.equipmentName);
        words.Add(verb);
        words.Add(target);
        return new BattleSentence {
            BaseSubject = subject,
            EquippedSubject = Decorate(subject, context.CharacterEquipment),
            BaseVerb = verb,
            EquippedVerb = Decorate(verb, context.SkillEquipment),
            Target = target,
            SpeechWords = words.ToArray(),
            SentenceAudioKey = AudioKey(string.Join(" ", words))
        };
    }

    public static string Conjugate(SkillData skill, CharacterData subject)
    {
        if (skill == null) return "";
        string conjugated = subject?.grammaticalNumber == "plural"
            ? skill.conjugations?.thirdPersonPlural : skill.conjugations?.thirdPersonSingular;
        return string.IsNullOrEmpty(conjugated) ? skill.skillName : conjugated;
    }

    public static string Decorate(string text, EquipmentData equipment) => equipment == null
        ? text : "<color=#66ccff>" + equipment.equipmentName + "</color> " + text;

    public static string AudioKey(string text) => text.Trim().ToLowerInvariant()
        .Replace(" ", "_").Replace("<color=#66ccff>", "").Replace("<color=#ff5555>", "")
        .Replace("</color>", "").Replace(".", "").Replace(",", "").Replace("!", "").Replace("?", "");
}

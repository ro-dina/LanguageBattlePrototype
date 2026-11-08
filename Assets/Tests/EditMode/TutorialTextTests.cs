using System.Linq;
using NUnit.Framework;
using UnityEngine;
public class TutorialTextTests
{
    [TestCase("technik_basics")][TestCase("adverb_basics")][TestCase("adjektiv_basics")][TestCase("modalverb_basics")][TestCase("zukunft_basics")]
    public void EveryOperationAndPracticeHintHasAnExplicitBilingualKey(string id)
    {
        var definition=TutorialCatalog.Get(id);
        foreach(var step in definition.steps.Where(s=>!string.IsNullOrEmpty(s.hint)))
        {
            Assert.That(step.hintKey,Is.Not.Null.And.Not.Empty);Assert.That(Localization.Source(step.hintKey),Is.EqualTo(step.hint));
            var ja=Localization.TextFor("ja",step.hintKey);Assert.That(ja,Is.Not.EqualTo(step.hint));Assert.That(ja,Does.Not.Contain("Unavailable"));
        }
    }
    [Test] public void EveryTutorialDialogueHasMatchingKeyAndKeepsGermanParameters()
    {
        var table=JsonUtility.FromJson<DialogueDatabase>(Resources.Load<TextAsset>("Data/dialogues").text);
        foreach(var line in table.dialogues)
        {
            Assert.That(line.localizationKey,Is.EqualTo("dialogue."+line.dialogueId));
            string ja=Localization.TextFor("ja",line.localizationKey,"angreifen","Technik","schnell","schwarz",15,"Der Hund wird schnell angreifen.");
            Assert.That(ja,Is.Not.EqualTo(Localization.TextFor("en",line.localizationKey,"angreifen","Technik","schnell","schwarz",15,"Der Hund wird schnell angreifen.")));
            var source=Localization.Source(line.localizationKey);
            if(source.Contains("{0}"))Assert.That(ja,Does.Contain("angreifen"));if(source.Contains("{5}"))Assert.That(ja,Does.Contain("Der Hund wird schnell angreifen."));
        }
    }
}

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class GachaContentTests
{
    private GachaItemData[] Items => JsonUtility.FromJson<GachaItemDatabase>(Resources.Load<TextAsset>("Data/gacha_items").text).items;
    [Test] public void AllThirtyGermanLettersAreUniquePositiveAndNormalizedWithOriginalRarityWeights()
    {
        var items = Items; var table = new GachaTable(items);
        var letters = items.Where(i => i.itemType == "letter").ToArray();
        Assert.That(letters.Length, Is.EqualTo(30));
        Assert.That(items.Select(i => i.id).Distinct().Count(), Is.EqualTo(items.Length));
        Assert.That(letters.Select(i => i.displayName), Is.EquivalentTo("abcdefghijklmnopqrstuvwxyzäöüß".Select(c => c.ToString())));
        foreach (var letter in letters) Assert.That(table.Probability(letter.id), Is.EqualTo(.02).Within(1e-12), letter.id);
        Assert.That(items.Sum(i => table.Probability(i.id)), Is.EqualTo(1).Within(1e-12));
        Assert.That(table.Probability("chunk_sch"), Is.EqualTo(.05).Within(1e-12));
        Assert.That(table.Probability("chunk_ss"), Is.EqualTo(.05).Within(1e-12));
        Assert.That(table.Probability("chunk_er"), Is.EqualTo(.05).Within(1e-12));
        Assert.That(table.Probability("word_wasser"), Is.EqualTo(.05).Within(1e-12));
        Assert.That(table.Probability("word_pistole"), Is.EqualTo(.05).Within(1e-12));
        Assert.That(table.Probability("suffix_ung"), Is.EqualTo(.12).Within(1e-12));
        Assert.That(table.Probability("word_nacht"), Is.EqualTo(.03).Within(1e-12));
    }
    [Test] public void EveryLetterCanBeDrawnWithExplicitRollAndRarityBoundariesRemainUnchanged()
    {
        var table = new GachaTable(Items);
        var draws = Enumerable.Range(0, table.CandidateCount(0)).Select(i => table.Draw(0,i)).ToArray();
        Assert.That(draws.Select(i => i.displayName), Is.EquivalentTo("abcdefghijklmnopqrstuvwxyzäöüß".Select(c => c.ToString())));
        foreach (int roll in new[] { 0,59,60,84,85,96,97,99 })
        {
            string rarity = roll < 60 ? "N" : roll < 85 ? "R" : roll < 97 ? "SR" : "SSR";
            Assert.That(table.Draw(roll,0).rarity, Is.EqualTo(rarity));
            Assert.That(table.Draw(roll,table.CandidateCount(roll)-1).rarity, Is.EqualTo(rarity));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => table.Draw(100,0));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.Draw(0,30));
    }
    [Test] public void MissingRarityStillUsesOriginalFallbackAndTotalProbabilityIsOne()
    {
        var table = new GachaTable(new[] { new GachaItemData { id="only", rarity="N" } });
        Assert.That(table.Draw(99,0).id, Is.EqualTo("only"));
        Assert.That(table.Probability("only"), Is.EqualTo(1).Within(1e-12));
    }
    [Test] public void AllNewLettersRoundTripVersionTwoWithoutLosingExistingCountsOrIndividualEquipment()
    {
        GameDatabase.EnsureAvailable();
        string directory = Path.Combine(Path.GetTempPath(),"LanguageBattleGachaTests",Guid.NewGuid().ToString("N"));
        try
        {
            var save = NewGameDefaults.Create();
            foreach (var item in Items.Where(i => i.itemType == "letter")) save.inventory.items.Add(new InventoryEntry(item.id,item.id == "letter_a" ? 54 : 3));
            string id = save.characterInstances.instances[0].instanceId;
            var gear = CharacterLoadoutStore.GetDefaultLoadout("nacht",id); gear.adjectiveId = "schwarz";
            save.characterEquipment.characters.Add(gear);
            var store = new SaveFileStore(directory); Assert.That(store.Save(save), Is.True,store.LastError);
            Assert.That(new SaveFileStore(directory).Load(out var restored), Is.EqualTo(SaveLoadStatus.Loaded));
            Assert.That(restored.version, Is.EqualTo(5)); Assert.That(restored.inventory.items.Count, Is.EqualTo(30));
            Assert.That(restored.inventory.items.Single(i => i.itemId == "letter_a").count, Is.EqualTo(54));
            Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(JsonUtility.ToJson(save)));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); Object.DestroyImmediate(GameDatabase.Instance.gameObject); }
    }
    [Test] public void LabelsUseGermanCharactersAndMetadataSupportsExistingChunksAndUnknownFallback()
    {
        foreach (var item in Items) Assert.That(ItemDisplayNames.Get(item.id), Is.EqualTo(item.displayName));
        Assert.That(ItemDisplayNames.Get("letter_ae_umlaut"), Is.EqualTo("ä"));
        Assert.That(ItemDisplayNames.Get("letter_oe_umlaut"), Is.EqualTo("ö"));
        Assert.That(ItemDisplayNames.Get("letter_ue_umlaut"), Is.EqualTo("ü"));
        Assert.That(ItemDisplayNames.Get("letter_eszett"), Is.EqualTo("ß"));
        Assert.That(ItemDisplayNames.Get("not-registered"), Is.EqualTo("not-registered"));
    }
}

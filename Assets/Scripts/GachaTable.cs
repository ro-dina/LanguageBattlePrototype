using System;
using System.Linq;

// Keep the original rarity weights and uniform selection within each rarity.
// Explicit rolls allow deterministic verification without sampling Unity's global random state.
public sealed class GachaTable
{
    private readonly GachaItemData[] items;
    public GachaTable(GachaItemData[] source)
    {
        items = source == null ? Array.Empty<GachaItemData>() :
            Array.FindAll(source, i => i != null && !string.IsNullOrWhiteSpace(i.id));
        if (items.Length == 0) throw new ArgumentException("No valid gacha items.");
    }
    private GachaItemData[] Candidates(int roll)
    {
        if (roll < 0 || roll >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
        string rarity = roll < 60 ? "N" : roll < 85 ? "R" : roll < 97 ? "SR" : "SSR";
        var candidates = Array.FindAll(items, i => i.rarity == rarity);
        return candidates.Length == 0 ? items : candidates;
    }
    public int CandidateCount(int rarityRoll) => Candidates(rarityRoll).Length;
    public GachaItemData Draw(int rarityRoll, int candidateIndex)
    {
        var candidates = Candidates(rarityRoll);
        if (candidateIndex < 0 || candidateIndex >= candidates.Length) throw new ArgumentOutOfRangeException(nameof(candidateIndex));
        return candidates[candidateIndex];
    }
    public double Probability(string itemId)
    {
        double result = 0;
        for (int roll = 0; roll < 100; roll++)
        {
            var candidates = Candidates(roll);
            result += .01 * candidates.Count(i => i.id == itemId) / candidates.Length;
        }
        return result;
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public enum BattleTense { Present, Future }
public enum BattleModal { None, Muessen }
public enum BattleElementKind { Adverb, Modal, Tense, Skill }
public enum BattleSelectionChange { Selected, Changed, Cleared }

// Event history records intent, including removed choices; it has no gameplay effect yet.
public sealed class BattleSelectionEvent
{
    public int Order { get; }
    public BattleElementKind Kind { get; }
    public string Id { get; }
    public string PreviousId { get; }
    public BattleSelectionChange Change { get; }
    internal BattleSelectionEvent(int order, BattleElementKind kind, string id, string previous)
    {
        Order = order; Kind = kind; Id = id; PreviousId = previous;
        Change = id == null ? BattleSelectionChange.Cleared : previous == null
            ? BattleSelectionChange.Selected : BattleSelectionChange.Changed;
    }
}

public sealed class BattleSelectionSnapshot
{
    public IReadOnlyDictionary<BattleElementKind, string> Elements { get; }
    public IReadOnlyList<BattleSelectionEvent> History { get; }
    internal BattleSelectionSnapshot(Dictionary<BattleElementKind, string> elements, List<BattleSelectionEvent> history)
    {
        Elements = new ReadOnlyDictionary<BattleElementKind, string>(new Dictionary<BattleElementKind, string>(elements));
        History = history.AsReadOnly();
    }
}

public sealed class BattleSelection
{
    private readonly Dictionary<BattleElementKind, string> elements = new Dictionary<BattleElementKind, string>();
    private readonly List<BattleSelectionEvent> history = new List<BattleSelectionEvent>();
    public BattleTense Tense => Get(BattleElementKind.Tense) == "futur1" ? BattleTense.Future : BattleTense.Present;
    public BattleModal Modal => Get(BattleElementKind.Modal) == "muessen" ? BattleModal.Muessen : BattleModal.None;
    public string Get(BattleElementKind kind) => elements.TryGetValue(kind, out var id) ? id : null;
    public void Select(BattleElementKind kind, string id)
    {
        if (string.IsNullOrEmpty(id)) id = null;
        string previous = Get(kind);
        if (previous == id) return;
        if (id == null) elements.Remove(kind); else elements[kind] = id;
        history.Add(new BattleSelectionEvent(history.Count + 1, kind, id, previous));
    }
    public void ClearModifiers()
    {
        Select(BattleElementKind.Adverb, null);
        Select(BattleElementKind.Modal, null);
        Select(BattleElementKind.Tense, null);
    }
    public BattleSelectionSnapshot Commit(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) throw new ArgumentException("A skill ID is required.");
        Select(BattleElementKind.Skill, skillId);
        return new BattleSelectionSnapshot(elements, new List<BattleSelectionEvent>(history));
    }
}

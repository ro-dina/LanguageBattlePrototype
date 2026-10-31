using System;

public enum CraftingElementKind { Material, CharacterInstance }
public sealed class CraftingElement
{
    public CraftingElementKind Kind { get; }
    public string Id { get; }
    private CraftingElement(CraftingElementKind kind,string id) { Kind=kind; Id=id; }
    public static CraftingElement Material(string itemId) => new CraftingElement(CraftingElementKind.Material,itemId);
    public static CraftingElement Character(string instanceId) => new CraftingElement(CraftingElementKind.CharacterInstance,instanceId);
}

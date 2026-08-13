[System.Serializable]
public class GachaItemData
{
    public string id;
    public string displayName;
    public string itemType;
    public string rarity;
    public string language;
}

[System.Serializable]
public class GachaItemDatabase
{
    public GachaItemData[] items;
}

using System;

[Serializable]
public class CraftingInputData
{
    public string itemId;
    public int count;
}

[Serializable]
public class CraftingRecipeData
{
    public string id;
    public CraftingInputData[] inputs;
    public string resultType;
    public string resultId;
    public int resultCount;
}

[Serializable]
public class CraftingRecipeDatabase
{
    public CraftingRecipeData[] recipes;
}

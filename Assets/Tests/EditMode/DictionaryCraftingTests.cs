using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
public class DictionaryCraftingTests
{
    private string directory;
    [SetUp] public void Setup()
    {
        if (PlayerSaveService.Instance != null) Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        GameDatabase.EnsureAvailable(); CharacterInstances.RestoreState(new CharacterInstanceState());
        CharacterCollection.RestoreState(new CharacterCollectionState()); CharacterLoadoutStore.RestoreState(new CharacterLoadoutState()); InventoryManager.RestoreState(new InventoryState());
        PlayerStateTransactions.TryChange(data => { data.wallet = new WalletState { balance = EconomySettings.Current.initialCoins }; return true; },out _);
        directory = Path.Combine(Path.GetTempPath(),"LanguageBattleDictionaryTests",Guid.NewGuid().ToString("N"));
    }
    [TearDown] public void Cleanup()
    {
        foreach (var i in CharacterInstances.GetAll()) CharacterInstances.EndBattle(i.instanceId);
        if (PlayerSaveService.Instance != null) Object.DestroyImmediate(PlayerSaveService.Instance.gameObject);
        if (GameDatabase.Instance != null) Object.DestroyImmediate(GameDatabase.Instance.gameObject);
        if (Directory.Exists(directory)) Directory.Delete(directory,true); else if (File.Exists(directory)) File.Delete(directory);
    }
    private string State() => JsonUtility.ToJson(PlayerStateTransactions.Capture());
    private CraftingElement[] Letters(string word)
    {
        var items=JsonUtility.FromJson<GachaItemDatabase>(Resources.Load<TextAsset>("Data/gacha_items").text).items;
        return word.Select(c => { string id=items.Single(i => i.itemType=="letter" && i.displayName==c.ToString()).id; InventoryManager.AddItem(id,1); return CraftingElement.Material(id); }).ToArray();
    }
    [TestCase("Nacht","nacht")][TestCase("wasser","wasser")][TestCase("Pistole","pistole")][TestCase("wasserpistole","wasserpistole")]
    public void DictionaryUsesDisplayedWordsCaseInsensitively(string word,string id) => Assert.That(GameDatabase.Instance.CharacterWords.Find(word)?.id,Is.EqualTo(id));
    [Test] public void GermanNormalizationPreservesDistinctLettersOrderAndAmbiguousHomographs()
    {
        var dictionary=GameDatabase.Instance.CharacterWords;
        Assert.That(dictionary.Find("Ma\u0308rz")?.id,Is.EqualTo("maerz"));
        Assert.That(dictionary.Find("Straße")?.id,Is.EqualTo("strasse"));
        foreach (string input in new[] {"strasse","Maerz","PistoleWasser","xyz-not-registered"}) Assert.That(dictionary.Find(input),Is.Null,input);
        var synthetic=new CharacterDictionary(new[] { new CharacterData {id="a",characterName="Über",language="de"}, new CharacterData {id="b",characterName="über",language="de"} });
        Assert.That(synthetic.Find("über"),Is.Null); Assert.That(synthetic.IsAmbiguous("Über","de"),Is.True);
        foreach (string word in new[] {"ä","ö","ü","ß"}) Assert.That(GermanWordMatching.Normalize(word),Is.EqualTo(word));
    }
    [Test] public void LettersOnlyCraftRepeatedRegisteredCharactersWithoutAnyRecipe()
    {
        foreach (int repeat in new[] {1,2})
        {
            var letters=Letters("feuer"); Assert.That(CraftingManager.Preview(letters).id,Is.EqualTo("feuer"));
            Assert.That(CraftingManager.TryCraft(letters,out var created,out _),Is.True);
            Assert.That(created.characterId,Is.EqualTo("feuer")); Assert.That(CharacterInstances.Count("feuer"),Is.EqualTo(repeat));
        }
        Assert.That(InventoryManager.GetAllItems(),Is.Empty); Assert.That(CharacterCollection.IsDiscovered("feuer"),Is.True);
    }
    [Test] public void LongOrderedChunkSequenceCanCraftWithoutFixedSlots()
    {
        string[] ids={"letter_w","letter_a","chunk_ss","chunk_er","letter_p","letter_i","letter_s","letter_t","letter_o","letter_l","letter_e"};
        foreach (string id in ids) InventoryManager.AddItem(id,1);
        var input=ids.Select(CraftingElement.Material).ToArray();
        Assert.That(CraftingManager.CombinedWord(input),Is.EqualTo("wasserpistole"));
        Assert.That(CraftingManager.TryCraft(input,out var created,out _),Is.True); Assert.That(created.characterId,Is.EqualTo("wasserpistole"));
    }
    [Test] public void ExplicitIndividualsConsumeOnlySelectedCopiesAndRemoveOnlyTheirLoadouts()
    {
        var water=CharacterInstances.Create("wasser"); var spare=CharacterInstances.Create("wasser"); var pistol=CharacterInstances.Create("pistole");
        var gear=CharacterLoadoutStore.GetLoadout(water.instanceId); gear.adjectiveId="schwarz"; CharacterLoadoutStore.Commit(gear);
        gear=CharacterLoadoutStore.GetLoadout(spare.instanceId); gear.adjectiveId="schnell"; CharacterLoadoutStore.Commit(gear);
        GameManager.SelectedCharacterInstanceId=water.instanceId;
        var inputs=new[] {CraftingElement.Character(water.instanceId),CraftingElement.Character(pistol.instanceId)};
        Assert.That(CraftingManager.CombinedWord(inputs),Is.EqualTo("WasserPistole"));
        int notifications=0; bool reentered=true; Action listener=()=> {notifications++; reentered=CraftingManager.TryCraft(inputs,out _,out _);};
        PlayerStateChanges.Changed+=listener;
        try { Assert.That(CraftingManager.TryCraft(inputs,out var created,out _),Is.True); Assert.That(CharacterLoadoutStore.GetLoadout(created.instanceId).adjectiveId,Is.Empty); }
        finally { PlayerStateChanges.Changed-=listener; }
        Assert.That(notifications,Is.EqualTo(1)); Assert.That(reentered,Is.False);
        Assert.That(CharacterInstances.Owns(water.instanceId),Is.False); Assert.That(CharacterInstances.Owns(pistol.instanceId),Is.False);
        Assert.That(CharacterLoadoutStore.GetLoadout(spare.instanceId).adjectiveId,Is.EqualTo("schnell"));
        Assert.That(CharacterLoadoutStore.CaptureState().characters.Single().instanceId,Is.EqualTo(spare.instanceId));
        Assert.That(CharacterInstances.Owns(GameManager.SelectedCharacterInstanceId),Is.True);
        Assert.That(CharacterCollection.IsDiscovered("pistole"),Is.True);
    }
    [Test] public void CharacterAndLetterMaterialsCanBeMixed()
    {
        var water=CharacterInstances.Create("wasser"); var suffix=Letters("pistole");
        var inputs=new[] {CraftingElement.Character(water.instanceId)}.Concat(suffix).ToArray();
        Assert.That(CraftingManager.TryCraft(inputs,out var created,out _),Is.True); Assert.That(created.characterId,Is.EqualTo("wasserpistole"));
    }
    [Test] public void InvalidDuplicateDeletedInsufficientAndBattleLockedInputsNeverChangeState()
    {
        var water=CharacterInstances.Create("wasser"); var pistol=CharacterInstances.Create("pistole");
        var valid=new[] {CraftingElement.Character(water.instanceId),CraftingElement.Character(pistol.instanceId)};
        var invalid=new[] {
            new[] {CraftingElement.Character(water.instanceId),CraftingElement.Character(water.instanceId)},
            new[] {CraftingElement.Character("deleted"),CraftingElement.Character(pistol.instanceId)},
            valid.Reverse().ToArray(),new[] {CraftingElement.Material("letter_n")},new[] {CraftingElement.Material("unknown")}};
        foreach (var input in invalid) { string before=State(); Assert.That(CraftingManager.TryCraft(input,out _,out _),Is.False); Assert.That(State(),Is.EqualTo(before)); }
        CharacterInstances.TryBeginBattle(water.instanceId); string locked=State();
        Assert.That(CraftingManager.CanCraft(valid),Is.False); Assert.That(CraftingManager.TryCraft(valid,out _,out _),Is.False); Assert.That(State(),Is.EqualTo(locked));
        CharacterInstances.EndBattle(water.instanceId); CharacterInstances.Remove(pistol.instanceId);
        string deleted=State(); Assert.That(CraftingManager.TryCraft(valid,out _,out _),Is.False); Assert.That(State(),Is.EqualTo(deleted));
    }
    [Test] public void SaveFailureLeavesMaterialsIndividualsEquipmentDiscoveryAndSelectionUntouched()
    {
        var service=PlayerSaveService.Initialize(directory); var water=CharacterInstances.Create("wasser"); var suffix=Letters("pistole");
        var inputs=new[] {CraftingElement.Character(water.instanceId)}.Concat(suffix).ToArray();
        GameManager.SelectedCharacterInstanceId=water.instanceId; string before=State();
        Directory.Delete(directory,true); File.WriteAllText(directory,"blocked");
        Assert.That(CraftingManager.TryCraft(inputs,out var output,out string error),Is.False); Assert.That(output,Is.Null); Assert.That(error,Does.Contain("failed"));
        Assert.That(State(),Is.EqualTo(before)); Assert.That(GameManager.SelectedCharacterInstanceId,Is.EqualTo(water.instanceId));
        Assert.That(File.ReadAllText(directory),Is.EqualTo("blocked"));
    }
    [Test] public void RewardApiConvertsWordsAndBatchDuplicatesWhileKeepingMaterialsAndProbabilities()
    {
        Assert.That(InventoryManager.AddItem("word_nacht",3),Is.True);
        Assert.That(RewardGrantService.Grant(new[] {new InventoryEntry("word_wasser",2),new InventoryEntry("word_pistole",2),new InventoryEntry("chunk_ss",3)},out _),Is.True);
        Assert.That(CharacterInstances.Count("nacht"),Is.EqualTo(3)); Assert.That(CharacterInstances.Count("wasser"),Is.EqualTo(2)); Assert.That(CharacterInstances.Count("pistole"),Is.EqualTo(2));
        Assert.That(InventoryManager.GetAllItems().Single().itemId,Is.EqualTo("chunk_ss"));
        Assert.That(CharacterInstances.GetAll().Select(i=>i.instanceId).Distinct().Count(),Is.EqualTo(7));
        Assert.That(InventoryManager.RestoreState(new InventoryState {items=new System.Collections.Generic.List<InventoryEntry>{new InventoryEntry("word_nacht",1)}}),Is.False);
        var saved=PlayerStateTransactions.Capture(); saved.inventory.items.Add(new InventoryEntry("word_nacht",1));
        Assert.That(SaveDataValidator.Validate(saved,out _),Is.False);
    }
}

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
public class SettingsLocalizationTests
{
    private string directory;
    [SetUp] public void Setup(){directory=Path.Combine(Path.GetTempPath(),"LanguageBattleSettingsEdit",Guid.NewGuid().ToString("N"));UserSettings.Initialize(directory,SystemLanguage.English);GameDatabase.EnsureAvailable();}
    [TearDown] public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);else if(File.Exists(directory))File.Delete(directory);}
    [TestCase(SystemLanguage.Japanese,"ja")][TestCase(SystemLanguage.English,"en")][TestCase(SystemLanguage.French,"en")]
    public void DefaultsUseJapaneseOrEnglishWithoutChangingLearningOrVoice(SystemLanguage language,string expected)
    {Assert.That(UserSettingsData.Defaults(language).uiLanguage,Is.EqualTo(expected));Assert.That(UserSettings.LearningLanguage,Is.EqualTo("de"));Assert.That(UserSettings.VoiceLanguage,Is.EqualTo("de"));}
    [Test] public void BothLanguagesParametersFallbackAndMissingKeysAreDefined()
    {
        Assert.That(Localization.TextFor("ja","ui.home"),Is.EqualTo("ホーム"));Assert.That(Localization.TextFor("en","ui.home"),Is.EqualTo("Home"));
        Assert.That(Localization.TextFor("fr","ui.home"),Is.EqualTo("Home"));Assert.That(Localization.TextFor("ja","ui.stage.first.reward",300),Is.EqualTo("初回クリア報酬：300 Coins"));
        Assert.That(Localization.FormatEntry(new TranslationEntry{key="test.fallback",en="English {0}",ja=""},"ja",42),Is.EqualTo("English 42"));
        Assert.That(Localization.Text("test.intentionally.missing"),Is.EqualTo("Unavailable"));Assert.That(Localization.MissingKeys,Does.Contain("test.intentionally.missing"));
    }
    [Test] public void TranslationTableRejectsDuplicateKeysMissingLanguagesAndDifferentParameters()
    {
        Assert.That(Localization.Validate(new TranslationTable{entries=Localization.All}),Is.Empty);
        var bad=new TranslationTable{entries=new[]{new TranslationEntry{key="same",en="A {0}",ja="A {1}"},new TranslationEntry{key="same",en="B",ja=""},new TranslationEntry{key="empty",en="",ja=""}}};
        Assert.That(Localization.Validate(bad).Length,Is.EqualTo(3));
    }
    [Test] public void SettingsPersistAndRemainSeparateFromProgressAndIds()
    {
        string before=JsonUtility.ToJson(PlayerStateTransactions.Capture());var data=UserSettings.Current;data.uiLanguage="ja";data.masterVolume=.43f;data.bgmVolume=.21f;data.sfxVolume=0;data.voiceVolume=.72f;
        Assert.That(UserSettings.TryUpdate(data,out _),Is.True);UserSettings.Initialize(directory,SystemLanguage.English);Assert.That(JsonUtility.ToJson(UserSettings.Current),Is.EqualTo(JsonUtility.ToJson(data)));
        Assert.That(JsonUtility.ToJson(PlayerStateTransactions.Capture()),Is.EqualTo(before));Assert.That(SaveDataValidator.CurrentVersion,Is.EqualTo(6));
    }
    [Test] public void MissingAndCorruptSettingsFallBackSafelyAndPreserveBytes()
    {
        var files=UserSettings.Store;Directory.CreateDirectory(directory);File.WriteAllText(files.MainPath,"broken");var data=files.Load(SystemLanguage.Japanese);Assert.That(data.uiLanguage,Is.EqualTo("ja"));
        Assert.That(File.ReadAllText(files.MainPath),Is.EqualTo("broken"));Assert.That(files.Save(data),Is.True);Assert.That(Directory.GetFiles(directory,"*.corrupt-*").Length,Is.EqualTo(1));Assert.That(File.ReadAllText(Directory.GetFiles(directory,"*.corrupt-*")[0]),Is.EqualTo("broken"));
    }
    [Test] public void ValidBackupRecoversCorruptPrimaryAndPendingWriteRecoversMissingPrimary()
    {
        var files=UserSettings.Store;var data=UserSettings.Current;data.uiLanguage="ja";Assert.That(files.Save(data),Is.True);data.uiLanguage="en";Assert.That(files.Save(data),Is.True);
        File.WriteAllText(files.MainPath,"broken");Assert.That(files.Load(SystemLanguage.English).uiLanguage,Is.EqualTo("ja"));File.Delete(files.MainPath);File.WriteAllText(files.TemporaryPath,JsonUtility.ToJson(data));Assert.That(files.Load(SystemLanguage.Japanese).uiLanguage,Is.EqualTo("en"));
    }
    [TestCase("")][TestCase(".bak")][TestCase(".tmp")]
    public void FutureSettingsAreProtectedInEveryRecoverySlot(string suffix)
    {Directory.CreateDirectory(directory);string path=UserSettings.Store.MainPath+suffix;File.WriteAllText(path,"{\"version\":2}");UserSettings.Initialize(directory,SystemLanguage.English);Assert.That(UserSettings.Store.WritesBlocked,Is.True);Assert.That(UserSettings.TryUpdate(UserSettings.Current,out _),Is.False);Assert.That(File.ReadAllText(path),Is.EqualTo("{\"version\":2}"));}
    [Test] public void InvalidSettingsValuesAndMissingFieldsAreRejected()
    {
        foreach(var volume in new[]{-1f,1.01f,float.NaN,float.PositiveInfinity}){var s=new UserSettingsData{masterVolume=volume};Assert.That(UserSettingsStore.Valid(s),Is.False);}
        Assert.That(UserSettingsStore.Decode("{\"version\":1,\"uiLanguage\":\"ja\"}",out _),Is.False);Assert.That(UserSettingsStore.Valid(new UserSettingsData{uiLanguage="de"}),Is.False);
    }
    [Test] public void SaveFailureDoesNotPublishOrChangeValues()
    {
        File.WriteAllText(directory,"blocked");int events=0;Action listener=()=>events++;UserSettings.Changed+=listener;
        try{var data=UserSettings.Current;data.uiLanguage="ja";Assert.That(UserSettings.TryUpdate(data,out _),Is.False);Assert.That(UserSettings.Language,Is.EqualTo("en"));Assert.That(events,Is.Zero);}finally{UserSettings.Changed-=listener;}
    }
    [Test] public void JapaneseFontContainsEveryTranslationAndGermanCharacters()
    {
        var font=Resources.Load<TMP_FontAsset>("Fonts/NotoSansCJKjp UI");Assert.That(font,Is.Not.Null);
        foreach(char c in string.Concat(Localization.All.Select(e=>e.ja))+"äöüÄÖÜß")if(!char.IsControl(c))Assert.That(font.HasCharacter(c,true,true),Is.True,"Missing glyph U+"+((int)c).ToString("X4"));
    }
    [Test] public void SwitchingUiLanguageNeverMutatesGermanMastersOrSentenceGeneration()
    {
        var db=GameDatabase.EnsureAvailable();string original=string.Join("|",db.GetAllCharacters().Select(c=>JsonUtility.ToJson(c)));
        var context=new SentenceContext{Subject=db.GetCharacter("nacht"),Target=db.GetCharacter("feuer"),Skill=db.GetSkill("sein")};string sentence=SentenceGenerator.Generate(context).Text;
        var data=UserSettings.Current;data.uiLanguage="ja";UserSettings.TryUpdate(data,out _);Assert.That(string.Join("|",db.GetAllCharacters().Select(c=>JsonUtility.ToJson(c))),Is.EqualTo(original));Assert.That(SentenceGenerator.Generate(context).Text,Is.EqualTo(sentence));Assert.That(Localization.Present("Nacht\nAdjective: schnell\nHP: 25"),Is.EqualTo("Nacht\n形容詞： schnell\nHP: 25"));
    }
    [Test] public void EachVolumeChannelUsesMasterAndItsOwnGainAndZeroMutes()
    {
        var data=UserSettings.Current;data.masterVolume=.5f;data.bgmVolume=.2f;data.sfxVolume=.4f;data.voiceVolume=.8f;Assert.That(UserSettings.TryUpdate(data,out _),Is.True);
        Assert.That(GameAudio.Gain(GameAudioChannel.Bgm),Is.EqualTo(.1f).Within(.001));Assert.That(GameAudio.Gain(GameAudioChannel.Sfx),Is.EqualTo(.2f).Within(.001));Assert.That(GameAudio.Gain(GameAudioChannel.Voice),Is.EqualTo(.4f).Within(.001));
        data.masterVolume=0;UserSettings.TryUpdate(data,out _);foreach(GameAudioChannel bus in Enum.GetValues(typeof(GameAudioChannel)))Assert.That(GameAudio.Gain(bus),Is.Zero);
        Assert.That(GameAudio.CharacterClip("missing"),Is.Null);Assert.That(GameAudio.CharacterClip("feuer"),Is.Not.Null);
    }
}

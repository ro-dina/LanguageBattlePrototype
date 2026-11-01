using System.Collections;
using UnityEngine;

public enum GameAudioChannel { Bgm, Sfx, Voice }

// Separate child sources prevent overlapping SFX and pronunciation from changing each other's gain.
// A channel never changes AudioListener.volume, so other apps/scenes cannot reset master volume.
public static class GameAudio
{
    public static float Gain(GameAudioChannel channel)
    {
        var s = UserSettings.Current;
        return s.masterVolume * (channel == GameAudioChannel.Bgm ? s.bgmVolume : channel == GameAudioChannel.Sfx ? s.sfxVolume : s.voiceVolume);
    }
    public static void Route(AudioSource source, GameAudioChannel channel)
    {
        if (source == null) return;
        var route = source.GetComponent<AudioChannelVolume>() ?? source.gameObject.AddComponent<AudioChannelVolume>();
        route.Bind(source, channel);
    }
    public static void PlayOneShot(AudioSource source, AudioClip clip, GameAudioChannel channel)
    {
        if (source == null || clip == null) return;
        string childName = "AudioChannel_" + channel;
        var child = source.transform.Find(childName);
        AudioSource routed;
        if (child == null)
        {
            var go = new GameObject(childName); go.transform.SetParent(source.transform, false); routed = go.AddComponent<AudioSource>();
            routed.playOnAwake = false; routed.volume = source.volume; routed.spatialBlend = source.spatialBlend;
            routed.outputAudioMixerGroup = source.outputAudioMixerGroup; Route(routed, channel);
        }
        else routed = child.GetComponent<AudioSource>();
        routed.PlayOneShot(clip);
    }
    public static AudioClip CharacterClip(string characterId)
    {
        var db = GameDatabase.EnsureAvailable();
        if (!db.ContainsCharacter(characterId)) return null;
        return Resources.Load<AudioClip>("Audio/Words/" + SentenceGenerator.AudioKey(db.GetCharacter(characterId).characterName));
    }
}

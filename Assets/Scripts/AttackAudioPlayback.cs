using System.Collections;
using UnityEngine;

public static class AttackAudioPlayback
{
    public static IEnumerator Play(AudioSource source, BattleSentence sentence)
    {
        if (source == null || sentence == null) yield break;
        AudioClip clip = Resources.Load<AudioClip>("Audio/Sentences/" + sentence.SentenceAudioKey);
        if (clip != null)
        {
            GameAudio.PlayOneShot(source, clip, GameAudioChannel.Voice);
            yield return new WaitForSeconds(clip.length + 0.05f);
            yield break;
        }
        Debug.LogWarning($"Sentence audio not found: Audio/Sentences/{sentence.SentenceAudioKey}. Falling back to word audio.");
        foreach (string word in sentence.SpeechWords)
        {
            string key = SentenceGenerator.AudioKey(word);
            clip = Resources.Load<AudioClip>("Audio/Words/" + key);
            if (clip == null)
            {
                Debug.LogWarning($"Word audio not found: Audio/Words/{key}");
                yield return new WaitForSeconds(0.15f);
            }
            else
            {
                GameAudio.PlayOneShot(source, clip, GameAudioChannel.Voice);
                yield return new WaitForSeconds(clip.length + 0.05f);
            }
        }
    }
}

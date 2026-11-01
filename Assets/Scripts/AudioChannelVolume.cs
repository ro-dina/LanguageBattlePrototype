using UnityEngine;

// Reusable for future music/effect sources, with authored base volume preserved.
[DisallowMultipleComponent]
public sealed class AudioChannelVolume : MonoBehaviour
{
    [SerializeField] private GameAudioChannel channel;
    private AudioSource source;
    private float baseVolume = 1;
    public void Bind(AudioSource value, GameAudioChannel bus)
    { if (source == null) { source = value; baseVolume = source.volume; } channel = bus; Apply(); }
    private void Awake() { source = GetComponent<AudioSource>(); if (source != null) baseVolume = source.volume; }
    private void OnEnable() { UserSettings.Changed += Apply; Apply(); }
    private void OnDisable() => UserSettings.Changed -= Apply;
    public void Apply() { if (source != null) source.volume = baseVolume * GameAudio.Gain(channel); }
}

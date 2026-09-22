using UnityEngine;

/// <summary>
/// Binds an AudioSource's volume to <see cref="GameAudioSettings.MusicVolume"/>, preserving
/// whatever volume it was authored with (e.g. the Soundtrack source's mix-level 0.02) as a base
/// and scaling it live as the Music slider changes.
/// </summary>
[DisallowMultipleComponent]
public class MusicVolumeApplier : MonoBehaviour
{
    [SerializeField] private AudioSource source;

    private float _baseVolume = 1f;

    private void Awake()
    {
        if (!source) source = GetComponent<AudioSource>();
        if (source != null) _baseVolume = source.volume;
    }

    private void OnEnable()
    {
        GameAudioSettings.MusicVolumeChanged += Apply;
        Apply(GameAudioSettings.MusicVolume);
    }

    private void OnDisable() => GameAudioSettings.MusicVolumeChanged -= Apply;

    private void Apply(float v)
    {
        if (source != null) source.volume = _baseVolume * v;
    }
}

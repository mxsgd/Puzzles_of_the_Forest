using UnityEngine;

/// <summary>
/// Binds an AudioSource's volume to <see cref="GameAudioSettings.SfxVolume"/>, preserving whatever
/// volume it was authored/instantiated with as a base and scaling it live as the SFX slider
/// changes. AudioSource.PlayOneShot already multiplies by AudioSource.volume, so every existing
/// PlayOneShot call site (chain SFX, placement SFX, spawn pop, score flyout arrival) picks this up
/// automatically — no changes needed at the call sites themselves.
/// </summary>
[DisallowMultipleComponent]
public class SfxVolumeApplier : MonoBehaviour
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
        GameAudioSettings.SfxVolumeChanged += Apply;
        Apply(GameAudioSettings.SfxVolume);
    }

    private void OnDisable() => GameAudioSettings.SfxVolumeChanged -= Apply;

    private void Apply(float v)
    {
        if (source != null) source.volume = _baseVolume * v;
    }
}

using System;
using UnityEngine;

/// <summary>
/// Persisted, independent Music/SFX volume scalars (0..1).
///
/// Previously PauseMenuController drove a single shared <c>AudioListener.volume = Max(music, sfx)</c>.
/// Since AudioListener.volume is one global multiplier applied to every AudioSource in the scene,
/// dragging just the Music slider to 0 did nothing as long as SFX stayed up (Max(0, sfx) = sfx) —
/// the only way to actually mute music was to also drag SFX down, which muted SFX too. This class
/// replaces that: each channel is read directly by the AudioSource(s) that belong to it
/// (see <see cref="SfxVolumeApplier"/> / the music applier attached to the Soundtrack source), so
/// the two sliders no longer interact with each other at all.
/// </summary>
public static class GameAudioSettings
{
    private const string PrefMusic = "idle_forest.music_volume";
    private const string PrefSfx   = "idle_forest.sfx_volume";

    private static float _music = -1f;
    private static float _sfx   = -1f;

    public static event Action<float> MusicVolumeChanged;
    public static event Action<float> SfxVolumeChanged;

    public static float MusicVolume
    {
        get { EnsureLoaded(); return _music; }
        set
        {
            EnsureLoaded();
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(clamped, _music)) return;
            _music = clamped;
            PlayerPrefs.SetFloat(PrefMusic, _music);
            MusicVolumeChanged?.Invoke(_music);
        }
    }

    public static float SfxVolume
    {
        get { EnsureLoaded(); return _sfx; }
        set
        {
            EnsureLoaded();
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(clamped, _sfx)) return;
            _sfx = clamped;
            PlayerPrefs.SetFloat(PrefSfx, _sfx);
            SfxVolumeChanged?.Invoke(_sfx);
        }
    }

    private static void EnsureLoaded()
    {
        if (_music >= 0f && _sfx >= 0f) return;
        _music = PlayerPrefs.GetFloat(PrefMusic, 1f);
        _sfx   = PlayerPrefs.GetFloat(PrefSfx,   1f);
    }
}

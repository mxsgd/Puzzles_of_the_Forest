using System;
using UnityEngine;

/// <summary>
/// Odtwarza per-biom SFX przy każdym postawieniu kafla.
/// Subskrybuje TileEvents.TilePlaced. Umieść jako komponent gdziekolwiek w scenie.
/// </summary>
[DisallowMultipleComponent]
public class TilePlacementSfx : MonoBehaviour
{
    [Serializable]
    public struct BiomeSfxEntry
    {
        public TileBiome biome;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Per-biom klipy")]
    [SerializeField] private BiomeSfxEntry[] biomeClips = Array.Empty<BiomeSfxEntry>();

    [Header("Pitch randomizacja")]
    [SerializeField] private bool randomizePitch = true;
    [SerializeField] private Vector2 pitchRange = new(0.95f, 1.05f);

    private BiomeSfxEntry[] _lookup;

    private void Awake()
    {
        if (!audioSource)
            audioSource = GetComponent<AudioSource>();
        if (!audioSource)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        RebuildLookup();
    }

    private void OnEnable()  => TileEvents.TilePlaced += OnTilePlaced;
    private void OnDisable() => TileEvents.TilePlaced -= OnTilePlaced;

    private void OnTilePlaced(TileGrid.Tile tile, TileBiome biome)
    {
        if (audioSource == null || biome == TileBiome.None)
            return;

        for (int i = 0; i < biomeClips.Length; i++)
        {
            ref var entry = ref biomeClips[i];
            if (entry.biome != biome || entry.clip == null)
                continue;

            float vol = entry.volume > 0f ? entry.volume : 1f;

            if (randomizePitch)
                audioSource.pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
            else
                audioSource.pitch = 1f;

            audioSource.PlayOneShot(entry.clip, vol);
            return;
        }
    }

    private void RebuildLookup()
    {
        _lookup = biomeClips ?? Array.Empty<BiomeSfxEntry>();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }
}

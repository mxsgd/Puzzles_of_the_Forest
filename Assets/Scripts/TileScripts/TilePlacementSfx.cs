using System;
using UnityEngine;

/// <summary>
/// Odtwarza per-biom SFX przy każdym postawieniu kafla.
/// Subskrybuje TileEvents.TilePlaced.
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

    [Header("Katalog (fallback gdy biomeClips puste)")]
    [SerializeField] private GameSfxCatalog sfxCatalog;

    [Header("Per-biom klipy")]
    [SerializeField] private BiomeSfxEntry[] biomeClips = Array.Empty<BiomeSfxEntry>();

    [Header("Pitch randomizacja")]
    [SerializeField] private bool randomizePitch = true;
    [SerializeField] private Vector2 pitchRange = new(0.95f, 1.05f);

    private void Awake()
    {
        if (!audioSource)
            audioSource = GetComponent<AudioSource>();
        if (!audioSource)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;

        if (sfxCatalog == null)
            sfxCatalog = GameSfxCatalog.Default;

        if ((biomeClips == null || biomeClips.Length == 0) && sfxCatalog != null)
            biomeClips = BuildDefaultBiomeClips(sfxCatalog);
    }

    private void OnEnable()  => TileEvents.TilePlaced += OnTilePlaced;
    private void OnDisable() => TileEvents.TilePlaced -= OnTilePlaced;

    private void OnTilePlaced(TileGrid.Tile tile, TileBiome biome)
    {
        if (audioSource == null || biome == TileBiome.None)
            return;

        if (!TryGetClip(biome, out var clip, out float vol))
            return;

        if (randomizePitch)
            audioSource.pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
        else
            audioSource.pitch = 1f;

        audioSource.PlayOneShot(clip, vol);
    }

    private bool TryGetClip(TileBiome biome, out AudioClip clip, out float volume)
    {
        clip = null;
        volume = 1f;

        if (biomeClips != null)
        {
            for (int i = 0; i < biomeClips.Length; i++)
            {
                ref var entry = ref biomeClips[i];
                if (entry.biome != biome || entry.clip == null)
                    continue;

                clip = entry.clip;
                volume = entry.volume > 0f ? entry.volume : 1f;
                return true;
            }
        }

        if (sfxCatalog == null)
            return false;

        clip = sfxCatalog.GetPlacementClip(biome);
        volume = sfxCatalog.placementVolume;
        return clip != null;
    }

    public static BiomeSfxEntry[] BuildDefaultBiomeClips(GameSfxCatalog catalog)
    {
        if (catalog == null || catalog.leavesClip == null)
            return Array.Empty<BiomeSfxEntry>();

        float vol = catalog.placementVolume > 0f ? catalog.placementVolume : 1f;
        return new[]
        {
            new BiomeSfxEntry { biome = TileBiome.Forested, clip = catalog.leavesClip, volume = vol },
            new BiomeSfxEntry { biome = TileBiome.Meadow,   clip = catalog.leavesClip, volume = vol },
            new BiomeSfxEntry { biome = TileBiome.Bushy,    clip = catalog.leavesClip, volume = vol },
            new BiomeSfxEntry { biome = TileBiome.Rocks,    clip = catalog.rocksClip,  volume = vol },
            new BiomeSfxEntry { biome = TileBiome.Water,    clip = catalog.waterClip,  volume = vol },
        };
    }
}

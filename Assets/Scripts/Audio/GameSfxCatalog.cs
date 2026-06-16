using UnityEngine;

/// <summary>
/// Centralny katalog klipów SFX — przypisz w Inspectorze lub użyj domyślnego assetu.
/// </summary>
[CreateAssetMenu(fileName = "GameSfxCatalog", menuName = "Idle Forest/Game Sfx Catalog")]
public class GameSfxCatalog : ScriptableObject
{
    private static GameSfxCatalog _default;

    [Header("Tile placement — per biom")]
    public AudioClip leavesClip;
    public AudioClip rocksClip;
    public AudioClip waterClip;

    [Header("Habitat chain")]
    public AudioClip chainTileClip;

    [Header("Animal spawn")]
    public AudioClip popClip;

    [Range(0f, 1f)] public float placementVolume = 1f;
    [Range(0f, 1f)] public float chainVolume = 0.75f;
    [Range(0f, 1f)] public float popVolume = 0.9f;

    public static GameSfxCatalog Default
    {
        get
        {
            if (_default != null)
                return _default;

            _default = Resources.Load<GameSfxCatalog>("GameSfxCatalog");
            return _default;
        }
    }

    public AudioClip GetPlacementClip(TileBiome biome)
    {
        return biome switch
        {
            TileBiome.Forested => leavesClip,
            TileBiome.Meadow => leavesClip,
            TileBiome.Bushy => leavesClip,
            TileBiome.Rocks => rocksClip,
            TileBiome.Water => waterClip,
            _ => null
        };
    }
}

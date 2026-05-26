using Tile = TileGrid.Tile;

/// <summary>
/// Lightweight value describing an effect a perk wants to produce.
/// PerkEffectExecutor interprets these through the proper domain owners
/// (TileDeck, GameUI, TilePlacementService) instead of perks calling them directly.
/// </summary>
public readonly struct PerkCommand
{
    public readonly PerkCommandKind Kind;
    public readonly int IntValue;
    public readonly TileBiome BiomeValue;
    public readonly Tile TargetTile;

    private PerkCommand(PerkCommandKind kind, int intValue = 0,
        TileBiome biome = TileBiome.None, Tile tile = null)
    {
        Kind = kind;
        IntValue = intValue;
        BiomeValue = biome;
        TargetTile = tile;
    }

    public static PerkCommand AddDeckTiles(int count)
        => new(PerkCommandKind.AddDeckTiles, intValue: count);

    public static PerkCommand AddRerolls(int count)
        => new(PerkCommandKind.AddRerolls, intValue: count);

    public static PerkCommand GrantFreeRerollCharges(int count)
        => new(PerkCommandKind.GrantFreeRerollCharges, intValue: count);

    public static PerkCommand SpawnTile(Tile target, TileBiome biome)
        => new(PerkCommandKind.SpawnTile, biome: biome, tile: target);

    public static PerkCommand TransformTileBiome(Tile target, TileBiome newBiome)
        => new(PerkCommandKind.TransformTileBiome, biome: newBiome, tile: target);
}

public enum PerkCommandKind
{
    None = 0,
    AddDeckTiles,
    AddRerolls,
    GrantFreeRerollCharges,
    SpawnTile,
    TransformTileBiome
}

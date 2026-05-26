using System.Collections.Generic;
using Tile = TileGrid.Tile;

/// <summary>
/// Categories of perk hooks. A perk definition declares which hooks it implements
/// so PerkManager can dispatch only to relevant perks.
/// </summary>
public enum PerkHook
{
    None = 0,
    SessionStart,
    HabitatAssigned,
    RerollCost,
    TilePlaced,
    HabitatEvaluation
}

/// <summary>
/// Read-only snapshot of a habitat creation event, enriched with biome diversity data
/// that raw HabitatAssignmentData does not carry.
/// </summary>
public readonly struct HabitatContext
{
    public readonly int HabitatId;
    public readonly HabitatAnimal Animal;
    public readonly IReadOnlyList<Tile> Tiles;
    public readonly int TileCount;
    public readonly int PointsAwarded;
    public readonly float ScoreValue;
    public readonly int DistinctBiomeCount;
    public readonly Tile SourcePlacedTile;
    public readonly TileRuntimeStore RuntimeStore;

    public HabitatContext(HabitatAssignmentData data, int distinctBiomes,
        Tile sourceTile, TileRuntimeStore runtimeStore)
    {
        HabitatId = data.HabitatId;
        Animal = data.Animal;
        Tiles = data.Tiles;
        TileCount = data.TileCount;
        PointsAwarded = data.PointsAwarded;
        ScoreValue = data.ScoreValue;
        DistinctBiomeCount = distinctBiomes;
        SourcePlacedTile = sourceTile;
        RuntimeStore = runtimeStore;
    }
}

/// <summary>
/// Read-only snapshot of a just-placed tile and its neighborhood.
/// Built once after placement commit, consumed by world-effect perks.
/// </summary>
public readonly struct PlacementContext
{
    public readonly Tile PlacedTile;
    public readonly TileDraw Draw;
    public readonly IReadOnlyList<Tile> Neighbors;
    public readonly TileRuntimeStore RuntimeStore;

    public PlacementContext(Tile placedTile, TileDraw draw,
        IReadOnlyList<Tile> neighbors, TileRuntimeStore runtimeStore)
    {
        PlacedTile = placedTile;
        Draw = draw;
        Neighbors = neighbors;
        RuntimeStore = runtimeStore;
    }
}

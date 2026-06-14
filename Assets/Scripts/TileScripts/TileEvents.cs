using System;
using System.Collections.Generic;
using Tile = TileGrid.Tile;

/// <summary>Payload when a new habitat is successfully registered.</summary>
public readonly struct HabitatAssignmentData
{
    public readonly int HabitatId;
    public readonly HabitatAnimal Animal;
    public readonly IReadOnlyList<Tile> Tiles;
    public readonly Tile PrimaryCoreTile;
    public readonly int PointsAwarded;
    public readonly float ScoreValue;
    public readonly int TileCount;

    public HabitatAssignmentData(
        int habitatId,
        HabitatAnimal animal,
        IReadOnlyList<Tile> tiles,
        int pointsAwarded,
        float scoreValue,
        int tileCount,
        Tile primaryCoreTile = null)
    {
        HabitatId = habitatId;
        Animal = animal;
        Tiles = tiles;
        PrimaryCoreTile = primaryCoreTile;
        PointsAwarded = pointsAwarded;
        ScoreValue = scoreValue;
        TileCount = tileCount;
    }
}

public static class TileEvents
{
    public static event Action<Tile> TileStateChanged;

    /// <summary>Emitted when a new habitat is created after classification.</summary>
    public static event Action<HabitatAssignmentData> HabitatAssigned;

    public static void RaiseTileStateChanged(Tile tile)
        => TileStateChanged?.Invoke(tile);

    public static void RaiseHabitatAssigned(HabitatAssignmentData data)
        => HabitatAssigned?.Invoke(data);
}

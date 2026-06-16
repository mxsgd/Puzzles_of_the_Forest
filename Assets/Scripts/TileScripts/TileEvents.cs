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

public readonly struct HabitatMergeData
{
    public readonly int SurvivorHabitatId;
    public readonly HabitatAnimal Animal;
    public readonly IReadOnlyList<Tile> Tiles;
    public readonly IReadOnlyList<int> AbsorbedHabitatIds;
    public readonly int MergedHabitatCount;
    public readonly int BasePointsAwarded;
    public readonly int ConnectionBonusPoints;
    public readonly Tile PrimaryCoreTile;

    public int TotalPointsAwarded => BasePointsAwarded + ConnectionBonusPoints;
    public int TileCount => Tiles?.Count ?? 0;

    public HabitatMergeData(
        int survivorHabitatId,
        HabitatAnimal animal,
        IReadOnlyList<Tile> tiles,
        IReadOnlyList<int> absorbedHabitatIds,
        int mergedHabitatCount,
        int basePointsAwarded,
        int connectionBonusPoints,
        Tile primaryCoreTile = null)
    {
        SurvivorHabitatId = survivorHabitatId;
        Animal = animal;
        Tiles = tiles;
        AbsorbedHabitatIds = absorbedHabitatIds;
        MergedHabitatCount = mergedHabitatCount;
        BasePointsAwarded = basePointsAwarded;
        ConnectionBonusPoints = connectionBonusPoints;
        PrimaryCoreTile = primaryCoreTile;
    }
}

public static class TileEvents
{
    public static event Action<Tile> TileStateChanged;

    /// <summary>Kafel postawiony — biom znany w momencie eventu.</summary>
    public static event Action<Tile, TileBiome> TilePlaced;

    /// <summary>Emitted when a new habitat is created after classification.</summary>
    public static event Action<HabitatAssignmentData> HabitatAssigned;

    /// <summary>Emitted when two or more adjacent habitats of the same animal merge into one.</summary>
    public static event Action<HabitatMergeData> HabitatMerged;

    /// <summary>Emitted when the chain-reaction animation for a habitat finishes.</summary>
    public static event Action<int> HabitatChainCompleted;

    /// <summary>Chain reaction finished and habitat presentation (e.g. animal spawn) is done.</summary>
    public static event Action<int> HabitatPresentationCompleted;

    public static void RaiseTileStateChanged(Tile tile)
        => TileStateChanged?.Invoke(tile);

    public static void RaiseTilePlaced(Tile tile, TileBiome biome)
        => TilePlaced?.Invoke(tile, biome);

    public static void RaiseHabitatAssigned(HabitatAssignmentData data)
        => HabitatAssigned?.Invoke(data);

    public static void RaiseHabitatMerged(HabitatMergeData data)
        => HabitatMerged?.Invoke(data);

    public static void RaiseHabitatChainCompleted(int habitatId)
        => HabitatChainCompleted?.Invoke(habitatId);

    public static void RaiseHabitatPresentationCompleted(int habitatId)
        => HabitatPresentationCompleted?.Invoke(habitatId);
}

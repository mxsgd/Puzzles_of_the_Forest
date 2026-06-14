using System;
using System.Collections.Generic;

/// <summary>
/// Biom kafla — definiuje jakie obiekty (drzewa, krzaki, kwiaty, kamienie...)
/// mogą się pojawić w jego 12 trójkątach.
/// </summary>
public enum TileBiome
{
    None = 0,
    Forested,    // Lesisty: drzewa + krzaki
    Meadow,      // Łąkowy:     kwiaty + trawa
    Rocks,       // Skalny:     drzewa + kamienie
    Bushy,       // Krzaczasty: krzaki + kwiaty
    Water        // Wodny (dedykowany prefab kafla z osobnym shaderem)
}

/// <summary>
/// Stałe tagi treści w slotach trójkątów. Używane przez populator i debug visualizer.
/// </summary>
public static class TileContentTags
{
    public const string Tree   = "Tree";
    public const string Bush   = "Bush";
    public const string Flower = "Flower";
    public const string Grass  = "Grass";
    public const string Rock   = "Rock";
}

/// <summary>
/// Reguły biomów: które tagi treści mogą wystąpić w danym biomie.
/// </summary>
public static class TileBiomeRules
{
    private static readonly Dictionary<TileBiome, string[]> AllowedContent = new()
    {
        { TileBiome.Forested,    new[] { TileContentTags.Tree,   TileContentTags.Bush   } },
        { TileBiome.Meadow,      new[] { TileContentTags.Flower, TileContentTags.Grass  } },
        { TileBiome.Rocks,       new[] { TileContentTags.Tree,   TileContentTags.Rock   } },
        { TileBiome.Bushy,       new[] { TileContentTags.Bush,   TileContentTags.Flower } },
        { TileBiome.Water,       Array.Empty<string>() }
    };

    public static IReadOnlyList<string> GetAllowedTags(TileBiome biome)
    {
        return AllowedContent.TryGetValue(biome, out var tags) ? tags : Array.Empty<string>();
    }

    public static string GetDisplayName(TileBiome biome) => biome switch
    {
        TileBiome.Forested    => "Forested",
        TileBiome.Meadow      => "Meadow",
        TileBiome.Rocks       => "Rocks",
        TileBiome.Bushy       => "Bushy",
        TileBiome.Water       => "Water",
        _                     => biome.ToString()
    };
}

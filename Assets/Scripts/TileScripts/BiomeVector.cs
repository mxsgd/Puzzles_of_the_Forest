using System;
using UnityEngine;

/// <summary>Habitat animal types for vector requirements and compatibility.</summary>
public enum HabitatAnimal
{
    None = 0,
    Deer,
    Beaver,
    Bear,
    Bees,
    RockDweller
}

/// <summary>
/// Biome counts for habitat classification: meadow, forest, bush, rock, water.
/// Each occupied tile contributes +1 along its biome axis when not filtered out.
/// </summary>
[Serializable]
public struct BiomeVector : IEquatable<BiomeVector>
{
    public int Meadow;
    public int Forest;
    public int Bush;
    public int Rock;
    public int Water;

    public static BiomeVector Zero => default;

    public static BiomeVector FromTileBiome(TileBiome biome)
    {
        return biome switch
        {
            TileBiome.Meadow => new BiomeVector { Meadow = 1 },
            TileBiome.Forested => new BiomeVector { Forest = 1 },
            TileBiome.Bushy => new BiomeVector { Bush = 1 },
            TileBiome.Rocks       => new BiomeVector { Rock = 1 },
            TileBiome.Water => new BiomeVector { Water = 1 },
            _ => Zero
        };
    }

    public void Add(in BiomeVector other)
    {
        Meadow += other.Meadow;
        Forest += other.Forest;
        Bush += other.Bush;
        Rock += other.Rock;
        Water += other.Water;
    }

    /// <summary>H[i] ≥ requirement[i] for all dimensions.</summary>
    public bool Satisfies(in BiomeVector requirement)
    {
        return Meadow >= requirement.Meadow
               && Forest >= requirement.Forest
               && Bush >= requirement.Bush
               && Rock >= requirement.Rock
               && Water >= requirement.Water;
    }

    /// <summary>Σ max(0, requirement[i] − H[i]) — np. 1 oznacza „brakuje jednej jednostki biomu».</summary>
    public int DeficitSumToward(in BiomeVector requirement)
    {
        return Mathf.Max(0, requirement.Meadow - Meadow)
               + Mathf.Max(0, requirement.Forest - Forest)
               + Mathf.Max(0, requirement.Bush - Bush)
               + Mathf.Max(0, requirement.Rock - Rock)
               + Mathf.Max(0, requirement.Water - Water);
    }

    public bool Equals(BiomeVector other)
    {
        return Meadow == other.Meadow && Forest == other.Forest && Bush == other.Bush
               && Rock == other.Rock && Water == other.Water;
    }

    public override bool Equals(object obj) => obj is BiomeVector other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Meadow;
            hash = hash * 397 ^ Forest;
            hash = hash * 397 ^ Bush;
            hash = hash * 397 ^ Rock;
            hash = hash * 397 ^ Water;
            return hash;
        }
    }

    public override string ToString()
        => $"({Meadow},{Forest},{Bush},{Rock},{Water})";
}

using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Mathematical habitat definitions: biome vectors R⁵ and base scoring.</summary>
public static class HabitatRequirements
{
    /// <summary>Requirement vectors (meadow, forest, bush, rock, water).</summary>
    public static BiomeVector GetRequirement(HabitatAnimal animal)
    {
        return animal switch
        {
            // Deer:   (2,1,1,0,1)
            HabitatAnimal.Deer => new BiomeVector
                { Meadow = 2, Forest = 1, Bush = 1, Rock = 0, Water = 1 },
            // Beaver: (0,2,1,0,2)
            HabitatAnimal.Beaver => new BiomeVector
                { Meadow = 0, Forest = 2, Bush = 1, Rock = 0, Water = 2 },
            // Bear:   (0,1,1,2,1)
            HabitatAnimal.Bear => new BiomeVector
                { Meadow = 0, Forest = 1, Bush = 1, Rock = 2, Water = 1 },
            // Bees:   (2,1,2,0,0)
            HabitatAnimal.Bees => new BiomeVector
                { Meadow = 2, Forest = 1, Bush = 2, Rock = 0, Water = 0 },
            // RockDweller: (1,0,0,3,1)
            HabitatAnimal.RockDweller => new BiomeVector
                { Meadow = 1, Forest = 0, Bush = 0, Rock = 3, Water = 1 },
            _ => BiomeVector.Zero
        };
    }

    /// <summary>Difficulty weights; scoring uses basePoints / tileCount.</summary>
    public static int GetBasePoints(HabitatAnimal animal)
    {
        return animal switch
        {
            HabitatAnimal.Bees => 300,
            HabitatAnimal.Beaver => 400,
            HabitatAnimal.Deer => 500,
            HabitatAnimal.Bear => 600,
            HabitatAnimal.RockDweller => 700,
            _ => 0
        };
    }

    /// <summary>All animals that participate in classification (excludes None).</summary>
    public static readonly HabitatAnimal[] ClassifiableAnimals =
    {
        HabitatAnimal.Deer,
        HabitatAnimal.Beaver,
        HabitatAnimal.Bear,
        HabitatAnimal.Bees,
        HabitatAnimal.RockDweller
    };

    public static float ComputeScore(HabitatAnimal animal, int tileCount)
    {
        if (tileCount <= 0) return float.NegativeInfinity;
        int b = GetBasePoints(animal);
        return b / (float)tileCount;
    }

    /// <summary>Awarded points for UI / meta; derived from best-candidate formula.</summary>
    public static int ComputeAwardedPoints(HabitatAnimal animal, int tileCount)
    {
        if (tileCount <= 0) return 0;
        return Mathf.RoundToInt(GetBasePoints(animal) / (float)tileCount);
    }

    /// <summary>Krótki opis brakujących osi biomu względem wymagań zwierzęcia (np. „1× Forest”).</summary>
    public static string FormatMissingBiomes(in BiomeVector current, in BiomeVector requirement)
    {
        var parts = new List<string>(5);
        AppendMissing(parts, requirement.Meadow - current.Meadow, TileBiome.Meadow);
        AppendMissing(parts, requirement.Forest - current.Forest, TileBiome.Forested);
        AppendMissing(parts, requirement.Bush - current.Bush, TileBiome.Bushy);
        AppendMissing(parts, requirement.Rock - current.Rock, TileBiome.Rocks);
        AppendMissing(parts, requirement.Water - current.Water, TileBiome.Water);
        if (parts.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(parts[i]);
        }
        return sb.ToString();
    }

    private static void AppendMissing(List<string> parts, int deficit, TileBiome biome)
    {
        if (deficit <= 0) return;
        string name = TileBiomeRules.GetDisplayName(biome);
        parts.Add(deficit == 1 ? $"1× {name}" : $"{deficit}× {name}");
    }
}

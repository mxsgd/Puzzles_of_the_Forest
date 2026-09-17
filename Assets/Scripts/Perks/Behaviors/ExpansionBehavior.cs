using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Expansion: when a habitat is completed, 30% chance to spawn a random-biome tile
/// on an empty cell adjacent to it — the finished habitat spills growth into the board.
/// Hook: HabitatAssigned -> chance-based SpawnTile(neighbor, random biome)
/// </summary>
[CreateAssetMenu(fileName = "Expansion", menuName = "Idle Forest/Perk Behaviors/Expansion")]
public class ExpansionBehavior : PerkBehavior
{
    [SerializeField, Range(0f, 1f)] private float chance = 0.30f;

    private static readonly TileBiome[] AllBiomes =
    {
        TileBiome.Meadow, TileBiome.Forested, TileBiome.Bushy, TileBiome.Rocks, TileBiome.Water
    };

    public override void OnHabitatAssigned(HabitatContext ctx, PerkRunState state, List<PerkCommand> commands)
    {
        if (Random.value > chance) return;
        if (ctx.Tiles == null || ctx.RuntimeStore == null) return;

        var candidates = new List<Tile>();
        foreach (var tile in ctx.Tiles)
        {
            if (tile == null) continue;
            foreach (var neighbor in tile.GetNeighbors())
            {
                if (neighbor == null) continue;
                var nrt = ctx.RuntimeStore.Get(neighbor);
                if (nrt != null && !nrt.occupied && !candidates.Contains(neighbor))
                    candidates.Add(neighbor);
            }
        }

        if (candidates.Count == 0) return;
        var target = candidates[Random.Range(0, candidates.Count)];
        var biome = AllBiomes[Random.Range(0, AllBiomes.Length)];
        commands.Add(PerkCommand.SpawnTile(target, biome));
    }
}

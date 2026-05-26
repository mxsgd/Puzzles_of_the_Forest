using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Expansion: after placing a tile, 30% chance to spawn a same-biome tile
/// on a random empty neighbor. A world-mutation perk.
/// Hook: TilePlaced -> chance-based SpawnTile(neighbor, same biome)
/// </summary>
[CreateAssetMenu(fileName = "Expansion", menuName = "Idle Forest/Perk Behaviors/Expansion")]
public class ExpansionBehavior : PerkBehavior
{
    [SerializeField, Range(0f, 1f)] private float chance = 0.30f;

    public override void OnTilePlaced(PlacementContext ctx, PerkRunState state, List<PerkCommand> commands)
    {
        if (Random.value > chance) return;
        if (ctx.Neighbors == null || ctx.RuntimeStore == null) return;

        var biome = ctx.Draw != null ? ctx.Draw.biome : TileBiome.None;
        if (biome == TileBiome.None) return;

        var candidates = new List<Tile>();
        for (int i = 0; i < ctx.Neighbors.Count; i++)
        {
            var n = ctx.Neighbors[i];
            if (n == null) continue;
            var nrt = ctx.RuntimeStore.Get(n);
            if (nrt != null && !nrt.occupied)
                candidates.Add(n);
        }

        if (candidates.Count == 0) return;
        var target = candidates[Random.Range(0, candidates.Count)];
        commands.Add(PerkCommand.SpawnTile(target, biome));
    }
}

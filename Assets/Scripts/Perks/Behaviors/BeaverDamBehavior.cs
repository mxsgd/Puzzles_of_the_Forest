using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Beaver Dam: when a Beaver habitat is created, attempt to spawn a Water tile
/// on an adjacent empty tile. A world-effect perk.
/// Hook: HabitatAssigned (Beaver) -> SpawnTile(neighbor, Water)
/// </summary>
[CreateAssetMenu(fileName = "BeaverDam", menuName = "Idle Forest/Perk Behaviors/Beaver Dam")]
public class BeaverDamBehavior : PerkBehavior
{
    [SerializeField] private HabitatAnimal triggerAnimal = HabitatAnimal.Beaver;

    public override void OnHabitatAssigned(HabitatContext ctx, PerkRunState state, List<PerkCommand> commands)
    {
        if (ctx.Animal != triggerAnimal) return;
        if (ctx.Tiles == null || ctx.Tiles.Count == 0) return;

        Tile target = FindAdjacentEmptyTile(ctx);
        if (target != null)
            commands.Add(PerkCommand.SpawnTile(target, TileBiome.Water));
    }

    private static Tile FindAdjacentEmptyTile(HabitatContext ctx)
    {
        if (ctx.RuntimeStore == null) return null;
        foreach (var tile in ctx.Tiles)
        {
            if (tile == null) continue;
            foreach (var neighbor in tile.GetNeighbors())
            {
                if (neighbor == null) continue;
                var nrt = ctx.RuntimeStore.Get(neighbor);
                if (nrt != null && !nrt.occupied)
                    return neighbor;
            }
        }
        return null;
    }
}

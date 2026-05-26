using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Biodiversity: if a habitat region contains >= N distinct biomes, gain +1 deck tile.
/// Hook: HabitatAssigned -> if distinctBiomes >= threshold -> AddDeckTiles(+1)
/// </summary>
[CreateAssetMenu(fileName = "Biodiversity", menuName = "Idle Forest/Perk Behaviors/Biodiversity")]
public class BiodiversityBehavior : PerkBehavior
{
    [SerializeField, Min(2)] private int requiredDistinctBiomes = 4;
    [SerializeField, Min(1)] private int bonusTiles = 1;

    public override void OnHabitatAssigned(HabitatContext ctx, PerkRunState state, List<PerkCommand> commands)
    {
        if (ctx.DistinctBiomeCount >= requiredDistinctBiomes)
            commands.Add(PerkCommand.AddDeckTiles(bonusTiles));
    }
}

using UnityEngine;

/// <summary>
/// Affinity for one biome: extra copies of that biome are added to the weighted draw pool used by
/// TileDeck for the initial deck, rerolls, and habitat/quest tile rewards alike, so it shows up
/// more often for the rest of the run. One asset per biome (Meadow, Forest, Bush, Rock, Water).
/// Hook: ModifyBiomeWeight.
/// </summary>
[CreateAssetMenu(fileName = "BiomeAffinity", menuName = "Idle Forest/Perk Behaviors/Biome Affinity")]
public class BiomeAffinityBehavior : PerkBehavior
{
    [SerializeField] private TileBiome favoredBiome;
    [SerializeField, Min(0)] private int weightBonus = 3;

    public override int ModifyBiomeWeight(TileBiome biome, int baseWeight, PerkRunState state)
    {
        return biome == favoredBiome ? baseWeight + weightBonus : baseWeight;
    }
}

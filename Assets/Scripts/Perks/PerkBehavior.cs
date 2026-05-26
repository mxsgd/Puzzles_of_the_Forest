using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for perk logic. Each concrete perk overrides only the hooks it needs.
/// Returned commands are executed by PerkEffectExecutor through domain owners.
///
/// Subclass as ScriptableObject so behaviors can live as sub-assets of PerkDefinition
/// or as standalone reusable assets.
/// </summary>
public abstract class PerkBehavior : ScriptableObject
{
    /// <summary>Modify starting resources at session begin. Called once per run.</summary>
    public virtual void OnSessionStart(PerkRunState state, List<PerkCommand> commands) { }

    /// <summary>React to a habitat being created. Return commands for rewards.</summary>
    public virtual void OnHabitatAssigned(HabitatContext ctx, PerkRunState state, List<PerkCommand> commands) { }

    /// <summary>
    /// Query: should the next reroll be free? Return true to consume a free charge
    /// instead of a normal reroll. Called before the reroll cost is deducted.
    /// </summary>
    public virtual bool TryConsumeFreeReroll(PerkRunState state) => false;

    /// <summary>React to a tile being placed. Return commands for world mutations.</summary>
    public virtual void OnTilePlaced(PlacementContext ctx, PerkRunState state, List<PerkCommand> commands) { }

    /// <summary>
    /// Modify habitat evaluation score for a candidate. Return the adjusted score.
    /// Called by both hover preview and final classifier through the shared pipeline.
    /// </summary>
    public virtual float ModifyHabitatScore(HabitatAnimal animal, float baseScore,
        int regionTileCount, int distinctBiomes, PerkRunState state)
        => baseScore;
}

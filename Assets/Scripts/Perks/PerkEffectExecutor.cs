using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Executes PerkCommands through proper domain owners.
/// This is the only class that touches TileDeck / GameUI / TilePlacementService
/// on behalf of perks — perks themselves never call those systems directly.
/// </summary>
public class PerkEffectExecutor
{
    private readonly TileDeck _deck;
    private readonly TilePlacementService _placement;
    private readonly TileRuntimeStore _runtimeStore;
    private readonly TileGrid _grid;
    private readonly PerkRunState _state;

    public PerkEffectExecutor(TileDeck deck, TilePlacementService placement,
        TileRuntimeStore runtimeStore, TileGrid grid, PerkRunState state)
    {
        _deck = deck;
        _placement = placement;
        _runtimeStore = runtimeStore;
        _grid = grid;
        _state = state;
    }

    public void Execute(List<PerkCommand> commands)
    {
        if (commands == null) return;
        for (int i = 0; i < commands.Count; i++)
            Execute(commands[i]);
    }

    public void Execute(PerkCommand cmd)
    {
        switch (cmd.Kind)
        {
            case PerkCommandKind.AddDeckTiles:
                _deck?.EnqueueRandomTilesFromPool(cmd.IntValue);
                break;

            case PerkCommandKind.AddRerolls:
                _state.BonusRerolls += cmd.IntValue;
                break;

            case PerkCommandKind.GrantFreeRerollCharges:
                _state.FreeRerollCharges += cmd.IntValue;
                break;

            case PerkCommandKind.SpawnTile:
                TrySpawnTile(cmd.TargetTile, cmd.BiomeValue);
                break;

            case PerkCommandKind.TransformTileBiome:
                TryTransformBiome(cmd.TargetTile, cmd.BiomeValue);
                break;
        }
    }

    private void TrySpawnTile(Tile target, TileBiome biome)
    {
        if (target == null || _placement == null || _runtimeStore == null) return;
        var rt = _runtimeStore.Get(target);
        if (rt.occupied) return;

        var prefab = _deck != null ? _deck.GetPrefabFor(biome) : null;
        if (prefab == null) return;

        var draw = new TileDraw(biome, prefab, null, TileBiomeRules.GetDisplayName(biome));
        var rotation = _grid != null ? _grid.transform.rotation : Quaternion.identity;
        _placement.PlaceOccupant(target, rotation, draw);
    }

    private void TryTransformBiome(Tile target, TileBiome newBiome)
    {
        if (target == null || _runtimeStore == null) return;
        var rt = _runtimeStore.Get(target);
        if (!rt.occupied) return;

        rt.biome = newBiome;
        if (rt.biomeRuntime != null)
        {
            float radius = _grid != null ? _grid.HexRadius : 1f;
            rt.biomeRuntime.Initialize(newBiome, radius);
        }
    }
}

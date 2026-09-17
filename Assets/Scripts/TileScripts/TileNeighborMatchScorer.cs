using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Base game mechanic (always on, not a perk): whenever a tile becomes occupied, flood-fill the
/// connected group of same-biome tiles it now belongs to and award
/// <see cref="pointsPerGroupTile"/> points per tile in that group (10 base + 10 per additional
/// tile already sharing the biome collapses to a flat 10 x group size). Independent of habitat
/// scoring — a placement can score both a habitat AND this bonus at once. This is a deliberate
/// counter-pull against habitat building, which needs a *mixed* set of biomes in a small region:
/// growing one big same-biome field competes with keeping tiles free for habitat recipes, so the
/// player has to think about placement, not just which habitat to chase. Scales up the same way
/// habitat merging does (§7.4 in the GDD) — the biggest connected group wins big, on purpose.
/// </summary>
public class TileNeighborMatchScorer : MonoBehaviour
{
    [SerializeField] private TileRuntimeStore runtimeStore;
    [SerializeField] private GameUI gameUI;
    [SerializeField, Min(1)] private int pointsPerGroupTile = 10;

    private readonly HashSet<Tile> _visited = new();
    private readonly Queue<Tile> _frontier = new();

    private void Awake()
    {
        if (!runtimeStore) runtimeStore = FindAnyObjectByType<TileRuntimeStore>();
        if (!gameUI) gameUI = FindAnyObjectByType<GameUI>();
    }

    private void OnEnable()  => TileEvents.TileStateChanged += OnTileStateChanged;
    private void OnDisable() => TileEvents.TileStateChanged -= OnTileStateChanged;

    private void OnTileStateChanged(Tile tile)
    {
        if (runtimeStore == null || gameUI == null || tile == null) return;

        var rt = runtimeStore.Get(tile);
        if (rt == null || !rt.occupied || rt.biome == TileBiome.None) return;

        int groupSize = CountConnectedSameBiomeGroup(tile, rt.biome);
        if (groupSize < 2) return;

        gameUI.AddScore(groupSize * pointsPerGroupTile);
        TileEvents.RaiseSameBiomeGroupScored(
            new SameBiomeGroupData(tile, rt.biome, new List<Tile>(_visited)));
    }

    private int CountConnectedSameBiomeGroup(Tile start, TileBiome biome)
    {
        _visited.Clear();
        _frontier.Clear();
        _visited.Add(start);
        _frontier.Enqueue(start);

        while (_frontier.Count > 0)
        {
            var t = _frontier.Dequeue();
            foreach (var neighbor in t.GetNeighbors())
            {
                if (neighbor == null || _visited.Contains(neighbor)) continue;
                var nrt = runtimeStore.Get(neighbor);
                if (nrt == null || !nrt.occupied || nrt.biome != biome) continue;
                _visited.Add(neighbor);
                _frontier.Enqueue(neighbor);
            }
        }

        return _visited.Count;
    }
}

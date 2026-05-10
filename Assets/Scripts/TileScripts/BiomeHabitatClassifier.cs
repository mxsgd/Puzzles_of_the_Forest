using System;
using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// After each tile placement: BFS ball (radius 5), incrementally enumerates connected regions (≤5 tiles),
/// evaluates biome vectors with compatibility filtering, picks the single best candidate by score = basePoints/tileCount.
/// </summary>
public class BiomeHabitatClassifier : MonoBehaviour
{
    [SerializeField] private TileRuntimeStore runtimeStore;

    [Header("Region search")]
    [SerializeField, Min(1)] private int maxTilesPerHabitat = 5;
    [SerializeField, Min(1)] private int maxGraphStepsFromPlacement = 5;

    [Header("Debug")]
    [SerializeField] private bool logClassification = true;
    [SerializeField] private bool verboseDebug;

    public int MaxTilesPerHabitat => maxTilesPerHabitat;
    public int MaxGraphStepsFromPlacement => maxGraphStepsFromPlacement;

    private readonly HashSet<Tile> _ballWork = new HashSet<Tile>();
    private readonly HashSet<Tile> _allowedWork = new HashSet<Tile>();
    private readonly HashSet<string> _regionKeySeen = new HashSet<string>();
    private readonly HashSet<Tile> _regionSetWork = new HashSet<Tile>();
    private readonly List<Tile> _regionListWork = new List<Tile>();
    private readonly List<Tile> _neighborScratch = new List<Tile>();

    private void Awake()
    {
        if (!runtimeStore) runtimeStore = FindAnyObjectByType<TileRuntimeStore>();
    }

    private void OnEnable()
    {
        TileEvents.TileStateChanged += OnTileStateChanged;
    }

    private void OnDisable()
    {
        TileEvents.TileStateChanged -= OnTileStateChanged;
    }

    private void OnTileStateChanged(Tile tile)
    {
        try
        {
            RunClassification(tile);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BiomeHabitat] RunClassification: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>Runs habitat discovery from the last placed tile (event sender).</summary>
    public void RunClassification(Tile placedTile = null)
    {
        if (runtimeStore == null || placedTile == null)
            return;

        var rt = runtimeStore.Get(placedTile);
        if (rt == null || !rt.occupied)
            return;

        CollectOccupiedBall(placedTile, maxGraphStepsFromPlacement, _ballWork);
        if (_ballWork.Count == 0)
            return;

        _allowedWork.Clear();
        foreach (var t in _ballWork)
        {
            var r = runtimeStore.Get(t);
            if (r != null && r.occupied && r.CanAcceptNewHabitat())
                _allowedWork.Add(t);
        }

        if (_allowedWork.Count == 0)
        {
            if (verboseDebug) Debug.Log("[BiomeHabitat] Brak kafli z miejscem na habitata w kulę.");
            return;
        }

        _regionKeySeen.Clear();

        HabitatAnimal bestAnimal = HabitatAnimal.None;
        List<Tile> bestRegion = null;
        float bestScore = float.NegativeInfinity;
        int bestTileCount = int.MaxValue;

        var seeds = new List<Tile>(_allowedWork);
        seeds.Sort(CompareTilesDeterministic);

        foreach (var seed in seeds)
        {
            EnumerateConnectedRegionsFromSeed(
                seed,
                _allowedWork,
                maxTilesPerHabitat,
                region =>
                {
                    foreach (var animal in HabitatRequirements.ClassifiableAnimals)
                    {
                        if (!TryBuildFilteredBiomeVector(region, animal, out var vec))
                            continue;

                        var req = HabitatRequirements.GetRequirement(animal);
                        if (!vec.Satisfies(req))
                            continue;

                        float score = HabitatRequirements.ComputeScore(animal, region.Count);
                        int basePts = HabitatRequirements.GetBasePoints(animal);

                        if (IsBetterCandidate(score, basePts, region.Count, animal, region,
                                bestScore, bestAnimal, bestTileCount, bestRegion))
                        {
                            bestScore = score;
                            bestAnimal = animal;
                            bestTileCount = region.Count;
                            if (bestRegion == null)
                                bestRegion = new List<Tile>();
                            else
                                bestRegion.Clear();
                            bestRegion.AddRange(region);
                        }
                    }
                });
        }

        if (bestAnimal == HabitatAnimal.None || bestRegion == null || bestRegion.Count == 0)
            return;

        if (runtimeStore.TryRegisterHabitat(bestAnimal, bestRegion, out _))
        {
            if (logClassification)
                Debug.Log($"[BiomeHabitat] Habitat {bestAnimal} id — kafle: {bestRegion.Count}, score≈{bestScore:F3}");
        }
    }

    private static int CompareTilesDeterministic(Tile a, Tile b)
    {
        if (a == null || b == null) return 0;
        int c = a.q.CompareTo(b.q);
        if (c != 0) return c;
        return a.r.CompareTo(b.r);
    }

    /// <summary>
    /// Strict ordering for picking one winner among ties (deterministic).
    /// </summary>
    private static bool IsBetterCandidate(
        float score, int basePoints, int tileCount, HabitatAnimal animal, List<Tile> region,
        float bestScore, HabitatAnimal bestAnimal, int bestTileCount, List<Tile> bestRegion)
    {
        const float eps = 1e-5f;
        if (score > bestScore + eps) return true;
        if (score < bestScore - eps) return false;

        if (basePoints > HabitatRequirements.GetBasePoints(bestAnimal)) return true;
        if (basePoints < HabitatRequirements.GetBasePoints(bestAnimal)) return false;

        if (tileCount < bestTileCount) return true;
        if (tileCount > bestTileCount) return false;

        int cmp = CompareTileLists(region, bestRegion);
        if (cmp != 0) return cmp < 0;

        return animal < bestAnimal;
    }

    private static int CompareTileLists(List<Tile> a, List<Tile> b)
    {
        if (b == null || b.Count == 0) return a != null && a.Count > 0 ? -1 : 0;
        if (a == null || a.Count == 0) return 1;
        var aa = new List<Tile>(a);
        var bb = new List<Tile>(b);
        aa.Sort(CompareTilesDeterministic);
        bb.Sort(CompareTilesDeterministic);
        int n = Mathf.Min(aa.Count, bb.Count);
        for (int i = 0; i < n; i++)
        {
            int c = CompareTilesDeterministic(aa[i], bb[i]);
            if (c != 0) return c;
        }
        return aa.Count.CompareTo(bb.Count);
    }

    /// <summary>
    /// Occupied tiles within maxSteps edges from center (for classification locality).
    /// </summary>
    private void CollectOccupiedBall(Tile center, int maxSteps, HashSet<Tile> into)
    {
        into.Clear();
        var queue = new Queue<(Tile t, int dist)>();
        queue.Enqueue((center, 0));
        into.Add(center);
        while (queue.Count > 0)
        {
            var (t, dist) = queue.Dequeue();
            if (dist >= maxSteps) continue;
            foreach (var n in GetNeighborTilesSafe(t))
            {
                if (n == null || into.Contains(n)) continue;
                var rn = runtimeStore.Get(n);
                if (rn == null || !rn.occupied) continue;
                into.Add(n);
                queue.Enqueue((n, dist + 1));
            }
        }
    }

    private IEnumerable<Tile> GetNeighborTilesSafe(Tile t)
    {
        try
        {
            return t.GetNeighbors() ?? System.Array.Empty<Tile>();
        }
        catch
        {
            return System.Array.Empty<Tile>();
        }
    }

    /// <summary>
    /// Incremental enumeration: only connected sets containing seed, |R| ≤ maxSize, vertices ⊆ allowed.
    /// Dedupes by canonical key across all seeds.
    /// </summary>
    private void EnumerateConnectedRegionsFromSeed(
        Tile seed,
        HashSet<Tile> allowed,
        int maxSize,
        Action<List<Tile>> onRegion)
    {
        _regionSetWork.Clear();
        _regionSetWork.Add(seed);
        ExtendConnectedRegion(allowed, maxSize, onRegion);
    }

    /// <summary>
    /// DFS over connected supersets: emit each unique region once (global key), grow by one boundary tile at a time.
    /// </summary>
    private void ExtendConnectedRegion(HashSet<Tile> allowed, int maxSize, Action<List<Tile>> onRegion)
    {
        EmitCurrentRegion(onRegion);

        if (_regionSetWork.Count >= maxSize)
            return;

        CollectNeighborsOutsideRegion(allowed, _neighborScratch);
        // Local copy: deeper recursive calls reuse _neighborScratch; parent must not enumerate a list cleared mid-loop.
        var frontier = new List<Tile>(_neighborScratch);

        foreach (var n in frontier)
        {
            _regionSetWork.Add(n);
            ExtendConnectedRegion(allowed, maxSize, onRegion);
            _regionSetWork.Remove(n);
        }
    }

    private void CollectNeighborsOutsideRegion(HashSet<Tile> allowed, List<Tile> into)
    {
        into.Clear();
        foreach (var t in _regionSetWork)
        {
            foreach (var n in GetNeighborTilesSafe(t))
            {
                if (n == null || !allowed.Contains(n) || _regionSetWork.Contains(n))
                    continue;
                if (!into.Contains(n))
                    into.Add(n);
            }
        }
        into.Sort(CompareTilesDeterministic);
    }

    private void EmitCurrentRegion(Action<List<Tile>> onRegion)
    {
        _regionListWork.Clear();
        foreach (var t in _regionSetWork)
            _regionListWork.Add(t);
        _regionListWork.Sort(CompareTilesDeterministic);

        var key = RegionKeyCanonical(_regionListWork);
        if (!_regionKeySeen.Add(key))
            return;

        onRegion?.Invoke(new List<Tile>(_regionListWork));
    }

    private static string RegionKeyCanonical(List<Tile> sortedTiles)
    {
        var parts = new List<string>(sortedTiles.Count);
        foreach (var t in sortedTiles)
        {
            if (t != null)
                parts.Add($"{t.q}:{t.r}");
        }
        return string.Join("|", parts);
    }

    /// <summary>
    /// Builds H from tiles; tiles incompatible with newAnimal on existing habitats contribute 0.
    /// Region is discarded only when caller finds vector does not satisfy requirement (spec: invalid candidate).
    /// </summary>
    private bool TryBuildFilteredBiomeVector(List<Tile> region, HabitatAnimal newAnimal, out BiomeVector vector)
    {
        vector = BiomeVector.Zero;
        foreach (var t in region)
        {
            if (t == null) continue;
            var rt = runtimeStore.Get(t);
            if (rt == null || !rt.occupied || !rt.CanAcceptNewHabitat())
                return false;

            if (!HabitatCompatibilityService.IsCompatibleWithAllOnTile(newAnimal, runtimeStore, t))
                continue;

            vector.Add(BiomeVector.FromTileBiome(rt.biome));
        }

        return true;
    }
}

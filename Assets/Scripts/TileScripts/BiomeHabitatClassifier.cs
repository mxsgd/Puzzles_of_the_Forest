using System;
using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// After each tile placement: BFS ball (radius 5), incrementally enumerates connected regions (≤5 tiles),
/// evaluates biome vectors with compatibility filtering, picks the single best candidate by score = basePoints/tileCount.
/// Only regions containing the placed tile are evaluated — eliminates the majority of useless callbacks.
/// Zero per-call heap allocs via HabitatRegionScratch shared buffers.
/// </summary>
public class BiomeHabitatClassifier : MonoBehaviour
{
    [SerializeField] private TileRuntimeStore runtimeStore;

    [Header("Region search")]
    [SerializeField, Min(1)] private int maxTilesPerHabitat = 5;
    [SerializeField, Min(1)] private int maxGraphStepsFromPlacement = 5;

    [Header("Habitat rules")]
    [SerializeField] private HabitatRulesProfile rulesProfile;

    [Header("Debug")]
    [SerializeField] private bool logClassification = true;
    [SerializeField] private bool verboseDebug;

    public int MaxTilesPerHabitat        => maxTilesPerHabitat;
    public int MaxGraphStepsFromPlacement => maxGraphStepsFromPlacement;
    public HabitatRulesProfile RulesProfile => rulesProfile;

    private readonly HabitatRegionScratch _scratch = new();

    private void Awake()
    {
        if (!runtimeStore) runtimeStore = FindAnyObjectByType<TileRuntimeStore>();
    }

    private void OnEnable()  => TileEvents.TileStateChanged += OnTileStateChanged;
    private void OnDisable() => TileEvents.TileStateChanged -= OnTileStateChanged;

    private void OnTileStateChanged(Tile tile)
    {
        try   { RunClassification(tile); }
        catch (Exception e) { Debug.LogError($"[BiomeHabitat] RunClassification: {e.Message}\n{e.StackTrace}"); }
    }

    /// <summary>Runs habitat discovery from the last placed tile.</summary>
    public void RunClassification(Tile placedTile = null)
    {
        if (runtimeStore == null || placedTile == null) return;

        var rt = runtimeStore.Get(placedTile);
        if (rt == null || !rt.occupied) return;

        // BFS: collect occupied tiles within radius
        HabitatRegionEnumerator.CollectOccupiedBall(
            runtimeStore, placedTile, maxGraphStepsFromPlacement, _scratch,
            includeCenterRegardlessOfOccupied: true);

        if (_scratch.Ball.Count == 0) return;

        // Filter to tiles that can still accept a habitat
        _scratch.Allowed.Clear();
        foreach (Tile t in _scratch.Ball)
        {
            var r = runtimeStore.Get(t);
            if (r != null && r.occupied && r.CanAcceptNewHabitat())
                _scratch.Allowed.Add(t);
        }

        if (_scratch.Allowed.Count == 0)
        {
            if (verboseDebug) Debug.Log("[BiomeHabitat] Brak kafli z miejscem na habitata w kuli.");
            return;
        }

        // placedTile must be in allowed for any useful region to exist
        if (!_scratch.Allowed.Contains(placedTile)) return;

        HabitatAnimal bestAnimal    = HabitatAnimal.None;
        List<Tile>    bestRegion    = null;
        float         bestScore     = float.NegativeInfinity;
        int           bestTileCount = int.MaxValue;

        // DFS over all connected subsets that MUST contain placedTile — main perf win
        HabitatRegionEnumerator.ForEachRegionContaining(
            _scratch.Allowed, maxTilesPerHabitat, mustInclude: placedTile, _scratch,
            region =>
            {
                HabitatCoreValidation.PrepareRegionCoreAnalysis(region, _scratch);

                foreach (var animal in HabitatRequirements.ClassifiableAnimals)
                {
                    if (!TryBuildFilteredBiomeVector(region, animal, out var vec)) continue;

                    var req = HabitatRequirements.GetRequirement(animal);
                    if (!vec.Satisfies(req)) continue;

                    if (!HabitatCoreValidation.ValidateCoreRequirement(
                            region, animal, rulesProfile, _scratch, out _))
                    {
                        if (verboseDebug)
                            Debug.Log($"[BiomeHabitat] Odrzucono {animal} — za mało kafli rdzeniowych.");
                        continue;
                    }

                    float score   = HabitatRequirements.ComputeScore(animal, region.Count);
                    int distinctBiomes = CountDistinctBiomesInRegion(region);
                    score = ApplyPerkScoreModifiers(animal, score, region.Count, distinctBiomes);
                    int   basePts = HabitatRequirements.GetBasePoints(animal);

                    if (IsBetterCandidate(score, basePts, region.Count, animal, region,
                            bestScore, bestAnimal, bestTileCount, bestRegion))
                    {
                        bestScore     = score;
                        bestAnimal    = animal;
                        bestTileCount = region.Count;
                        // region is a shared buffer — snapshot it
                        if (bestRegion == null) bestRegion = new List<Tile>(region);
                        else { bestRegion.Clear(); bestRegion.AddRange(region); }
                    }
                }
            });

        if (bestAnimal == HabitatAnimal.None || bestRegion == null || bestRegion.Count == 0)
            return;

        if (runtimeStore.TryRegisterHabitat(bestAnimal, bestRegion, out _, rulesProfile))
        {
            if (logClassification)
                Debug.Log($"[BiomeHabitat] Habitat {bestAnimal} — kafle: {bestRegion.Count}, score≈{bestScore:F3}");
        }
    }

    // -------------------------------------------------------------------------
    // Biome vector
    // -------------------------------------------------------------------------

    private bool TryBuildFilteredBiomeVector(List<Tile> region, HabitatAnimal newAnimal, out BiomeVector vector)
    {
        vector = BiomeVector.Zero;
        foreach (Tile t in region)
        {
            if (t == null) continue;
            var r = runtimeStore.Get(t);
            if (r == null || !r.occupied || !r.CanAcceptNewHabitat()) return false;

            if (!HabitatCompatibilityService.IsCompatibleWithAllOnTile(newAnimal, runtimeStore, t))
                continue;

            vector.Add(BiomeVector.FromTileBiome(r.biome));
        }
        return true;
    }

    // -------------------------------------------------------------------------
    // Candidate comparison
    // -------------------------------------------------------------------------

    private static readonly Comparison<Tile> TileOrder = CompareTilesDeterministic;

    private static bool IsBetterCandidate(
        float score, int basePoints, int tileCount, HabitatAnimal animal, List<Tile> region,
        float bestScore, HabitatAnimal bestAnimal, int bestTileCount, List<Tile> bestRegion)
    {
        const float eps = 1e-5f;
        if (score > bestScore + eps) return true;
        if (score < bestScore - eps) return false;

        int bp = HabitatRequirements.GetBasePoints(bestAnimal);
        if (basePoints > bp) return true;
        if (basePoints < bp) return false;

        if (tileCount < bestTileCount) return true;
        if (tileCount > bestTileCount) return false;

        int cmp = CompareTileLists(region, bestRegion);
        if (cmp != 0) return cmp < 0;

        return animal < bestAnimal;
    }

    private static int CompareTilesDeterministic(Tile a, Tile b)
    {
        if (a == null || b == null) return 0;
        int c = a.q.CompareTo(b.q);
        return c != 0 ? c : a.r.CompareTo(b.r);
    }

    private static int CompareTileLists(List<Tile> a, List<Tile> b)
    {
        if (b == null || b.Count == 0) return (a != null && a.Count > 0) ? -1 : 0;
        if (a == null || a.Count == 0) return 1;

        int n = UnityEngine.Mathf.Min(a.Count, b.Count);
        for (int i = 0; i < n; i++)
        {
            int c = CompareTilesDeterministic(a[i], b[i]);
            if (c != 0) return c;
        }
        return a.Count.CompareTo(b.Count);
    }

    // -------------------------------------------------------------------------
    // Perk-aware shared pipeline
    // -------------------------------------------------------------------------

    private static float ApplyPerkScoreModifiers(HabitatAnimal animal, float baseScore,
        int regionTileCount, int distinctBiomes)
    {
        if (PerkManager.Instance == null) return baseScore;
        return PerkManager.Instance.ModifyHabitatScore(animal, baseScore, regionTileCount, distinctBiomes);
    }

    private int CountDistinctBiomesInRegion(List<Tile> region)
    {
        int mask = 0;
        foreach (var t in region)
        {
            if (t == null) continue;
            var rt = runtimeStore.Get(t);
            if (rt != null && rt.biome != TileBiome.None)
                mask |= 1 << (int)rt.biome;
        }
        int count = 0;
        while (mask != 0) { count += mask & 1; mask >>= 1; }
        return count;
    }
}

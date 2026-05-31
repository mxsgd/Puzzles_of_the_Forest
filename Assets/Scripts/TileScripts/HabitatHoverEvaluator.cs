using System;
using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Scratch buffers for hover preview evaluation.
/// Contains a HabitatRegionScratch for BFS/DFS (zero alloc) plus hover-specific fields.
/// Allocate once per consumer, reuse every frame.
/// </summary>
public sealed class HabitatHoverScratch
{
    public readonly HabitatRegionScratch          Region        = new();
    public readonly List<HabitatAnimal>           YellowAnimals = new();
    public readonly HashSet<HabitatAnimal>        YellowSet     = new();
    public readonly List<Tile>                    CandidateRegion = new();
}

public enum HabitatHoverPreviewKind { Gray, Yellow, Green }

public readonly struct HabitatHoverResult
{
    public readonly HabitatHoverPreviewKind      Kind;
    public readonly HabitatAnimal                GreenAnimal;
    public readonly IReadOnlyList<HabitatAnimal> YellowAnimals;

    public HabitatHoverResult(HabitatHoverPreviewKind kind, HabitatAnimal greenAnimal,
        IReadOnlyList<HabitatAnimal> yellowAnimals)
    {
        Kind          = kind;
        GreenAnimal   = greenAnimal;
        YellowAnimals = yellowAnimals ?? Array.Empty<HabitatAnimal>();
    }
}

/// <summary>
/// Podgląd habitatu nad polem.
/// Wszystkie alokacje wewnętrzne są wyeliminowane przez reuse buforów z HabitatHoverScratch.
/// </summary>
public static class HabitatHoverEvaluator
{
    private static readonly Comparison<HabitatAnimal> AnimalOrder = (a, b) => a.CompareTo(b);

    public static void Evaluate(
        TileRuntimeStore store,
        Tile hoverTile,
        TileDraw nextDraw,
        int maxTilesPerHabitat,
        int maxGraphSteps,
        HabitatHoverScratch s,
        out HabitatHoverResult result,
        HabitatRulesProfile rulesProfile = null)
    {
        result = new HabitatHoverResult(HabitatHoverPreviewKind.Gray, HabitatAnimal.None, Array.Empty<HabitatAnimal>());
        if (store == null || hoverTile == null) return;

        var hoverRt = store.Get(hoverTile);
        if (hoverRt == null) return;

        // Already occupied — show existing habitat if present
        if (hoverRt.occupied)
        {
            if (hoverRt.habitatId >= 0 &&
                store.TryGetHabitatAnimal(hoverRt.habitatId, out var registered) &&
                registered != HabitatAnimal.None)
            {
                result = new HabitatHoverResult(HabitatHoverPreviewKind.Green, registered, Array.Empty<HabitatAnimal>());
            }
            return;
        }

        if (nextDraw == null || nextDraw.biome == TileBiome.None) return;
        if (!hoverRt.CanAcceptNewHabitat()) return;

        var rs = s.Region;

        // BFS — center (unoccupied hover tile) is always included
        HabitatRegionEnumerator.CollectOccupiedBall(
            store, hoverTile, maxGraphSteps, rs,
            includeCenterRegardlessOfOccupied: true);

        if (rs.Ball.Count == 0) return;

        rs.Allowed.Clear();
        foreach (Tile t in rs.Ball)
        {
            if (t == null) continue;
            var r = store.Get(t);
            if (ReferenceEquals(t, hoverTile))
            {
                if (r.CanAcceptNewHabitat()) rs.Allowed.Add(t);
                continue;
            }
            if (r.occupied && r.CanAcceptNewHabitat()) rs.Allowed.Add(t);
        }

        if (!rs.Allowed.Contains(hoverTile)) return;

        s.YellowSet.Clear();

        HabitatAnimal bestGreenAnimal    = HabitatAnimal.None;
        List<Tile>    bestGreenRegion    = null;
        float         bestGreenScore     = float.NegativeInfinity;
        int           bestGreenTileCount = int.MaxValue;

        // DFS — mustInclude = hoverTile (same optimisation as Classifier)
        HabitatRegionEnumerator.ForEachRegionContaining(
            rs.Allowed, maxTilesPerHabitat, mustInclude: hoverTile, rs,
            region =>
            {
                foreach (var animal in HabitatRequirements.ClassifiableAnimals)
                {
                    if (!TryBuildSimulatedBiomeVector(store, region, animal, hoverTile, nextDraw, out var vec))
                        continue;

                    var req = HabitatRequirements.GetRequirement(animal);
                    if (vec.Satisfies(req))
                    {
                        if (!HabitatCoreValidation.ValidateCoreRequirement(
                                region, animal, rulesProfile, out _))
                            continue;

                        float score   = HabitatRequirements.ComputeScore(animal, region.Count);
                        int distinctBiomes = CountDistinctBiomesInRegion(store, region, hoverTile, nextDraw);
                        score = ApplyPerkScoreModifiers(animal, score, region.Count, distinctBiomes);
                        int   basePts = HabitatRequirements.GetBasePoints(animal);
                        if (IsBetterCandidate(score, basePts, region.Count, animal, region,
                                bestGreenScore, bestGreenAnimal, bestGreenTileCount, bestGreenRegion))
                        {
                            bestGreenScore     = score;
                            bestGreenAnimal    = animal;
                            bestGreenTileCount = region.Count;
                            // region is shared buffer — snapshot
                            if (bestGreenRegion == null) bestGreenRegion = new List<Tile>(region);
                            else { bestGreenRegion.Clear(); bestGreenRegion.AddRange(region); }
                        }
                    }
                    else
                    {
                        if (vec.DeficitSumToward(req) == 1)
                            s.YellowSet.Add(animal);
                    }
                }
            });

        if (bestGreenAnimal != HabitatAnimal.None)
        {
            result = new HabitatHoverResult(HabitatHoverPreviewKind.Green, bestGreenAnimal, Array.Empty<HabitatAnimal>());
            return;
        }

        if (s.YellowSet.Count > 0)
        {
            s.YellowAnimals.Clear();
            foreach (var a in s.YellowSet) s.YellowAnimals.Add(a);
            s.YellowAnimals.Sort(AnimalOrder);
            result = new HabitatHoverResult(HabitatHoverPreviewKind.Yellow, HabitatAnimal.None,
                s.YellowAnimals.ToArray());
            return;
        }

        result = new HabitatHoverResult(HabitatHoverPreviewKind.Gray, HabitatAnimal.None, Array.Empty<HabitatAnimal>());
    }

    /// <summary>
    /// Region kafli będących wstępnym kandydatem na habitat danego zwierzęcia po postawieniu kafelka na <paramref name="hoverTile"/>.
    /// Zwraca true gdy znaleziono region (pełny habitat lub „prawie” — deficit 1).
    /// </summary>
    public static bool TryGetCandidateRegion(
        TileRuntimeStore store,
        Tile hoverTile,
        TileDraw nextDraw,
        HabitatAnimal animal,
        int maxTilesPerHabitat,
        int maxGraphSteps,
        HabitatHoverScratch s,
        out IReadOnlyList<Tile> region,
        HabitatRulesProfile rulesProfile = null)
    {
        region = Array.Empty<Tile>();
        if (store == null || hoverTile == null || animal == HabitatAnimal.None) return false;
        if (nextDraw == null || nextDraw.biome == TileBiome.None) return false;

        var hoverRt = store.Get(hoverTile);
        if (hoverRt == null || hoverRt.occupied || !hoverRt.CanAcceptNewHabitat()) return false;

        if (!TryPrepareHoverBall(store, hoverTile, maxGraphSteps, s, out var rs))
            return false;

        s.CandidateRegion.Clear();
        List<Tile> bestRegion = null;
        float bestScore = float.NegativeInfinity;
        int bestTileCount = int.MaxValue;
        bool bestIsFull = false;

        HabitatRegionEnumerator.ForEachRegionContaining(
            rs.Allowed, maxTilesPerHabitat, mustInclude: hoverTile, rs,
            candidate =>
            {
                if (!TryBuildSimulatedBiomeVector(store, candidate, animal, hoverTile, nextDraw, out var vec))
                    return;

                var req = HabitatRequirements.GetRequirement(animal);
                bool satisfies = vec.Satisfies(req);
                if (satisfies)
                {
                    if (!HabitatCoreValidation.ValidateCoreRequirement(candidate, animal, rulesProfile, out _))
                        return;

                    float score = HabitatRequirements.ComputeScore(animal, candidate.Count);
                    int distinctBiomes = CountDistinctBiomesInRegion(store, candidate, hoverTile, nextDraw);
                    score = ApplyPerkScoreModifiers(animal, score, candidate.Count, distinctBiomes);
                    int basePts = HabitatRequirements.GetBasePoints(animal);

                    if (IsBetterCandidate(score, basePts, candidate.Count, animal, candidate,
                            bestScore, animal, bestTileCount, bestRegion))
                    {
                        bestIsFull = true;
                        bestScore = score;
                        bestTileCount = candidate.Count;
                        CopyRegion(candidate, ref bestRegion);
                    }
                }
                else if (!bestIsFull && vec.DeficitSumToward(req) == 1)
                {
                    if (bestRegion == null || candidate.Count < bestTileCount)
                    {
                        bestTileCount = candidate.Count;
                        CopyRegion(candidate, ref bestRegion);
                    }
                }
            });

        if (bestRegion == null || bestRegion.Count == 0) return false;

        s.CandidateRegion.Clear();
        s.CandidateRegion.AddRange(bestRegion);
        region = s.CandidateRegion;
        return true;
    }

    private static bool TryPrepareHoverBall(
        TileRuntimeStore store,
        Tile hoverTile,
        int maxGraphSteps,
        HabitatHoverScratch s,
        out HabitatRegionScratch rs)
    {
        rs = s.Region;
        rs.Ball.Clear();
        rs.Allowed.Clear();

        HabitatRegionEnumerator.CollectOccupiedBall(
            store, hoverTile, maxGraphSteps, rs,
            includeCenterRegardlessOfOccupied: true);

        if (rs.Ball.Count == 0) return false;

        foreach (Tile t in rs.Ball)
        {
            if (t == null) continue;
            var r = store.Get(t);
            if (ReferenceEquals(t, hoverTile))
            {
                if (r.CanAcceptNewHabitat()) rs.Allowed.Add(t);
                continue;
            }
            if (r.occupied && r.CanAcceptNewHabitat()) rs.Allowed.Add(t);
        }

        return rs.Allowed.Contains(hoverTile);
    }

    private static void CopyRegion(List<Tile> source, ref List<Tile> dest)
    {
        if (dest == null) dest = new List<Tile>(source);
        else { dest.Clear(); dest.AddRange(source); }
    }

    // -------------------------------------------------------------------------
    // Biome vector simulation
    // -------------------------------------------------------------------------

    private static bool TryBuildSimulatedBiomeVector(
        TileRuntimeStore store, List<Tile> region,
        HabitatAnimal newAnimal, Tile hoverTile, TileDraw draw, out BiomeVector vector)
    {
        vector = BiomeVector.Zero;
        bool hoverContributed = false;

        foreach (Tile t in region)
        {
            if (t == null) continue;
            var rt = store.Get(t);
            bool isHover = ReferenceEquals(t, hoverTile);

            if (isHover)
            {
                if (!rt.CanAcceptNewHabitat()) return false;
                if (!HabitatCompatibilityService.IsCompatibleWithAllOnTile(newAnimal, store, t)) continue;
                if (draw == null || draw.biome == TileBiome.None) return false;
                vector.Add(BiomeVector.FromTileBiome(draw.biome));
                hoverContributed = true;
            }
            else
            {
                if (!rt.occupied || !rt.CanAcceptNewHabitat()) continue;
                if (!HabitatCompatibilityService.IsCompatibleWithAllOnTile(newAnimal, store, t)) continue;
                vector.Add(BiomeVector.FromTileBiome(rt.biome));
            }
        }

        return hoverContributed;
    }

    // -------------------------------------------------------------------------
    // Candidate comparison helpers
    // -------------------------------------------------------------------------

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

    // Both lists come pre-sorted from TryEmitRegion — no copy/sort.
    private static int CompareTileLists(List<Tile> a, List<Tile> b)
    {
        int aCount = a?.Count ?? 0;
        int bCount = b?.Count ?? 0;
        int n = Mathf.Min(aCount, bCount);
        for (int i = 0; i < n; i++)
        {
            int c = CompareTilesDeterministic(a[i], b[i]);
            if (c != 0) return c;
        }
        return aCount.CompareTo(bCount);
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

    private static int CountDistinctBiomesInRegion(TileRuntimeStore store, List<Tile> region,
        Tile hoverTile, TileDraw draw)
    {
        int mask = 0;
        foreach (var t in region)
        {
            if (t == null) continue;
            TileBiome biome;
            if (ReferenceEquals(t, hoverTile))
                biome = draw != null ? draw.biome : TileBiome.None;
            else
            {
                var rt = store.Get(t);
                biome = rt != null ? rt.biome : TileBiome.None;
            }
            if (biome != TileBiome.None)
                mask |= 1 << (int)biome;
        }
        int count = 0;
        while (mask != 0) { count += mask & 1; mask >>= 1; }
        return count;
    }
}

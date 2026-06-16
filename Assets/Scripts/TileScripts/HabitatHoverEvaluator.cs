using System;
using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Scratch buffers for hover preview evaluation.
/// Contains a HabitatRegionScratch for BFS/DFS (zero alloc) plus hover-specific fields.
/// Allocate once per consumer, reuse every frame.
/// </summary>
public enum HabitatCandidateOutlineKind
{
    /// <summary>Pełny habitat (zielona ikona) — vec.Satisfies na regionie przed przycięciem obrysu.</summary>
    Full,
    /// <summary>Prawie habitat (żółta ikona) — deficit 1; obrys tylko blisko hover (np. 3 kroki).</summary>
    Almost
}

public sealed class HabitatHoverScratch
{
    public readonly HabitatRegionScratch          Region        = new();
    /// <summary>Osobny bufor na filtr odległości — nie wolno używać <see cref="Region"/> w callbacku DFS.</summary>
    public readonly HabitatRegionScratch          Filter        = new();
    public readonly List<HabitatAnimal>           YellowAnimals = new();
    public readonly List<string>                    YellowDeficitHints = new();
    /// <summary>Stabilna kopia wyniku — nie czyścić poza <see cref="HabitatHoverEvaluator.Evaluate"/>.</summary>
    public readonly List<HabitatAnimal>           YellowAnimalsResult = new();
    public readonly List<string>                    YellowDeficitHintsResult = new();
    public readonly HashSet<HabitatAnimal>        YellowSet     = new();
    public readonly Dictionary<HabitatAnimal, string> YellowDeficitByAnimal = new();
    public readonly List<Tile>                    CandidateRegion = new();
}

public enum HabitatHoverPreviewKind { Gray, Yellow, Green }

public readonly struct HabitatHoverResult
{
    public readonly HabitatHoverPreviewKind      Kind;
    public readonly HabitatAnimal                GreenAnimal;
    public readonly IReadOnlyList<HabitatAnimal> YellowAnimals;
    /// <summary>Równolegle do <see cref="YellowAnimals"/> — czego brakuje do habitatu.</summary>
    public readonly IReadOnlyList<string>        YellowDeficitHints;

    public HabitatHoverResult(HabitatHoverPreviewKind kind, HabitatAnimal greenAnimal,
        IReadOnlyList<HabitatAnimal> yellowAnimals, IReadOnlyList<string> yellowDeficitHints = null)
    {
        Kind          = kind;
        GreenAnimal   = greenAnimal;
        YellowAnimals = yellowAnimals ?? Array.Empty<HabitatAnimal>();
        YellowDeficitHints = yellowDeficitHints ?? Array.Empty<string>();
    }

    public string GetYellowDeficitHint(int yellowIndex)
    {
        if (yellowIndex < 0 || yellowIndex >= YellowAnimals.Count)
            return string.Empty;
        return yellowIndex < YellowDeficitHints.Count ? YellowDeficitHints[yellowIndex] : string.Empty;
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
        s.YellowDeficitByAnimal.Clear();

        HabitatAnimal bestGreenAnimal    = HabitatAnimal.None;
        List<Tile>    bestGreenRegion    = null;
        float         bestGreenScore     = float.NegativeInfinity;
        int           bestGreenTileCount = int.MaxValue;

        // DFS — mustInclude = hoverTile (same optimisation as Classifier)
        HabitatRegionEnumerator.ForEachRegionContaining(
            rs.Allowed, maxTilesPerHabitat, mustInclude: hoverTile, rs,
            region =>
            {
                HabitatCoreValidation.PrepareRegionCoreAnalysis(region, rs);

                foreach (var animal in HabitatRequirements.ClassifiableAnimals)
                {
                    if (!TryBuildSimulatedBiomeVector(store, region, animal, hoverTile, nextDraw, out var vec))
                        continue;

                    var req = HabitatRequirements.GetRequirement(animal);
                    if (vec.Satisfies(req))
                    {
                        if (!HabitatCoreValidation.ValidateCoreRequirement(
                                region, animal, rulesProfile, rs, out _, store))
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
                        if (vec.DeficitSumToward(req) == 1
                            && HabitatCoreValidation.ValidateCoreRequirement(region, animal, rulesProfile, rs, out _, store))
                        {
                            s.YellowSet.Add(animal);
                            if (!s.YellowDeficitByAnimal.ContainsKey(animal))
                            {
                                var missing = HabitatRequirements.FormatMissingBiomes(vec, req);
                                s.YellowDeficitByAnimal[animal] = string.IsNullOrEmpty(missing)
                                    ? string.Empty
                                    : $"Need: {missing}";
                            }
                        }
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
            s.YellowAnimalsResult.Clear();
            s.YellowDeficitHintsResult.Clear();
            foreach (var a in s.YellowSet) s.YellowAnimalsResult.Add(a);
            s.YellowAnimalsResult.Sort(AnimalOrder);
            for (int i = 0; i < s.YellowAnimalsResult.Count; i++)
            {
                var a = s.YellowAnimalsResult[i];
                s.YellowDeficitHintsResult.Add(s.YellowDeficitByAnimal.TryGetValue(a, out var hint) ? hint : string.Empty);
            }

            result = new HabitatHoverResult(HabitatHoverPreviewKind.Yellow, HabitatAnimal.None,
                s.YellowAnimalsResult.ToArray(), s.YellowDeficitHintsResult.ToArray());
            return;
        }

        result = new HabitatHoverResult(HabitatHoverPreviewKind.Gray, HabitatAnimal.None, Array.Empty<HabitatAnimal>());
    }

    /// <summary>
    /// Region kafli do obrysu pod ikoną. Walidacja (wektor + rdzeń) na pełnym regionie DFS;
    /// obrys = tylko kafle w promieniu <paramref name="maxDisplayStepsFromHover"/> od hover.
    /// </summary>
    public static bool TryGetCandidateRegion(
        TileRuntimeStore store,
        Tile hoverTile,
        TileDraw nextDraw,
        HabitatAnimal animal,
        int maxTilesPerHabitat,
        int maxSearchGraphSteps,
        int maxDisplayStepsFromHover,
        HabitatCandidateOutlineKind outlineKind,
        HabitatHoverScratch s,
        out IReadOnlyList<Tile> region,
        HabitatRulesProfile rulesProfile = null)
    {
        region = Array.Empty<Tile>();
        if (store == null || hoverTile == null || animal == HabitatAnimal.None) return false;
        if (nextDraw == null || nextDraw.biome == TileBiome.None) return false;
        if (maxSearchGraphSteps < 1 || maxDisplayStepsFromHover < 1) return false;

        var hoverRt = store.Get(hoverTile);
        if (hoverRt == null || hoverRt.occupied || !hoverRt.CanAcceptNewHabitat()) return false;

        if (!TryPrepareHoverBall(store, hoverTile, maxSearchGraphSteps, s, out var rs))
            return false;

        s.CandidateRegion.Clear();
        List<Tile> bestRegion = null;
        float bestScore = float.NegativeInfinity;
        int bestTileCount = int.MaxValue;

        HabitatRegionEnumerator.ForEachRegionContaining(
            rs.Allowed, maxTilesPerHabitat, mustInclude: hoverTile, rs,
            candidate =>
            {
                HabitatCoreValidation.PrepareRegionCoreAnalysis(candidate, rs);

                if (!TryValidateRawRegionForOutline(
                        store, candidate, animal, hoverTile, nextDraw, outlineKind, rulesProfile, rs, out _))
                    return;

                if (!TryFilterDisplayRegion(
                        hoverTile, candidate, maxDisplayStepsFromHover, s.Filter, s.CandidateRegion, out var display))
                    return;

                float score = HabitatRequirements.ComputeScore(animal, display.Count);
                int distinctBiomes = CountDistinctBiomesInRegion(store, display, hoverTile, nextDraw);
                score = ApplyPerkScoreModifiers(animal, score, display.Count, distinctBiomes);
                int basePts = HabitatRequirements.GetBasePoints(animal);

                if (IsBetterCandidate(score, basePts, display.Count, animal, display,
                        bestScore, animal, bestTileCount, bestRegion))
                {
                    bestScore = score;
                    bestTileCount = display.Count;
                    CopyRegion(display, ref bestRegion);
                }
            });

        if (bestRegion == null || bestRegion.Count == 0) return false;

        if (!TryFilterDisplayRegion(
                hoverTile, bestRegion, maxDisplayStepsFromHover, s.Filter, s.CandidateRegion, out _))
            return false;

        region = s.CandidateRegion;
        return true;
    }

    private static bool TryValidateRawRegionForOutline(
        TileRuntimeStore store,
        List<Tile> rawRegion,
        HabitatAnimal animal,
        Tile hoverTile,
        TileDraw nextDraw,
        HabitatCandidateOutlineKind outlineKind,
        HabitatRulesProfile rulesProfile,
        HabitatRegionScratch scratch,
        out BiomeVector vector)
    {
        vector = BiomeVector.Zero;
        if (!TryBuildSimulatedBiomeVector(store, rawRegion, animal, hoverTile, nextDraw, out vector))
            return false;

        if (!HabitatCoreValidation.ValidateCoreRequirement(rawRegion, animal, rulesProfile, scratch, out _, store))
            return false;

        var req = HabitatRequirements.GetRequirement(animal);
        return outlineKind switch
        {
            HabitatCandidateOutlineKind.Full => vector.Satisfies(req),
            HabitatCandidateOutlineKind.Almost =>
                !vector.Satisfies(req) && vector.DeficitSumToward(req) == 1,
            _ => false
        };
    }

    private static bool TryFilterDisplayRegion(
        Tile hoverTile,
        IReadOnlyList<Tile> rawRegion,
        int maxStepsFromHover,
        HabitatRegionScratch filterScratch,
        List<Tile> output,
        out List<Tile> displayRegion)
    {
        displayRegion = null;
        if (!HabitatRegionEnumerator.TryFilterRegionNearHover(
                hoverTile, rawRegion, maxStepsFromHover, filterScratch, output))
            return false;

        displayRegion = output;
        return displayRegion.Count > 0;
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

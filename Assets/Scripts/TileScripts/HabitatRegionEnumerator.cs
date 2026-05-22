using System;
using System.Collections.Generic;
using Tile = TileGrid.Tile;

/// <summary>
/// Pre-allocated scratch buffers shared between BFS ball collection and DFS region enumeration.
/// Allocate once (per consumer), reuse every frame — zero per-call heap allocs.
/// </summary>
public sealed class HabitatRegionScratch
{
    // BFS — list used as sliding window (head index replaces Queue dequeue alloc).
    public readonly List<Tile> BfsTiles = new();
    public readonly List<int>  BfsDist  = new();

    // DFS region enumeration
    public readonly HashSet<int>     RegionHashes    = new();
    public readonly HashSet<Tile>    RegionSet       = new();
    public readonly List<Tile>       RegionList      = new();
    public readonly List<Tile>       NeighborScratch = new();

    // Pre-allocated per-depth frontier buffers — depth ≤ maxSize (typically 5).
    public readonly List<List<Tile>> FrontierStack   = new()
        { new(), new(), new(), new(), new(), new(), new(), new() };

    // Reused across outer loops
    public readonly HashSet<Tile> Ball    = new();
    public readonly HashSet<Tile> Allowed = new();
    public readonly List<Tile>    Seeds   = new();
}

/// <summary>
/// Shared BFS + DFS region enumeration utilities.
/// All hot paths are zero-alloc when using the provided HabitatRegionScratch.
/// </summary>
public static class HabitatRegionEnumerator
{
    private static readonly Comparison<Tile> TileOrder = CompareTiles;

    // -------------------------------------------------------------------------
    // BFS ball
    // -------------------------------------------------------------------------

    /// <summary>
    /// Collects occupied tiles reachable within <paramref name="maxSteps"/> edges of <paramref name="center"/>.
    /// Result is stored in <c>s.Ball</c>. Uses the list-as-queue pattern — no Queue alloc.
    /// <para>If <paramref name="includeCenterRegardlessOfOccupied"/> is true, center is always added
    /// (useful for hover preview where the hovered tile is not yet occupied).</para>
    /// </summary>
    public static void CollectOccupiedBall(
        TileRuntimeStore store, Tile center, int maxSteps,
        HabitatRegionScratch s, bool includeCenterRegardlessOfOccupied = false)
    {
        s.Ball.Clear();
        s.BfsTiles.Clear();
        s.BfsDist.Clear();

        if (center == null) return;

        s.Ball.Add(center);
        s.BfsTiles.Add(center);
        s.BfsDist.Add(0);

        for (int head = 0; head < s.BfsTiles.Count; head++)
        {
            Tile t    = s.BfsTiles[head];
            int  dist = s.BfsDist[head];
            if (dist >= maxSteps) continue;

            foreach (Tile n in GetNeighborsSafe(t))
            {
                if (n == null || s.Ball.Contains(n)) continue;
                var rn = store.Get(n);
                if (rn == null || !rn.occupied) continue;
                s.Ball.Add(n);
                s.BfsTiles.Add(n);
                s.BfsDist.Add(dist + 1);
            }
        }

        // If center is not occupied and we don't want it, remove it.
        if (!includeCenterRegardlessOfOccupied)
        {
            var rc = store.Get(center);
            if (rc == null || !rc.occupied)
                s.Ball.Remove(center);
        }
    }

    // -------------------------------------------------------------------------
    // DFS region enumeration
    // -------------------------------------------------------------------------

    /// <summary>
    /// Enumerates every connected subset of <paramref name="allowed"/> (size ≤ <paramref name="maxSize"/>)
    /// that contains <paramref name="mustInclude"/> (pass null to enumerate all).
    /// Deduplicates by integer hash — no string allocations.
    /// Uses pre-allocated FrontierStack — no List allocations during recursion.
    /// The <paramref name="onRegion"/> callback receives <c>s.RegionList</c> — a shared buffer
    /// valid only for the duration of the call; copy if you need to retain it.
    /// </summary>
    public static void ForEachRegionContaining(
        HashSet<Tile> allowed, int maxSize, Tile mustInclude,
        HabitatRegionScratch s, Action<List<Tile>> onRegion)
    {
        s.RegionHashes.Clear();

        s.Seeds.Clear();
        foreach (Tile t in allowed) s.Seeds.Add(t);
        s.Seeds.Sort(TileOrder);

        foreach (Tile seed in s.Seeds)
        {
            s.RegionSet.Clear();
            s.RegionSet.Add(seed);
            ExtendRegion(allowed, maxSize, mustInclude, s, onRegion, depth: 0);
        }
    }

    private static void ExtendRegion(
        HashSet<Tile> allowed, int maxSize, Tile mustInclude,
        HabitatRegionScratch s, Action<List<Tile>> onRegion, int depth)
    {
        if (mustInclude == null || s.RegionSet.Contains(mustInclude))
            TryEmitRegion(s, onRegion);

        if (s.RegionSet.Count >= maxSize) return;

        // Collect boundary neighbors without allocating
        s.NeighborScratch.Clear();
        foreach (Tile t in s.RegionSet)
        {
            foreach (Tile n in GetNeighborsSafe(t))
            {
                if (n == null || !allowed.Contains(n) || s.RegionSet.Contains(n)) continue;
                if (!s.NeighborScratch.Contains(n)) s.NeighborScratch.Add(n);
            }
        }
        s.NeighborScratch.Sort(TileOrder);

        // Pre-allocated frontier buffer at this depth — no new List<>
        while (s.FrontierStack.Count <= depth) s.FrontierStack.Add(new List<Tile>());
        List<Tile> frontier = s.FrontierStack[depth];
        frontier.Clear();
        frontier.AddRange(s.NeighborScratch);

        foreach (Tile n in frontier)
        {
            s.RegionSet.Add(n);
            ExtendRegion(allowed, maxSize, mustInclude, s, onRegion, depth + 1);
            s.RegionSet.Remove(n);
        }
    }

    private static void TryEmitRegion(HabitatRegionScratch s, Action<List<Tile>> onRegion)
    {
        s.RegionList.Clear();
        foreach (Tile t in s.RegionSet) s.RegionList.Add(t);
        s.RegionList.Sort(TileOrder);

        int hash = ComputeHash(s.RegionList);
        if (!s.RegionHashes.Add(hash)) return;

        onRegion?.Invoke(s.RegionList);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    public static IEnumerable<Tile> GetNeighborsSafe(Tile t)
    {
        try   { return t?.GetNeighbors() ?? Array.Empty<Tile>(); }
        catch { return Array.Empty<Tile>(); }
    }

    private static int ComputeHash(List<Tile> sorted)
    {
        unchecked
        {
            int h = 17;
            for (int i = 0; i < sorted.Count; i++)
            {
                Tile t = sorted[i];
                if (t != null) h = h * 31 + (t.q * 73856093 ^ t.r * 19349663);
            }
            return h;
        }
    }

    private static int CompareTiles(Tile a, Tile b)
    {
        if (a == null || b == null) return 0;
        int c = a.q.CompareTo(b.q);
        return c != 0 ? c : a.r.CompareTo(b.r);
    }
}

using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Wspólna geometria obrysu regionu kafli (granice heksów) — używana przez
/// <see cref="HabitatOutlineVisualizer"/> (hover) i podświetlenie kandydatów przy hoverze ikon.
/// </summary>
public static class HabitatRegionOutlineUtility
{
    public static void CollectBoundaryEdges(
        IReadOnlyList<Tile> region,
        HashSet<Tile> regionSet,
        float lineHeightOffset,
        float layerYOffset,
        float lineInset,
        List<(Vector3 a, Vector3 b)> edges)
    {
        if (region == null || regionSet == null || edges == null)
            return;

        edges.Clear();
        float y = 1f;
        bool ySet = false;

        foreach (var tile in region)
        {
            if (tile == null) continue;
            if (!ySet)
            {
                y = tile.worldPos.y + lineHeightOffset + layerYOffset;
                ySet = true;
            }

            var neighbors = tile.GetNeighbors();
            if (neighbors == null) continue;
            foreach (var neighbor in neighbors)
            {
                if (neighbor == null || regionSet.Contains(neighbor)) continue;

                if (TryComputeEdgeSegment(tile, neighbor, y, lineInset, out var a, out var b))
                    edges.Add((a, b));
            }
        }
    }

    /// <summary>
    /// Endpoints of the hex edge shared between two adjacent tiles (inset slightly inward along
    /// the tile-to-neighbor direction). Shared by boundary-outline drawing above and by any other
    /// system that wants to highlight a specific tile-to-tile edge (e.g. SameBiomeConnectionAnimator).
    /// </summary>
    public static bool TryComputeEdgeSegment(
        Tile tile, Tile neighbor, float y, float lineInset, out Vector3 a, out Vector3 b)
    {
        a = b = default;
        if (tile == null || neighbor == null) return false;

        var toNeighbor = neighbor.worldPos - tile.worldPos;
        var dist = new Vector3(toNeighbor.x, 0f, toNeighbor.z).magnitude;
        if (dist < 0.001f) return false;

        var mid = tile.worldPos + toNeighbor * 0.5f;
        mid.y = y;
        var dirXZ = new Vector3(toNeighbor.x, 0f, toNeighbor.z).normalized;
        var perp = new Vector3(dirXZ.z, 0f, -dirXZ.x);
        var halfEdge = dist / (2f * Mathf.Sqrt(3f));
        var inward = -dirXZ * lineInset;
        a = mid + perp * halfEdge + inward;
        a.y = y;
        b = mid - perp * halfEdge + inward;
        b.y = y;
        return true;
    }
}

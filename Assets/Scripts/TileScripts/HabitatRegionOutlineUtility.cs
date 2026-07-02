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

                var toNeighbor = neighbor.worldPos - tile.worldPos;
                var dist = new Vector3(toNeighbor.x, 0f, toNeighbor.z).magnitude;
                if (dist < 0.001f) continue;

                var mid = tile.worldPos + toNeighbor * 0.5f;
                mid.y = y;
                var dirXZ = new Vector3(toNeighbor.x, 0f, toNeighbor.z).normalized;
                var perp = new Vector3(dirXZ.z, 0f, -dirXZ.x);
                var halfEdge = dist / (2f * Mathf.Sqrt(3f));
                var inward = -dirXZ * lineInset;
                var c1 = mid + perp * halfEdge + inward;
                c1.y = y;
                var c2 = mid - perp * halfEdge + inward;
                c2.y = y;
                edges.Add((c1, c2));
            }
        }
    }
}

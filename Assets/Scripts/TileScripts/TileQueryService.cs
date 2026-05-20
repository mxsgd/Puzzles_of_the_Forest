using UnityEngine;
using Tile = TileGrid.Tile;

public class TileQueryService : MonoBehaviour
{
    [SerializeField] private TileGrid grid;
    [SerializeField] private TileRuntimeStore runtime;

    public bool TryGetNearestTile(Vector3 worldPoint, out Tile tile, float maxDistance = Mathf.Infinity)
    {
        tile = null;
        if (grid == null)
            return false;
        return grid.TryGetNearestTile(worldPoint, out tile, maxDistance);
    }

    public bool TryGetNearestFreeTile(Vector3 worldPoint, out Tile tile, float maxDistance = Mathf.Infinity)
    {
        tile = null;
        if (grid == null || runtime == null)
            return false;

        if (!grid.TryGetNearestTile(worldPoint, out Tile nearest, maxDistance))
            return false;

        if (!runtime.Get(nearest).occupied)
        {
            tile = nearest;
            return true;
        }

        // Kandydat zajęty — sprawdź sąsiadów (max 6), nie całą siatkę.
        float maxSqr = maxDistance * maxDistance;
        Tile best = null;
        float bestSqr = maxSqr;

        foreach (Tile n in grid.GetNeighbors(nearest))
        {
            if (n == null || runtime.Get(n).occupied)
                continue;
            float sqr = (n.worldPos - worldPoint).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = n;
            }
        }

        if (best == null)
            return false;

        tile = best;
        return true;
    }
}

using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Per-tile foam edge mask for water shader (_FoamEdgeMask / _FoamEdgeMaskB).
/// Foam appears only on the top face, only near hex edges that border non-water tiles.
/// </summary>
public static class WaterFoamMaskUtility
{
    private static readonly Vector2Int[] AxialDirections =
    {
        new(1, 0), new(1, -1), new(0, -1),
        new(-1, 0), new(-1, 1), new(0, 1)
    };

    private static readonly int FoamEdgeMaskId = Shader.PropertyToID("_FoamEdgeMask");
    private static readonly int FoamEdgeMaskBId = Shader.PropertyToID("_FoamEdgeMaskB");
    private static readonly int FoamHexRadiusId = Shader.PropertyToID("_FoamHexRadius");
    private static readonly int FoamWidthId = Shader.PropertyToID("_FoamWidth");

    private const float FoamBandFraction = 0.28f;

    private static MaterialPropertyBlock _block;

    public static void RefreshAround(Tile tile, TileRuntimeStore store, TileGrid grid)
    {
        if (tile == null || store == null || grid == null)
            return;

        Apply(tile, store, grid);
        foreach (var neighbor in tile.GetNeighbors())
            Apply(neighbor, store, grid);
    }

    public static void RefreshAllWater(TileRuntimeStore store, TileGrid grid)
    {
        if (store == null || grid == null)
            return;

        foreach (var tile in grid.tiles)
        {
            if (tile == null)
                continue;

            var runtime = store.Get(tile);
            if (runtime is { occupied: true, biome: TileBiome.Water })
                Apply(tile, store, grid);
        }
    }

    public static void Apply(Tile tile, TileRuntimeStore store, TileGrid grid)
    {
        if (tile == null || store == null || grid == null)
            return;

        var runtime = store.Get(tile);
        if (runtime is not { occupied: true, biome: TileBiome.Water })
            return;

        var instance = runtime.occupantInstance;
        if (instance == null)
            return;

        var renderer = instance.GetComponentInChildren<Renderer>();
        if (renderer == null)
            return;

        ComputeEdgeMask(tile, store, grid, out var mask0, out var mask1);
        var hexRadiusOs = GetObjectSpaceHexRadius(renderer);

        _block ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(_block);
        _block.SetVector(FoamEdgeMaskId, mask0);
        _block.SetVector(FoamEdgeMaskBId, new Vector4(mask1.x, mask1.y, 0f, 0f));
        _block.SetFloat(FoamHexRadiusId, hexRadiusOs);
        _block.SetFloat(FoamWidthId, Mathf.Max(0.02f, hexRadiusOs * FoamBandFraction));
        renderer.SetPropertyBlock(_block);
    }

    private static float GetObjectSpaceHexRadius(Renderer renderer)
    {
        var mf = renderer.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return 0.5f;

        var extents = mf.sharedMesh.bounds.extents;
        return Mathf.Max(extents.x, extents.z);
    }

    private static void ComputeEdgeMask(Tile tile, TileRuntimeStore store, TileGrid grid, out Vector4 mask0, out Vector2 mask1)
    {
        mask0 = Vector4.zero;
        mask1 = Vector2.zero;

        for (int d = 0; d < AxialDirections.Length; d++)
        {
            var dir = AxialDirections[d];
            var key = new Vector2Int(tile.q + dir.x, tile.r + dir.y);
            float edgeValue = 1f;

            if (grid.TryGetTileAtAxial(key.x, key.y, out var neighbor))
            {
                var neighborRuntime = store.Get(neighbor);
                if (neighborRuntime is { occupied: true, biome: TileBiome.Water })
                    edgeValue = 0f;
            }

            switch (d)
            {
                case 0: mask0.x = edgeValue; break;
                case 1: mask0.y = edgeValue; break;
                case 2: mask0.z = edgeValue; break;
                case 3: mask0.w = edgeValue; break;
                case 4: mask1.x = edgeValue; break;
                case 5: mask1.y = edgeValue; break;
            }
        }
    }
}

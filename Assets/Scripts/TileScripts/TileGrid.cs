using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(Collider))]
public class TileGrid : MonoBehaviour
{
    [Header("Grid")]
    [Min(1)] public int rows = 200;
    [Min(1)] public int cols = 200;
    public bool rebuildOnValidate = true;

    [Header("Rozmiar heksu")]
    [Tooltip("Gdy włączone — kafle używają wymuszonego circumradius (R) zamiast skali z bounds meshu GridPlane.")]
    public bool useFixedHexRadius = true;
    [Min(0.01f), Tooltip("Wymuszony circumradius w metrach. R = 5 ⇒ 2R = 10 m.")]
    public float fixedHexRadius = 5f;
    [Tooltip("Po przebudowie siatki automatycznie skaluj transform tego obiektu tak, żeby mesh GridPlane pokrył obszar wszystkich kafli.")]
    public bool autoFitGroundPlane = true;

    [System.Serializable]
    public class Tile
    {
        public int i, j;
        public int q, r;
        public Vector3 worldPos;
        [System.NonSerialized] public TileGrid grid;

        public IEnumerable<Tile> GetNeighbors() => grid?.GetNeighbors(this);
    }

    public List<Tile> tiles = new List<Tile>();
    private Tile[,] _grid;
    private readonly Dictionary<Vector2Int, Tile> _axialLookup = new Dictionary<Vector2Int, Tile>();
    private Tile _centerTile;
    private static readonly Vector2Int[] AxialDirections =
    {
        new(1, 0), new(1, -1), new(0, -1),
        new(-1, 0), new(-1, 1), new(0, 1)
    };

    private float _hexScale = 1f;
    private Bounds _localBounds;

    void OnEnable() { CacheBounds(); BuildGrid(); }

    void OnValidate()
    {
        if (!Application.isPlaying && rebuildOnValidate) { CacheBounds(); BuildGrid(); }
    }

    void CacheBounds()
    {
        var mf = GetComponent<MeshFilter>();
        _localBounds = mf && mf.sharedMesh ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
    }

    public void BuildGrid()
    {
        tiles.Clear();
        _axialLookup.Clear();
        _centerTile = null;

        if (rows < 1 || cols < 1) return;
        _grid = new Tile[rows, cols];

        _hexScale = useFixedHexRadius && fixedHexRadius > 0.0001f
            ? fixedHexRadius
            : ComputeBoundsBasedScale();

        if (autoFitGroundPlane)
            FitGroundPlaneToGrid();

        Vector3 origin = transform.position;
        int q0 = Mathf.RoundToInt((cols - 1) * 0.5f);
        int r0 = Mathf.RoundToInt((rows - 1) * 0.5f);
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
            {
                int axialQ = j - q0;
                int axialR = i - r0;
                float xR1 = Mathf.Sqrt(3f) * (axialQ + axialR * 0.5f);
                float zR1 = 1.5f * axialR;
                Vector3 worldCenter = origin + new Vector3(xR1 * _hexScale, 0f, zR1 * _hexScale);

                Vector3 rayStart = worldCenter + Vector3.up * 5f;
                if (Physics.Raycast(rayStart, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                    worldCenter = hit.point;

                var t = new Tile
                {
                    i = i,
                    j = j,
                    q = axialQ,
                    r = axialR,
                    worldPos = worldCenter,
                    grid = this
                };

                tiles.Add(t);
                _grid[i, j] = t;
                _axialLookup[new Vector2Int(t.q, t.r)] = t;

                float sqr = (worldCenter - origin).sqrMagnitude;
                if (sqr < bestDistance)
                {
                    bestDistance = sqr;
                    _centerTile = t;
                }
            }
    }

    private float ComputeBoundsBasedScale()
    {
        var sizeLocal = _localBounds.size;
        float widthR1 = Mathf.Sqrt(3f) * (cols + 0.5f);
        float depthR1 = 1.5f * (rows - 1) + 2f;
        if (sizeLocal.x <= 0.0001f || sizeLocal.z <= 0.0001f || widthR1 <= 0.0001f || depthR1 <= 0.0001f)
            return 1f;
        return Mathf.Min(sizeLocal.x / widthR1, sizeLocal.z / depthR1);
    }

    [ContextMenu("Fit Ground Plane To Grid")]
    private void FitGroundPlaneContext()
    {
        CacheBounds();
        if (useFixedHexRadius && fixedHexRadius > 0.0001f) _hexScale = fixedHexRadius;
        else _hexScale = ComputeBoundsBasedScale();
        FitGroundPlaneToGrid();
    }

    private void FitGroundPlaneToGrid()
    {
        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        var meshSize = mf.sharedMesh.bounds.size;
        if (meshSize.x <= 0.0001f || meshSize.z <= 0.0001f) return;

        float worldX = Mathf.Sqrt(3f) * (cols + 0.5f) * _hexScale;
        float worldZ = (1.5f * (rows - 1) + 2f) * _hexScale;

        var ls = transform.localScale;
        ls.x = worldX / meshSize.x;
        ls.z = worldZ / meshSize.z;
        transform.localScale = ls;
    }
    /// <summary>
    /// Promień heksu (circumradius) w world-units — przeskalowany przez bounds meshu.
    /// Używany do liczenia pozycji slotów-trójkątów wewnątrz kafla.
    /// </summary>
    public float HexRadius => _hexScale;

    public Tile GetTile(int i, int j) => (i >= 0 && i < rows && j >= 0 && j < cols) ? _grid[i, j] : null;

    public IEnumerable<Tile> GetNeighbors(Tile tile)
    {
        if (tile == null)
            yield break;

        foreach (var dir in AxialDirections)
        {
            var key = new Vector2Int(tile.q + dir.x, tile.r + dir.y);
            if (_axialLookup.TryGetValue(key, out var neighbor))
                yield return neighbor;
        }
    }
    public Tile GetCenterTile() => _centerTile;
}


using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Rysuje otoczkę (obrys) wzdłuż granic kafli dla każdego przypisanego habitatu.
/// Odświeża się po każdym TileStateChanged (w tym po przypisaniu habitatu).
/// </summary>
public class HabitatOutlineVisualizer : MonoBehaviour
{
    [SerializeField] private TileGrid grid;
    [SerializeField] private TileRuntimeStore runtimeStore;

    [Header("Obrys")]
    [SerializeField] private float lineHeightOffset = 0.05f;
    [SerializeField] private float lineWidth = 0.15f;
    [Tooltip("Opcjonalnie: materiał dla linii (np. URP Unlit). Jeśli puste, używany jest prosty Unlit/Color.")]
    [SerializeField] private Material lineMaterialTemplate;
    [SerializeField] private Color deerColor = new Color(0.6f, 0.4f, 0.2f);
    [SerializeField] private Color beaverColor = new Color(0.3f, 0.5f, 0.6f);
    [SerializeField] private Color bearColor = new Color(0.25f, 0.25f, 0.3f);

    [SerializeField] private Transform outlinesParent;
    private readonly List<GameObject> _outlineRoots = new List<GameObject>();

    private void Awake()
    {
        if (!grid) grid = FindAnyObjectByType<TileGrid>();
        if (!runtimeStore) runtimeStore = FindAnyObjectByType<TileRuntimeStore>();
        if (!outlinesParent) outlinesParent = transform;
    }

    private void OnEnable()
    {
        TileEvents.TileStateChanged += OnTileStateChanged;
        RefreshAllOutlines();
    }

    private void OnDisable()
    {
        TileEvents.TileStateChanged -= OnTileStateChanged;
        ClearOutlines();
    }

    private void OnTileStateChanged(Tile tile)
    {
        RefreshAllOutlines();
    }

    private void ClearOutlines()
    {
        foreach (var go in _outlineRoots)
        {
            if (go != null) Destroy(go);
        }
        _outlineRoots.Clear();
    }

    private void RefreshAllOutlines()
    {
        if (runtimeStore == null || grid == null) return;
        ClearOutlines();

        var groups = runtimeStore.GetHabitatGroups();
        foreach (var (habitatId, animal, tiles) in groups)
        {
            if (tiles == null || tiles.Count == 0) continue;
            var regionSet = new HashSet<Tile>(tiles);
            var edges = GetBoundaryEdges(tiles, regionSet);
            if (edges.Count == 0) continue;
            var color = GetColorForAnimal(animal);
            CreateOutline(edges, color);
        }
    }

    private Color GetColorForAnimal(HabitatAnimal animal)
    {
        return animal switch
        {
            HabitatAnimal.Deer => deerColor,
            HabitatAnimal.Beaver => beaverColor,
            HabitatAnimal.Bear => bearColor,
            _ => Color.gray
        };
    }

    /// <summary>Zwraca listę krawędzi granicznych (dwa punkty świata) dla regionu hexów.</summary>
    private List<(Vector3 a, Vector3 b)> GetBoundaryEdges(IReadOnlyList<Tile> region, HashSet<Tile> regionSet)
    {
        var edges = new List<(Vector3, Vector3)>();
        float y = 1f;
        bool ySet = false;

        foreach (var tile in region)
        {
            if (tile == null) continue;
            if (!ySet) { y = tile.worldPos.y + lineHeightOffset; ySet = true; }

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
                var corner1 = mid + perp * halfEdge;
                var corner2 = mid - perp * halfEdge;
                corner1.y = y;
                corner2.y = y;
                edges.Add((corner1, corner2));
            }
        }
        return edges;
    }

    private void CreateOutline(List<(Vector3 a, Vector3 b)> edges, Color color)
    {
        var root = new GameObject("HabitatOutline");
        root.transform.SetParent(outlinesParent, false);

        foreach (var (a, b) in edges)
        {
            var seg = new GameObject("Segment");
            seg.transform.SetParent(root.transform, false);
            var lr = seg.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth * 0.8f;
            if (lineMaterialTemplate != null)
                lr.material = new Material(lineMaterialTemplate) { color = color };
            else
            {
                var sh = Shader.Find("Unlit/Color");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                lr.material = sh != null ? new Material(sh) { color = color } : new Material(Shader.Find("Legacy Shaders/Diffuse")) { color = color };
            }
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 0;
        }

        _outlineRoots.Add(root);
    }
}

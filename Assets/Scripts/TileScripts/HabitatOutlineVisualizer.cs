using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Rysuje otoczkę (obrys) wzdłuż granic kafli dla każdego habitatu.
///
/// Optymalizacje vs. oryginał:
/// - Jeden material per typ zwierzęcia (stworzony raz w Awake) — zero new Material() per refresh.
/// - Pula LineRendererów (SetActive zamiast Destroy+new GameObject) — zero alokacji per refresh.
/// - RefreshAllOutlines: deaktywuje nadmiarowe segmenty zamiast je niszczyć.
/// </summary>
public class HabitatOutlineVisualizer : MonoBehaviour
{
    [SerializeField] private TileGrid grid;
    [SerializeField] private TileRuntimeStore runtimeStore;

    [Header("Obrys")]
    [SerializeField] private float lineHeightOffset   = 0.05f;
    [SerializeField, Min(0f)] private float perHabitatHeightStep = 0.02f;
    [SerializeField, Min(0f)] private float lineInset  = 0.08f;
    [SerializeField] private float lineWidth           = 0.15f;
    [SerializeField] private Material lineMaterialTemplate;

    [SerializeField] private Color deerColor        = new Color(0.6f,  0.4f,  0.2f);
    [SerializeField] private Color beaverColor      = new Color(0.3f,  0.5f,  0.6f);
    [SerializeField] private Color bearColor        = new Color(0.25f, 0.25f, 0.3f);
    [SerializeField] private Color beesColor        = new Color(0.95f, 0.85f, 0.2f);
    [SerializeField] private Color rockDwellerColor = new Color(0.55f, 0.45f, 0.4f);

    [SerializeField] private Transform outlinesParent;

    // Segment pool — GameObjects are never destroyed, only activated/deactivated.
    private readonly List<(GameObject go, LineRenderer lr)> _segmentPool = new();
    private readonly List<(Vector3 a, Vector3 b)> _edgeScratch = new();
    private int _activeSegmentCount;

    // One material per animal type — created once in Awake, reused on every refresh.
    private readonly Dictionary<HabitatAnimal, Material> _materialByAnimal = new();

    private void Awake()
    {
        if (!grid)          grid          = FindAnyObjectByType<TileGrid>();
        if (!runtimeStore)  runtimeStore  = FindAnyObjectByType<TileRuntimeStore>();
        if (!outlinesParent) outlinesParent = transform;

        BuildAnimalMaterials();
    }

    private void BuildAnimalMaterials()
    {
        _materialByAnimal.Clear();
        foreach (HabitatAnimal animal in System.Enum.GetValues(typeof(HabitatAnimal)))
        {
            Material mat;
            if (lineMaterialTemplate != null)
            {
                mat = new Material(lineMaterialTemplate) { color = GetColorForAnimal(animal) };
            }
            else
            {
                var sh = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                mat = sh != null ? new Material(sh) { color = GetColorForAnimal(animal) }
                                 : new Material(Shader.Find("Legacy Shaders/Diffuse")) { color = GetColorForAnimal(animal) };
            }
            mat.name = $"OutlineMat_{animal}";
            _materialByAnimal[animal] = mat;
        }
    }

    private void OnEnable()
    {
        TileEvents.TileStateChanged += OnTileStateChanged;
        TileEvents.HabitatAssigned  += OnHabitatAssigned;
        RefreshAllOutlines();
    }

    private void OnDisable()
    {
        TileEvents.TileStateChanged -= OnTileStateChanged;
        TileEvents.HabitatAssigned  -= OnHabitatAssigned;
        DeactivateAllSegments();
    }

    private void OnDestroy()
    {
        // Materials created in Awake must be freed.
        foreach (var mat in _materialByAnimal.Values)
            if (mat != null) Destroy(mat);
        _materialByAnimal.Clear();
    }

    private void OnTileStateChanged(Tile _) => RefreshAllOutlines();
    private void OnHabitatAssigned(HabitatAssignmentData _) => RefreshAllOutlines();

    // -------------------------------------------------------------------------

    private void RefreshAllOutlines()
    {
        if (runtimeStore == null || grid == null) return;

        DeactivateAllSegments();
        _activeSegmentCount = 0;

        var groups = runtimeStore.GetHabitatGroups();
        groups.Sort((a, b) => a.habitatId.CompareTo(b.habitatId));

        int layerIndex = 0;
        foreach (var (_, animal, tiles) in groups)
        {
            if (tiles == null || tiles.Count == 0) continue;

            var regionSet    = new HashSet<Tile>(tiles);
            float layerYOff  = perHabitatHeightStep * layerIndex;

            _materialByAnimal.TryGetValue(animal, out var mat);

            _edgeScratch.Clear();
            HabitatRegionOutlineUtility.CollectBoundaryEdges(
                tiles, regionSet, lineHeightOffset, layerYOff, lineInset, _edgeScratch);

            foreach (var (a, b) in _edgeScratch)
            {
                var (go, lr) = GetOrCreateSegment(_activeSegmentCount);
                lr.SetPosition(0, a);
                lr.SetPosition(1, b);
                lr.material     = mat;
                lr.sortingOrder = layerIndex;
                go.SetActive(true);
                _activeSegmentCount++;
            }

            layerIndex++;
        }
    }

    private void DeactivateAllSegments()
    {
        for (int i = 0; i < _activeSegmentCount; i++)
            if (i < _segmentPool.Count) _segmentPool[i].go.SetActive(false);
        _activeSegmentCount = 0;
    }

    // Returns an existing (inactive) pooled segment or creates a new one.
    private (GameObject go, LineRenderer lr) GetOrCreateSegment(int idx)
    {
        if (idx < _segmentPool.Count)
            return _segmentPool[idx];

        var go = new GameObject("OutlineSegment");
        go.transform.SetParent(outlinesParent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount    = 2;
        lr.startWidth       = lineWidth;
        lr.endWidth         = lineWidth * 0.8f;
        lr.useWorldSpace    = true;
        lr.numCapVertices   = 2;
        lr.numCornerVertices = 0;
        go.SetActive(false);

        var entry = (go, lr);
        _segmentPool.Add(entry);
        return entry;
    }

    private Color GetColorForAnimal(HabitatAnimal animal) => animal switch
    {
        HabitatAnimal.Deer        => deerColor,
        HabitatAnimal.Beaver      => beaverColor,
        HabitatAnimal.Bear        => bearColor,
        HabitatAnimal.Bees        => beesColor,
        HabitatAnimal.RockDweller => rockDwellerColor,
        _                         => Color.gray
    };
}

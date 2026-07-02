using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Tile = TileGrid.Tile;

/// <summary>
/// Obrys habitatu — widoczny tylko przy najechaniu.
/// Połączone sub-habitaty tego samego zwierzęcia = jeden spójny obrys wokół całego regionu.
/// </summary>
public class HabitatOutlineVisualizer : MonoBehaviour
{
    [SerializeField] private TileGrid grid;
    [SerializeField] private TileRuntimeStore runtimeStore;
    [SerializeField] private TileQueryService query;
    [SerializeField] private Camera mainCamera;

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

    [SerializeField] private Transform outlinesParent;

    [Header("Hover")]
    [SerializeField] private float maxRayDistance = 500f;
    [SerializeField] private LayerMask groundMask = ~0;

    private readonly List<(GameObject go, LineRenderer lr)> _segmentPool = new();
    private readonly List<(Vector3 a, Vector3 b)> _edgeScratch = new();
    private int _activeSegmentCount;

    private readonly Dictionary<HabitatAnimal, Material> _materialByAnimal = new();

    private int _hoveredVisualGroupId = -1;
    private bool _needsRefresh;

    private void Awake()
    {
        if (!grid)          grid          = FindAnyObjectByType<TileGrid>();
        if (!runtimeStore)  runtimeStore  = FindAnyObjectByType<TileRuntimeStore>();
        if (!query)         query         = FindAnyObjectByType<TileQueryService>();
        if (!mainCamera)    mainCamera    = Camera.main;
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
        TileEvents.HabitatMerged    += OnHabitatMerged;
        DeactivateAllSegments();
        _hoveredVisualGroupId = -1;
    }

    private void OnDisable()
    {
        TileEvents.TileStateChanged -= OnTileStateChanged;
        TileEvents.HabitatAssigned  -= OnHabitatAssigned;
        TileEvents.HabitatMerged    -= OnHabitatMerged;
        DeactivateAllSegments();
        _hoveredVisualGroupId = -1;
    }

    private void OnDestroy()
    {
        foreach (var mat in _materialByAnimal.Values)
            if (mat != null) Destroy(mat);
        _materialByAnimal.Clear();
    }

    private void LateUpdate()
    {
        if (!isActiveAndEnabled || runtimeStore == null || query == null)
            return;

        if (!TryGetHoveredHabitatId(out int habitatId))
        {
            if (_hoveredVisualGroupId >= 0)
            {
                DeactivateAllSegments();
                _hoveredVisualGroupId = -1;
            }
            return;
        }

        int groupId = runtimeStore.GetGroupLeader(habitatId);
        if (groupId != _hoveredVisualGroupId || _needsRefresh)
        {
            RefreshOutlineForVisualGroup(habitatId);
            _hoveredVisualGroupId = groupId;
            _needsRefresh = false;
        }
    }

    private void OnTileStateChanged(Tile _) => MarkDirtyIfHovering();
    private void OnHabitatAssigned(HabitatAssignmentData _) => MarkDirtyIfHovering();
    private void OnHabitatMerged(HabitatMergeData _) => MarkDirtyIfHovering();

    private void MarkDirtyIfHovering()
    {
        if (_hoveredVisualGroupId >= 0)
            _needsRefresh = true;
    }

    private bool TryGetHoveredHabitatId(out int habitatId)
    {
        habitatId = -1;

        if (!TryGetPointerScreen(out var screen, out var pointerId))
            return false;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId))
            return false;

        var cam = mainCamera != null ? mainCamera : Camera.main;
        if (cam == null)
            return false;

        var ray = cam.ScreenPointToRay(screen);
        if (!Physics.Raycast(ray, out var hit, maxRayDistance, groundMask, QueryTriggerInteraction.Ignore))
            return false;

        if (!query.TryGetNearestTile(hit.point, out var tile, maxRayDistance))
            return false;

        var rt = runtimeStore.Get(tile);
        if (rt == null || !rt.occupied || rt.habitatId < 0)
            return false;

        habitatId = rt.habitatId;
        return true;
    }

    private void RefreshOutlineForVisualGroup(int habitatId)
    {
        DeactivateAllSegments();
        _activeSegmentCount = 0;

        if (!runtimeStore.TryGetVisualHabitatGroup(habitatId, out _, out var animal, out var tiles))
            return;

        if (tiles == null || tiles.Count == 0)
            return;

        var regionSet = new HashSet<Tile>(tiles);
        _materialByAnimal.TryGetValue(animal, out var mat);

        _edgeScratch.Clear();
        HabitatRegionOutlineUtility.CollectBoundaryEdges(
            tiles, regionSet, lineHeightOffset, 0f, lineInset, _edgeScratch);

        for (int e = 0; e < _edgeScratch.Count; e++)
        {
            var (a, b) = _edgeScratch[e];
            var (go, lr) = GetOrCreateSegment(_activeSegmentCount);
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.material = mat;
            lr.sortingOrder = 0;
            go.SetActive(true);
            _activeSegmentCount++;
        }
    }

    private void DeactivateAllSegments()
    {
        for (int i = 0; i < _activeSegmentCount; i++)
        {
            if (i < _segmentPool.Count)
                _segmentPool[i].go.SetActive(false);
        }
        _activeSegmentCount = 0;
    }

    /// <summary>Usuwa nieaktywne segmenty ponad limit — pula inaczej rośnie bez końca.</summary>
    public void TrimInactiveSegmentPool(int maxPoolSize)
    {
        maxPoolSize = Mathf.Max(_activeSegmentCount, maxPoolSize);
        while (_segmentPool.Count > maxPoolSize)
        {
            int last = _segmentPool.Count - 1;
            var entry = _segmentPool[last];
            _segmentPool.RemoveAt(last);
            if (entry.go != null)
                Destroy(entry.go);
        }
    }

    private (GameObject go, LineRenderer lr) GetOrCreateSegment(int idx)
    {
        if (idx < _segmentPool.Count)
            return _segmentPool[idx];

        var go = new GameObject("OutlineSegment");
        go.transform.SetParent(outlinesParent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount     = 2;
        lr.startWidth        = lineWidth;
        lr.endWidth          = lineWidth * 0.8f;
        lr.useWorldSpace     = true;
        lr.numCapVertices    = 2;
        lr.numCornerVertices = 0;
        go.SetActive(false);

        var entry = (go, lr);
        _segmentPool.Add(entry);
        return entry;
    }

    private static bool TryGetPointerScreen(out Vector2 screen, out int pointerId)
    {
        screen = default;
        pointerId = -1;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            screen = mouse.position.ReadValue();
            return true;
        }

        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.isPressed)
        {
            screen = ts.primaryTouch.position.ReadValue();
            pointerId = ts.primaryTouch.touchId.ReadValue();
            return true;
        }

        return false;
    }

    private Color GetColorForAnimal(HabitatAnimal animal) => animal switch
    {
        HabitatAnimal.Deer        => deerColor,
        HabitatAnimal.Beaver      => beaverColor,
        HabitatAnimal.Bear        => bearColor,
        HabitatAnimal.Bees        => beesColor,
        _                         => Color.gray
    };
}

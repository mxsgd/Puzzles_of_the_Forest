using System.Collections;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Koordynuje zwalnianie pul GPU w Play Mode (linie, ghosty, availability, partikle).
/// Ogranicza wzrost pamięci współdzielonej DXGI przy D3D12 w edytorze.
/// </summary>
[DisallowMultipleComponent]
public class GpuResourceBudget : MonoBehaviour
{
    public static GpuResourceBudget Instance { get; private set; }

    [Header("Częstotliwość")]
    [SerializeField, Min(1)] private int tilesBetweenLightTrim = 5;
    [SerializeField, Min(1)] private int habitatsBetweenHeavyTrim = 2;

    [Header("Limity pul")]
    [SerializeField, Min(8)] private int maxOutlineSegmentPool = 96;
    [SerializeField, Min(8)] private int maxCandidateLinePool = 48;
    [SerializeField, Min(4)] private int maxAvailabilityPooledPerPrefab = 20;
    [SerializeField, Min(1)] private int maxGhostPoolEntriesAfterTrim = 2;

    [Header("Unload")]
    [SerializeField, Min(1f)] private float unloadAssetsCooldownSeconds = 4f;

    [SerializeField] private TilePlacementService placement;
    [SerializeField] private HabitatOutlineVisualizer outlineVisualizer;
    [SerializeField] private TileNextTileHoverPreview hoverPreview;

    private int _tilesSinceTrim;
    private int _habitatsSinceHeavyTrim;
    private float _lastUnloadTime = -999f;
    private Coroutine _unloadRoutine;

    public static GpuResourceBudget EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        Instance = FindAnyObjectByType<GpuResourceBudget>();
        if (Instance != null)
            return Instance;

        var host = GameObject.Find("_TileSystems") ?? new GameObject("GpuResourceBudget");
        Instance = host.GetComponent<GpuResourceBudget>();
        if (Instance == null)
            Instance = host.AddComponent<GpuResourceBudget>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveRefs();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        TileEvents.TilePlaced += OnTilePlaced;
        TileEvents.HabitatAssigned += OnHabitatAssigned;
    }

    private void OnDisable()
    {
        TileEvents.TilePlaced -= OnTilePlaced;
        TileEvents.HabitatAssigned -= OnHabitatAssigned;
    }

    public void ResetCounters()
    {
        _tilesSinceTrim = 0;
        _habitatsSinceHeavyTrim = 0;
    }

    private void ResolveRefs()
    {
        if (!placement) placement = FindAnyObjectByType<TilePlacementService>();
        if (!outlineVisualizer) outlineVisualizer = FindAnyObjectByType<HabitatOutlineVisualizer>();
        if (!hoverPreview) hoverPreview = FindAnyObjectByType<TileNextTileHoverPreview>();
    }

    private void OnTilePlaced(Tile tile, TileBiome biome)
    {
        _tilesSinceTrim++;
        if (_tilesSinceTrim < tilesBetweenLightTrim)
            return;

        _tilesSinceTrim = 0;
        RunLightTrim();
    }

    private void OnHabitatAssigned(HabitatAssignmentData data)
    {
        _habitatsSinceHeavyTrim++;
        RunLightTrim();

        // Nie wywołuj UnloadUnusedAssets w trakcie gry — koreluje z D3D12 device removed
        // gdy GPU wciąż renderuje meshe / animatory (patrz Editor.log).
    }

    public void RunLightTrim()
    {
        ResolveRefs();
        hoverPreview?.TrimGpuPools(maxCandidateLinePool, maxGhostPoolEntriesAfterTrim);
        outlineVisualizer?.TrimInactiveSegmentPool(maxOutlineSegmentPool);
        placement?.TrimAvailabilityPools(maxAvailabilityPooledPerPrefab);
        placement?.TrimPlacementParticlePool();
    }

    /// <summary>Pełny trim — tylko po wyjściu z Play Mode lub na żądanie z menu.</summary>
    public void RunHeavyTrim()
    {
        RunLightTrim();
        TileNextTileHoverPreview.PurgeGhostPool();
        RequestUnloadUnusedAssets();
    }

    public void RequestUnloadUnusedAssets()
    {
        if (!isActiveAndEnabled)
            return;
        if (Time.unscaledTime - _lastUnloadTime < unloadAssetsCooldownSeconds)
            return;

        _lastUnloadTime = Time.unscaledTime;
        if (_unloadRoutine != null)
            StopCoroutine(_unloadRoutine);
        _unloadRoutine = StartCoroutine(UnloadRoutine());
    }

    private IEnumerator UnloadRoutine()
    {
        var op = Resources.UnloadUnusedAssets();
        yield return op;
        System.GC.Collect();
        _unloadRoutine = null;
    }
}

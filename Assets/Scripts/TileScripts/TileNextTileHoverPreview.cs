using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Tile = TileGrid.Tile;

/// <summary>
/// Nad wolnym, legalnym polem: duch następnego kafla z talii + ikony habitatu.
///
/// Ghost pool — każdy unikalny (biom, prefab) buduje się dokładnie raz przez cały runtime.
/// Zmiana karty → SetActive(false) na starym, SetActive(true) na nowym. Zero Populate per kafel.
/// Ikony: poolowane sloty UI (tło + ikona), zero Destroy/Instantiate w runtime.
/// </summary>
[DisallowMultipleComponent]
public class TileNextTileHoverPreview : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private TileQueryService query;
    [SerializeField] private TileRuntimeStore runtimeStore;
    [SerializeField] private TileDeck tileDeck;
    [SerializeField] private TileAvailabilityService availability;
    [SerializeField] private BiomeHabitatClassifier classifier;
    [SerializeField] private TileGrid tileGrid;
    [SerializeField] private BiomeTilePopulator biomePopulator;

    [Header("Pozycja")]
    [SerializeField] private Vector3 ghostWorldOffset = new(0f, 1f, 0f);
    [SerializeField, Min(0.01f), Tooltip("Wizualna skala ducha w podglądzie (absolutna, uniform). Populate/dekoracje zawsze liczone w skali prefaba — ta wartość nie wpływa na placement.")]
    private float ghostUniformScale = 1.35f;
    [SerializeField] private float iconsAboveTile = 2.2f;
    [SerializeField, Tooltip("Dodatkowy odstęp między krawędziami slotów (px ref. 1920). Pitch = backgroundSize + iconSpacing.")]
    private float iconSpacing = 56f;
    [SerializeField, Range(0.5f, 1.25f), Tooltip("Mnożnik rozmiaru slotu (tło + ikona zwierzęcia).")]
    private float iconSizeMultiplier = 0.88f;
    [SerializeField, Min(10f), Tooltip("Tekst pod żółtą ikoną — czego brakuje do habitatu.")]
    private float deficitHintFontSize = 18f;
    [SerializeField] private Color deficitHintColor = new(1f, 0.96f, 0.75f, 0.95f);
    [SerializeField, Min(40f)] private float deficitHintWidth = 140f;
    [SerializeField, FormerlySerializedAs("deficitHintBelowSlot"), Tooltip("Odstęp tekstu nad ikoną zwierzęcia (px ref. 1920).")]
    private float deficitHintAboveSlot = 10f;

    [Header("Kolory preview habitatu")]
    [SerializeField] private Color grayBackgroundColor = new(0.55f, 0.55f, 0.55f, 0.85f);
    [SerializeField] private Color yellowBackgroundColor = new(0.96f, 0.86f, 0.35f, 0.95f);
    [SerializeField] private Color greenBackgroundColor = new(0.45f, 0.82f, 0.48f, 0.95f);
    [SerializeField] private Color iconColor = Color.white;

    [Header("Podświetlenie kandydatów (hover ikony)")]
    [SerializeField, Min(1), Tooltip("Zielona ikona: maks. kroki heks od hover do obrysu (bez dalekiego ogona).")]
    private int candidateHighlightMaxGraphSteps = 4;
    [SerializeField, Min(1), Tooltip("Żółta ikona: tylko najbliższe kafle (prawie habitat); szuka w pełnej kuli placement.")]
    private int candidateYellowHighlightMaxGraphSteps = 3;
    [SerializeField, Tooltip("Obrys regionu — LineRenderer + TileGlow.mat (jak HabitatOutlineVisualizer). Water pomijany.")]
    private Material candidateTileHighlightMaterial;
    [SerializeField] private float candidateLineHeightOffset = 2.5f;
    [SerializeField, Min(0f)] private float candidateLineInset = 0.08f;
    [SerializeField] private float candidateLineWidth = 0.3f;

    [Header("Rozmiary slotu (px ekranu)")]
    [SerializeField, Min(8f)] private float backgroundSize = 48f;
    [SerializeField, Min(8f)] private float iconSize = 32f;
    [SerializeField, Min(1)] private int backgroundCornerRadius = 12;

    [Header("Ikony (przypisz sprite'y w Inspectorze)")]
    [SerializeField] private HabitatIconEntry[] habitatIcons = new HabitatIconEntry[0];

    [System.Serializable]
    public struct HabitatIconEntry
    {
        public HabitatAnimal animal;
        public Sprite sprite;
    }

    private readonly HabitatHoverScratch _scratch = new();
    private readonly Dictionary<HabitatAnimal, Sprite> _iconByAnimal = new();
    private readonly HashSet<Tile> _placeableCache = new();

    private static TileNextTileHoverPreview _activeInstance;

    // --- Ghost pool ---
    // Klucz: (biome, prefab instance ID) — unikalny per typ draw.
    // Wartość: zbudowany root z dekoracjami, nieaktywny gdy nie używany.
    private readonly Dictionary<(TileBiome, int), GameObject> _ghostPool = new();
    private GameObject _activeGhostRoot;   // aktualnie widoczny ghost
    private TileDraw   _activeGhostDraw;   // draw dla aktywnego ghosta (szybkie porównanie)

    // --- Icon pool (screen-space overlay — bez SSAO/post-process sceny 3D) ---
    private Transform _iconsRoot;
    private RectTransform _iconsAnchor;
    private Canvas _iconsCanvas;
    private readonly List<HabitatPreviewSlot> _iconPool = new();
    private const int IconPoolCapacity = 8;

    private readonly List<(GameObject go, LineRenderer lr)> _candidateLinePool = new();
    private readonly List<Tile> _candidateRegionScratch = new();
    private readonly HashSet<Tile> _candidateRegionSet = new();
    private readonly List<(Vector3 a, Vector3 b)> _candidateEdgeScratch = new();
    private readonly List<int> _candidateHighlightTileKeys = new();
    private readonly List<RaycastResult> _iconRaycastScratch = new();
    private int _activeCandidateLineCount;
    private Transform _candidateHighlightParent;
    private bool _iconPointerHandlersRegistered;

    private Tile _lastHover;
    private TileDraw _lastDraw;
    private HabitatHoverResult _lastResult;
    private bool _placeableDirty = true;
    private bool _needsReevaluate = true;
    private int _lastIconSignature;

    /// <summary>Kafel nad którym był ostatni hover (null jeśli poza siatką).</summary>
    public Tile LastHoverTile => _lastHover;
    /// <summary>Wynik ostatniej evaluacji hover — gotowy do odczytu przy kliknięciu, bez ponownego Evaluate.</summary>
    public HabitatHoverResult LastHoverResult => _lastResult;

    private const int SortGhost = 3100;
    private const int SortIcons = 3200;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (!mainCamera) mainCamera = Camera.main;
        if (!query) query = FindAnyObjectByType<TileQueryService>();
        if (!runtimeStore) runtimeStore = FindAnyObjectByType<TileRuntimeStore>();
        if (!tileDeck) tileDeck = FindAnyObjectByType<TileDeck>();
        if (!availability) availability = FindAnyObjectByType<TileAvailabilityService>();
        if (!classifier) classifier = FindAnyObjectByType<BiomeHabitatClassifier>();
        if (!tileGrid) tileGrid = FindAnyObjectByType<TileGrid>();
        if (!biomePopulator) biomePopulator = FindAnyObjectByType<BiomeTilePopulator>();

        _iconByAnimal.Clear();
        if (habitatIcons != null)
            foreach (var e in habitatIcons)
                if (e.sprite != null && e.animal != HabitatAnimal.None)
                    _iconByAnimal[e.animal] = e.sprite;

        InitIconPool();
    }

    private void OnEnable()
    {
        if (_activeInstance != null && _activeInstance != this) { enabled = false; return; }
        _activeInstance = this;
        TileEvents.TileStateChanged += OnTileStateChanged;
        TileEvents.HabitatAssigned += OnHabitatAssigned;
        if (tileDeck != null)
        {
            tileDeck.DeckChanged += OnDeckChanged;
            tileDeck.DeckEmptied += OnDeckEmptied;
        }
        _placeableDirty = true;
    }

    private void OnDisable()
    {
        if (_activeInstance == this) _activeInstance = null;
        TileEvents.TileStateChanged -= OnTileStateChanged;
        TileEvents.HabitatAssigned -= OnHabitatAssigned;
        if (tileDeck != null)
        {
            tileDeck.DeckChanged -= OnDeckChanged;
            tileDeck.DeckEmptied -= OnDeckEmptied;
        }
        HideAllGhosts();
        HideAllIcons();
        _lastHover = null;
        _lastDraw = null;
        _activeGhostDraw = null;
        _lastIconSignature = 0;
        _needsReevaluate = true;
    }

    private void OnDestroy()
    {
        // Poolowane ghosci są dziećmi tego GameObject — Unity je niszczy automatycznie.
        _ghostPool.Clear();
        _activeGhostRoot = null;
        _activeGhostDraw = null;

        if (_iconsRoot != null)
        {
            Destroy(_iconsRoot.gameObject);
            _iconsRoot = null;
            _iconsAnchor = null;
            _iconsCanvas = null;
        }
        _iconPool.Clear();
        ClearCandidateTileHighlight();
    }

    private void OnDeckEmptied() { HideAllGhosts(); HideAllIcons(); enabled = false; }

    public void ResetForNewSession()
    {
        PurgeGhostPoolInternal();
        HideAllIcons();
        _lastHover = null;
        _lastDraw = null;
        _activeGhostDraw = null;
        _placeableDirty = true;
        _needsReevaluate = true;
        enabled = true;
    }

    /// <summary>Zwalnia nieaktywne ghosty z puli — wywoływane po Play Mode / przy presji VRAM.</summary>
    public static void PurgeGhostPool()
    {
        if (_activeInstance != null)
            _activeInstance.PurgeGhostPoolInternal();
    }

    private void PurgeGhostPoolInternal()
    {
        HideAllGhosts();
        foreach (var kv in _ghostPool)
        {
            if (kv.Value != null)
                Destroy(kv.Value);
        }
        _ghostPool.Clear();
    }

    private void OnTileStateChanged(Tile _) { _placeableDirty = true; _needsReevaluate = true; }

    private void OnHabitatAssigned(HabitatAssignmentData data)
    {
        TrimGhostPool();

        if (_lastHover == null || data.Tiles == null || data.Animal == HabitatAnimal.None)
            return;

        foreach (var t in data.Tiles)
        {
            if (!ReferenceEquals(t, _lastHover)) continue;

            _lastResult = new HabitatHoverResult(HabitatHoverPreviewKind.Green, data.Animal, System.Array.Empty<HabitatAnimal>());
            _needsReevaluate = false;
            _lastIconSignature = -1;
            if (_iconsAnchor != null && isActiveAndEnabled && mainCamera != null)
            {
                RebuildIcons(_lastResult);
                UpdateIconsScreenPosition(_lastHover, mainCamera);
            }
            break;
        }
    }

    private void OnDeckChanged(System.Collections.Generic.IReadOnlyList<TileDraw> _)
    {
        _placeableDirty = true;
        _needsReevaluate = true;
        _lastDraw = tileDeck?.Current;

        // Po stawianiu z ikony _lastHover może być już zajęty — nie pokazuj ghosta następnej karty.
        if (_lastHover == null || !IsTileEligibleForPlacementPreview(_lastHover))
        {
            HideAllGhosts();
            return;
        }

        var next = _lastDraw;
        if (next != null && next.prefab != null)
            ActivateGhostFromPool(next, _lastHover);
    }

    /// <summary>Wywołaj po udanym placement (np. klik na ikonę) — czyści obrys i ghost na starym polu.</summary>
    public void NotifyPlacementCompleted(Tile placedTile)
    {
        ClearCandidateTileHighlight();
        HideAllGhosts();
        _placeableDirty = true;
        _needsReevaluate = true;
        if (tileDeck != null && !tileDeck.IsEmpty)
            _lastDraw = tileDeck.Current;
    }

    // -------------------------------------------------------------------------
    // LateUpdate
    // -------------------------------------------------------------------------

    private void LateUpdate()
    {
        if (!isActiveAndEnabled || !Application.isPlaying) return;

        if (tileDeck == null || tileDeck.IsEmpty || runtimeStore == null || query == null)
        { ClearVisuals(); return; }

        var next = tileDeck.Current;
        if (next == null || next.prefab == null) { ClearVisuals(); return; }

        if (!TryGetPointerScreen(out var screen, out var pointerId)) { ClearVisuals(); return; }

        var cam = mainCamera != null ? mainCamera : Camera.main;
        if (cam == null) return;

        bool overHabitatIcons = IsPointerOverHabitatIcons(screen);
        if (!overHabitatIcons && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId))
        {
            ClearVisuals();
            return;
        }

        if (overHabitatIcons)
        {
            UpdatePreviewWhilePointerOverIcons(next, cam);
            return;
        }

        var ray = cam.ScreenPointToRay(screen);
        if (!Physics.Raycast(ray, out var hit, 800f, ~0, QueryTriggerInteraction.Ignore))
        { ClearVisuals(); return; }

        if (!query.TryGetNearestTile(hit.point, out var tile, 500f)) { ClearVisuals(); return; }

        EnsurePlaceableCache();
        if (!_placeableCache.Contains(tile)) { ClearVisuals(); return; }

        int maxTiles = classifier != null ? classifier.MaxTilesPerHabitat : 5;
        int maxSteps = classifier != null ? classifier.MaxGraphStepsFromPlacement : 5;

        bool drawChanged = !DrawEquals(next, _lastDraw);
        bool tileChanged = !ReferenceEquals(tile, _lastHover);

        if (drawChanged || tileChanged || _needsReevaluate)
            RefreshHoverPreviewAtTile(tile, next, maxTiles, maxSteps);

        SyncGhostForPlaceableTile(tile, next, drawChanged);
        UpdateIconsScreenPosition(tile, cam);
    }

    private void UpdatePreviewWhilePointerOverIcons(TileDraw next, Camera cam)
    {
        if (_lastHover == null)
        {
            ClearCandidateTileHighlight();
            HideAllGhosts();
            return;
        }

        int maxTiles = classifier != null ? classifier.MaxTilesPerHabitat : 5;
        int maxSteps = classifier != null ? classifier.MaxGraphStepsFromPlacement : 5;

        if (_needsReevaluate || _placeableDirty || !DrawEquals(next, _lastDraw))
            RefreshHoverPreviewAtTile(_lastHover, next, maxTiles, maxSteps);

        bool drawChanged = !DrawEquals(next, _activeGhostDraw);
        SyncGhostForPlaceableTile(_lastHover, next, drawChanged);
        UpdateIconsScreenPosition(_lastHover, cam);
    }

    private void RefreshHoverPreviewAtTile(Tile tile, TileDraw next, int maxTiles, int maxSteps)
    {
        if (tile == null || next == null || runtimeStore == null) return;

        _lastHover = tile;
        _lastDraw = next;
        ClearCandidateTileHighlight();

        HabitatHoverEvaluator.Evaluate(runtimeStore, tile, next, maxTiles, maxSteps, _scratch, out _lastResult,
            classifier != null ? classifier.RulesProfile : null);
        _needsReevaluate = false;

        int sig = IconSignature(_lastResult);
        if (sig != _lastIconSignature)
        {
            _lastIconSignature = sig;
            RebuildIcons(_lastResult);
        }
    }

    private void SyncGhostForPlaceableTile(Tile tile, TileDraw next, bool drawChanged)
    {
        if (!IsTileEligibleForPlacementPreview(tile))
        {
            HideAllGhosts();
            return;
        }

        if (next == null || next.prefab == null)
        {
            HideAllGhosts();
            return;
        }

        if (drawChanged || _activeGhostRoot == null)
            ActivateGhostFromPool(next, tile);

        if (_activeGhostRoot != null)
        {
            _activeGhostRoot.transform.position = tile.worldPos + ghostWorldOffset;
            if (tileGrid != null) _activeGhostRoot.transform.rotation = tileGrid.transform.rotation;
            ApplyGhostVisualScale(_activeGhostRoot.transform, _activeGhostDraw?.prefab);
        }
    }

    private bool IsTileEligibleForPlacementPreview(Tile tile)
    {
        if (tile == null || runtimeStore == null) return false;
        var rt = runtimeStore.Get(tile);
        if (rt == null || rt.occupied) return false;
        EnsurePlaceableCache();
        return _placeableCache.Contains(tile);
    }

    // -------------------------------------------------------------------------
    // Ghost pool
    // -------------------------------------------------------------------------

    /// <summary>
    /// Zwraca gotowy ghost z puli lub buduje nowy. Koszt Populate ponoszony dokładnie raz
    /// per unikalny (biom, prefab) przez cały runtime. Kolejne użycia = SetActive + move.
    /// </summary>
    private void ActivateGhostFromPool(TileDraw draw, Tile tile)
    {
        if (draw == null || draw.prefab == null) return;

        var key = (draw.biome, draw.prefab.GetInstanceID());

        // Ukryj poprzedni ghost (inny typ draw).
        if (_activeGhostRoot != null && !DrawEquals(draw, _activeGhostDraw))
        {
            _activeGhostRoot.SetActive(false);
            _activeGhostRoot = null;
        }

        if (!_ghostPool.TryGetValue(key, out var root) || root == null)
        {
            root = BuildAndPoolGhost(draw, tile);
            _ghostPool[key] = root;
            TrimGhostPool();
        }

        _activeGhostRoot = root;
        _activeGhostDraw = draw;
        _activeGhostRoot.SetActive(true);
    }

    /// <summary>
    /// Jednorazowy build dla danego draw type. Wywołuje Populate i zwraca nieaktywny root.
    /// Pozycja i skala ustawiane PRZED Populate — GetWorldPosition liczy absolutne pozycje slotów.
    /// </summary>
    private GameObject BuildAndPoolGhost(TileDraw draw, Tile tile)
    {
        var root = new GameObject($"Ghost_{draw.biome}");
        root.transform.SetParent(transform, false);

        // Pozycja przed Populate — dekoracje liczone w skali prefaba (jak przy placement).
        root.transform.position = tile.worldPos + ghostWorldOffset;
        if (tileGrid != null) root.transform.rotation = tileGrid.transform.rotation;
        root.transform.localScale = Vector3.one;

        var instance = Instantiate(draw.prefab, root.transform);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = GetPlacementScale(draw.prefab);

        if (draw.biome != TileBiome.None && biomePopulator != null)
            PopulateGhostBiome(instance, draw, tile);

        ApplyGhostVisualScale(root.transform, draw.prefab);
        ConfigureGhostRenderPipeline(instance);
        root.SetActive(false); // domyślnie ukryty, aktywowany przez ActivateGhostFromPool
        return root;
    }

    /// <summary>
    /// Wyciąga aktywnego ghosta z puli i zwraca go gotowego do "promocji" na postawiony kafel.
    /// Usuwa wpis z puli — następny hover odbuduje ghosta dla nowego draw type.
    /// Zwraca (null, null) jeśli nie ma aktywnego ghosta.
    /// </summary>
    public (GameObject instance, GameObject ghostRoot) TakeActiveGhostForPlacement()
    {
        if (_activeGhostRoot == null || _activeGhostDraw == null) return (null, null);

        var root = _activeGhostRoot;
        var draw = _activeGhostDraw;

        // Usuń z puli — następny hover dla tego draw type odbuduje ghosta.
        var key = (draw.biome, draw.prefab.GetInstanceID());
        _ghostPool.Remove(key);
        _activeGhostRoot = null;
        _activeGhostDraw = null;
        _lastDraw = null; // wymuś rebuild przy następnym LateUpdate

        if (root.transform.childCount == 0) { Destroy(root); return (null, null); }
        var instance = root.transform.GetChild(0).gameObject;
        // Dekoracje parentowane w skali prefaba — przed placement musi wrócić ta sama skala.
        if (draw.prefab != null)
            instance.transform.localScale = GetPlacementScale(draw.prefab);
        return (instance, root);
    }

    private void PopulateGhostBiome(GameObject instance, TileDraw draw, Tile tile)
    {
        if (instance == null || draw == null || tile == null || biomePopulator == null)
            return;
        if (draw.biome == TileBiome.None)
            return;

        var biomeRuntime = instance.GetComponent<TileBiomeRuntime>()
            ?? instance.AddComponent<TileBiomeRuntime>();

        float radius = tileGrid != null ? tileGrid.HexRadius : 1f;
        biomeRuntime.Initialize(draw.biome, radius, draw.biomeVariantId);

        int seed = unchecked((tile.q * 73856093) ^ (tile.r * 19349663));
        biomePopulator.Populate(biomeRuntime, seed, instance.transform);
    }

    /// <summary>Ogranicza pule linii kandydata i ghostów — wywoływane przez <see cref="GpuResourceBudget"/>.</summary>
    public void TrimGpuPools(int maxCandidateLines, int maxGhosts)
    {
        TrimCandidateLinePool(maxCandidateLines);
        TrimGhostPoolToLimit(maxGhosts);
    }

    private void TrimCandidateLinePool(int maxPoolSize)
    {
        maxPoolSize = Mathf.Max(_activeCandidateLineCount, maxPoolSize);
        while (_candidateLinePool.Count > maxPoolSize)
        {
            int last = _candidateLinePool.Count - 1;
            var entry = _candidateLinePool[last];
            _candidateLinePool.RemoveAt(last);
            if (entry.go != null)
                Destroy(entry.go);
        }
    }

    private const int DefaultMaxGhostPoolEntries = 6;

    private void TrimGhostPool()
    {
        TrimGhostPoolToLimit(DefaultMaxGhostPoolEntries);
    }

    private void TrimGhostPoolToLimit(int limit)
    {
        limit = Mathf.Max(1, limit);
        if (_ghostPool.Count <= limit)
            return;

        var keysToRemove = new List<(TileBiome, int)>();
        foreach (var kv in _ghostPool)
        {
            if (kv.Value == null || kv.Value == _activeGhostRoot)
                continue;
            keysToRemove.Add(kv.Key);
        }

        while (_ghostPool.Count > limit && keysToRemove.Count > 0)
        {
            var key = keysToRemove[keysToRemove.Count - 1];
            keysToRemove.RemoveAt(keysToRemove.Count - 1);
            if (!_ghostPool.TryGetValue(key, out var go) || go == null)
            {
                _ghostPool.Remove(key);
                continue;
            }
            if (go == _activeGhostRoot)
                continue;
            Destroy(go);
            _ghostPool.Remove(key);
        }
    }

    private static Vector3 GetPlacementScale(GameObject prefab)
        => prefab != null ? prefab.transform.localScale : Vector3.one;

    /// <summary>Wizualny rozmiar ducha — nie zmienia układu dekoracji zapisanych w skali prefaba.</summary>
    private void ApplyGhostVisualScale(Transform ghostRoot, GameObject prefab)
    {
        if (ghostRoot == null || ghostRoot.childCount == 0) return;
        var placement = GetPlacementScale(prefab);
        float uniform = placement.x;
        float mul = uniform > 0.0001f ? ghostUniformScale / uniform : 1f;
        ghostRoot.GetChild(0).localScale = placement * mul;
    }

    private void HideAllGhosts()
    {
        if (_activeGhostRoot != null)
        {
            _activeGhostRoot.SetActive(false);
            _activeGhostRoot = null;
        }
        _activeGhostDraw = null;
    }


    private static void ConfigureGhostRenderPipeline(GameObject root)
    {
        if (root == null) return;
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            r.sortingOrder = SortGhost;
    }

    // -------------------------------------------------------------------------
    // Icon pool
    // -------------------------------------------------------------------------

    private void InitIconPool()
    {
        if (_iconsRoot == null)
        {
            var go = new GameObject("HabitatHoverIcons",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _iconsRoot = go.transform;
            // Overlay + rodzic 3D psuł transform.position — canvas jako root sceny.
            _iconsRoot.SetParent(null, worldPositionStays: false);

            _iconsCanvas = go.GetComponent<Canvas>();
            _iconsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _iconsCanvas.overrideSorting = true;
            _iconsCanvas.sortingOrder = SortIcons;
            // Jak GameUI — importowane sprite'y zwierząt tracą kolor przy vertexColorAlwaysGammaSpace.

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var canvasRt = (RectTransform)_iconsRoot;
            canvasRt.anchorMin = Vector2.zero;
            canvasRt.anchorMax = Vector2.one;
            canvasRt.offsetMin = Vector2.zero;
            canvasRt.offsetMax = Vector2.zero;

            var anchorGo = new GameObject("IconsAnchor", typeof(RectTransform));
            _iconsAnchor = anchorGo.GetComponent<RectTransform>();
            _iconsAnchor.SetParent(_iconsRoot, false);
            _iconsAnchor.anchorMin = _iconsAnchor.anchorMax = new Vector2(0.5f, 0.5f);
            _iconsAnchor.pivot = new Vector2(0.5f, 0.5f);
            _iconsAnchor.anchoredPosition = Vector2.zero;
            _iconsAnchor.sizeDelta = Vector2.zero;
        }

        for (int i = _iconPool.Count; i < IconPoolCapacity; i++)
        {
            var go = new GameObject($"HabitatPreviewSlot_{i}", typeof(RectTransform));
            go.transform.SetParent(_iconsAnchor, false);
            var slotRt = (RectTransform)go.transform;
            slotRt.anchorMin = slotRt.anchorMax = new Vector2(0.5f, 0.5f);
            slotRt.pivot = new Vector2(0.5f, 0.5f);

            var slot = go.AddComponent<HabitatPreviewSlot>();
            slot.ConfigureSizes(ScaledBackgroundSize, ScaledIconSize, backgroundCornerRadius,
                deficitHintFontSize, deficitHintColor, deficitHintAboveSlot, deficitHintWidth);
            slot.EnsureBuilt();
            slot.Clear();
            if (!_iconPointerHandlersRegistered)
            {
                slot.PointerEntered += OnIconSlotPointerEntered;
                slot.PointerExited += OnIconSlotPointerExited;
            }
            _iconPool.Add(slot);
        }

        _iconPointerHandlersRegistered = true;

        RefreshIconPoolSizes();
    }

    private float ScaledBackgroundSize => backgroundSize * iconSizeMultiplier;
    private float ScaledIconSize       => iconSize * iconSizeMultiplier;
    private float IconSlotPitch        => ScaledBackgroundSize + iconSpacing;

    private void RefreshIconPoolSizes()
    {
        foreach (var slot in _iconPool)
            slot?.ConfigureSizes(ScaledBackgroundSize, ScaledIconSize, backgroundCornerRadius,
                deficitHintFontSize, deficitHintColor, deficitHintAboveSlot, deficitHintWidth);
    }

    private void RebuildIcons(HabitatHoverResult result)
    {
        InitIconPool();

        RefreshIconPoolSizes();

        foreach (var slot in _iconPool)
            slot?.Clear();

        float slotPitch = IconSlotPitch;
        int idx = 0;

        if (result.Kind == HabitatHoverPreviewKind.Green && result.GreenAnimal != HabitatAnimal.None
            && _iconByAnimal.TryGetValue(result.GreenAnimal, out var greenSp) && greenSp != null)
        {
            ShowPooledSlot(idx++, greenSp, HabitatHoverPreviewKind.Green, result.GreenAnimal, Vector2.zero);
        }
        else if (result.Kind == HabitatHoverPreviewKind.Yellow && result.YellowAnimals?.Count > 0)
        {
            float startX = -0.5f * slotPitch * Mathf.Max(0, result.YellowAnimals.Count - 1);
            for (int i = 0; i < result.YellowAnimals.Count; i++)
            {
                var animal = result.YellowAnimals[i];
                if (!_iconByAnimal.TryGetValue(animal, out var sp) || sp == null) continue;
                ShowPooledSlot(idx++, sp, HabitatHoverPreviewKind.Yellow, animal,
                    new Vector2(startX + i * slotPitch, 0f), result.GetYellowDeficitHint(i));
            }
        }
    }

    private void OnIconSlotPointerEntered(HabitatPreviewSlot slot)
    {
        if (slot == null || slot.Animal == HabitatAnimal.None || _lastHover == null || _lastDraw == null)
            return;

        int maxTiles = classifier != null ? classifier.MaxTilesPerHabitat : 5;
        int searchSteps = classifier != null ? classifier.MaxGraphStepsFromPlacement : 5;

        bool isYellow = slot.PreviewKind == HabitatHoverPreviewKind.Yellow;
        var outlineKind = isYellow
            ? HabitatCandidateOutlineKind.Almost
            : HabitatCandidateOutlineKind.Full;
        int displaySteps = isYellow
            ? Mathf.Max(1, candidateYellowHighlightMaxGraphSteps)
            : (candidateHighlightMaxGraphSteps > 0
                ? Mathf.Min(candidateHighlightMaxGraphSteps, searchSteps)
                : searchSteps);

        if (!HabitatHoverEvaluator.TryGetCandidateRegion(
                runtimeStore, _lastHover, _lastDraw, slot.Animal, maxTiles, searchSteps, displaySteps,
                outlineKind, _scratch,
                out var region, classifier != null ? classifier.RulesProfile : null))
        {
            ClearCandidateTileHighlight();
            return;
        }

        if (IsSameCandidateHighlightRegion(region))
            return;

        ApplyCandidateTileHighlight(region);
    }

    private void OnIconSlotPointerExited(HabitatPreviewSlot _)
        => ClearCandidateTileHighlight();

    private void UpdateIconsScreenPosition(Tile tile, Camera cam)
    {
        if (_iconsAnchor == null || tile == null || cam == null)
            return;

        Vector3 world = tile.worldPos + Vector3.up * iconsAboveTile;
        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z < 0f)
        {
            HideAllIcons();
            return;
        }

        var canvasRt = (RectTransform)_iconsRoot;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRt, screen, null, out Vector2 localPoint))
        {
            _iconsAnchor.anchoredPosition = localPoint;
        }
    }

    private void ShowPooledSlot(int idx, Sprite animalSprite, HabitatHoverPreviewKind kind,
        HabitatAnimal animal, Vector2 localPosPx, string deficitHint = null)
    {
        if (idx >= _iconPool.Count) return;
        var slot = _iconPool[idx];
        if (slot == null) return;

        slot.Show(animalSprite, kind, animal, grayBackgroundColor, yellowBackgroundColor, greenBackgroundColor,
            iconColor, deficitHint);

        if (slot.RectTransform != null)
            slot.RectTransform.anchoredPosition = localPosPx;
    }

    private void HideAllIcons()
    {
        ClearCandidateTileHighlight();
        if (_iconsRoot != null)
            foreach (var slot in _iconPool)
                slot?.Clear();
        _lastIconSignature = 0;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void ClearVisuals()
    {
        _lastHover = null;
        _lastDraw = null;
        _needsReevaluate = true;
        ClearCandidateTileHighlight();
        HideAllGhosts();
        HideAllIcons();
    }

    /// <summary>Czy wskaznik jest nad slotem podglądu habitatu (do stawiania z ikony).</summary>
    public bool IsPointerOverHabitatIcons(Vector2 screen)
    {
        if (_iconsCanvas == null || EventSystem.current == null) return false;

        var ped = new PointerEventData(EventSystem.current) { position = screen };
        _iconRaycastScratch.Clear();
        EventSystem.current.RaycastAll(ped, _iconRaycastScratch);
        for (int i = 0; i < _iconRaycastScratch.Count; i++)
        {
            if (_iconRaycastScratch[i].gameObject.GetComponentInParent<HabitatPreviewSlot>() != null)
                return true;
        }
        return false;
    }

    /// <summary>Kafel pod ghostem/ikonami — ustawiany przy hover siatki, zachowany gdy mysz na ikonie.</summary>
    public bool TryGetPlacementTileWhileOverIcons(out Tile tile)
    {
        tile = _lastHover;
        return tile != null;
    }

    private void ApplyCandidateTileHighlight(IReadOnlyList<Tile> tiles)
    {
        ClearCandidateTileHighlight();
        if (candidateTileHighlightMaterial == null || tiles == null || tiles.Count == 0)
            return;

        _candidateRegionScratch.Clear();
        _candidateRegionSet.Clear();
        _candidateHighlightTileKeys.Clear();

        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            _candidateRegionScratch.Add(tile);
            _candidateRegionSet.Add(tile);
            _candidateHighlightTileKeys.Add(GetTileKey(tile));
        }

        if (_candidateRegionScratch.Count == 0)
            return;

        EnsureCandidateHighlightParent();
        _candidateEdgeScratch.Clear();
        HabitatRegionOutlineUtility.CollectBoundaryEdges(
            _candidateRegionScratch,
            _candidateRegionSet,
            candidateLineHeightOffset,
            0f,
            candidateLineInset,
            _candidateEdgeScratch);

        _activeCandidateLineCount = 0;
        for (int i = 0; i < _candidateEdgeScratch.Count; i++)
        {
            var (a, b) = _candidateEdgeScratch[i];
            var (go, lr) = GetOrCreateCandidateLineSegment(_activeCandidateLineCount);
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.material = candidateTileHighlightMaterial;
            lr.startWidth = candidateLineWidth;
            lr.endWidth = candidateLineWidth * 0.8f;
            go.SetActive(true);
            _activeCandidateLineCount++;
        }
    }

    private void ClearCandidateTileHighlight()
    {
        for (int i = 0; i < _activeCandidateLineCount; i++)
        {
            if (i < _candidateLinePool.Count)
                _candidateLinePool[i].go.SetActive(false);
        }

        _activeCandidateLineCount = 0;
        _candidateHighlightTileKeys.Clear();
    }

    private bool IsSameCandidateHighlightRegion(IReadOnlyList<Tile> tiles)
    {
        if (tiles == null || tiles.Count != _candidateHighlightTileKeys.Count)
            return false;

        var keys = new HashSet<int>(_candidateHighlightTileKeys);
        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            if (tile == null || IsWaterTile(tile))
                continue;
            if (!keys.Remove(GetTileKey(tile)))
                return false;
        }

        return keys.Count == 0;
    }

    private static int GetTileKey(Tile tile) => tile.q * 10000 + tile.r;

    private (GameObject go, LineRenderer lr) GetOrCreateCandidateLineSegment(int idx)
    {
        if (idx < _candidateLinePool.Count)
            return _candidateLinePool[idx];

        EnsureCandidateHighlightParent();
        var go = new GameObject("CandidateOutlineSegment");
        go.transform.SetParent(_candidateHighlightParent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 0;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        go.SetActive(false);

        var entry = (go, lr);
        _candidateLinePool.Add(entry);
        return entry;
    }

    private void EnsureCandidateHighlightParent()
    {
        if (_candidateHighlightParent != null) return;
        var go = new GameObject("HabitatCandidateHighlights");
        go.transform.SetParent(transform, false);
        _candidateHighlightParent = go.transform;
    }

    private bool IsWaterTile(Tile tile)
    {
        if (ReferenceEquals(tile, _lastHover))
            return _lastDraw != null && _lastDraw.biome == TileBiome.Water;

        var rt = runtimeStore != null ? runtimeStore.Get(tile) : null;
        return rt != null && rt.biome == TileBiome.Water;
    }

    private void EnsurePlaceableCache()
    {
        if (!_placeableDirty) return;
        _placeableCache.Clear();
        if (availability != null)
            foreach (var t in availability.GetAvailable())
                if (t != null) _placeableCache.Add(t);
        _placeableDirty = false;
    }

    private static bool DrawEquals(TileDraw a, TileDraw b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        return a.biome == b.biome
               && ReferenceEquals(a.prefab, b.prefab)
               && string.Equals(a.biomeVariantId, b.biomeVariantId, StringComparison.OrdinalIgnoreCase);
    }

    private static int IconSignature(HabitatHoverResult r)
    {
        unchecked
        {
            int h = (int)r.Kind * 397 ^ (int)r.GreenAnimal;
            if (r.YellowAnimals != null)
            {
                for (int i = 0; i < r.YellowAnimals.Count; i++)
                {
                    h = h * 397 ^ (int)r.YellowAnimals[i];
                    var hint = r.GetYellowDeficitHint(i);
                    if (!string.IsNullOrEmpty(hint))
                        h = h * 397 ^ hint.GetHashCode();
                }
            }
            return h;
        }
    }

    private static bool TryGetPointerScreen(out Vector2 screen, out int pointerId)
    {
        screen = default;
        pointerId = -1;
        var mouse = Mouse.current;
        if (mouse != null) { screen = mouse.position.ReadValue(); return true; }
        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.isPressed)
        {
            screen = ts.primaryTouch.position.ReadValue();
            pointerId = ts.primaryTouch.touchId.ReadValue();
            return true;
        }
        return false;
    }

    /// <summary>Sprite zwierzęcia z tablicy habitatIcons (dla mockupu edytora).</summary>
    public Sprite TryGetHabitatSprite(HabitatAnimal animal)
    {
        if (animal == HabitatAnimal.None || habitatIcons == null)
            return null;

        foreach (var e in habitatIcons)
        {
            if (e.animal == animal && e.sprite != null)
                return e.sprite;
        }

        return null;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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

    [Header("Kolory preview habitatu")]
    [SerializeField] private Color grayBackgroundColor = new(0.55f, 0.55f, 0.55f, 0.85f);
    [SerializeField] private Color yellowBackgroundColor = new(0.96f, 0.86f, 0.35f, 0.95f);
    [SerializeField] private Color greenBackgroundColor = new(0.45f, 0.82f, 0.48f, 0.95f);
    [SerializeField] private Color iconColor = Color.white;

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
    }

    private void OnDeckEmptied() { HideAllGhosts(); HideAllIcons(); enabled = false; }

    public void ResetForNewSession()
    {
        HideAllGhosts();
        HideAllIcons();
        _lastHover = null;
        _lastDraw = null;
        _activeGhostDraw = null;
        _placeableDirty = true;
        _needsReevaluate = true;
        enabled = true;
    }

    private void OnTileStateChanged(Tile _) { _placeableDirty = true; _needsReevaluate = true; }

    private void OnHabitatAssigned(HabitatAssignmentData data)
    {
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
        // Przy zmianie karty: jeśli nowy ghost jest już w puli → swap jest darmowy (SetActive).
        // Jeśli nie ma go w puli → EnsureGhost zbuduje go przy następnym hover (lazy).
        // _lastHover null oznacza że gracz jeszcze nie hoverowal — poczekamy na pierwszą pozycję.
        if (_lastHover != null)
        {
            var next = tileDeck?.Current;
            if (next != null && next.prefab != null)
                ActivateGhostFromPool(next, _lastHover);
        }
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

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId))
        { ClearVisuals(); return; }

        var cam = mainCamera != null ? mainCamera : Camera.main;
        if (cam == null) return;

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
        {
            _lastHover = tile;
            _lastDraw = next;
            HabitatHoverEvaluator.Evaluate(runtimeStore, tile, next, maxTiles, maxSteps, _scratch, out _lastResult,
                classifier != null ? classifier.RulesProfile : null);
            _needsReevaluate = false;
        }

        // Aktywuje ghost z puli (lub buduje jeśli tego biome/prefab jeszcze nie ma).
        if (drawChanged || _activeGhostRoot == null)
            ActivateGhostFromPool(next, tile);

        // Przesuń aktywny ghost — jedyna operacja per-tile.
        if (_activeGhostRoot != null)
        {
            _activeGhostRoot.transform.position = tile.worldPos + ghostWorldOffset;
            if (tileGrid != null) _activeGhostRoot.transform.rotation = tileGrid.transform.rotation;
            ApplyGhostVisualScale(_activeGhostRoot.transform, _activeGhostDraw?.prefab);
        }

        int sig = IconSignature(_lastResult);
        if (sig != _lastIconSignature)
        {
            _lastIconSignature = sig;
            RebuildIcons(_lastResult);
        }

        UpdateIconsScreenPosition(tile, cam);
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
            // Pierwszy raz ten draw — zbuduj i wstaw do puli.
            root = BuildAndPoolGhost(draw, tile);
            _ghostPool[key] = root;
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
        {
            var biomeRuntime = instance.GetComponent<TileBiomeRuntime>()
                ?? instance.AddComponent<TileBiomeRuntime>();

            float radius = tileGrid != null ? tileGrid.HexRadius : 1f;
            biomeRuntime.Initialize(draw.biome, radius);

            // Stały seed per biome — dekoracje stabilne, bez re-generate per hover; skala = prefab.
            int seed = unchecked((int)draw.biome * 73856093);
            biomePopulator.Populate(biomeRuntime, seed, instance.transform);
        }

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
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
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
            slot.ConfigureSizes(ScaledBackgroundSize, ScaledIconSize, backgroundCornerRadius);
            slot.EnsureBuilt();
            slot.Clear();
            _iconPool.Add(slot);
        }

        RefreshIconPoolSizes();
    }

    private float ScaledBackgroundSize => backgroundSize * iconSizeMultiplier;
    private float ScaledIconSize       => iconSize * iconSizeMultiplier;
    private float IconSlotPitch        => ScaledBackgroundSize + iconSpacing;

    private void RefreshIconPoolSizes()
    {
        foreach (var slot in _iconPool)
            slot?.ConfigureSizes(ScaledBackgroundSize, ScaledIconSize, backgroundCornerRadius);
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
            ShowPooledSlot(idx++, greenSp, HabitatHoverPreviewKind.Green, Vector2.zero);
        }
        else if (result.Kind == HabitatHoverPreviewKind.Yellow && result.YellowAnimals?.Count > 0)
        {
            float startX = -0.5f * slotPitch * Mathf.Max(0, result.YellowAnimals.Count - 1);
            for (int i = 0; i < result.YellowAnimals.Count; i++)
            {
                var animal = result.YellowAnimals[i];
                if (!_iconByAnimal.TryGetValue(animal, out var sp) || sp == null) continue;
                ShowPooledSlot(idx++, sp, HabitatHoverPreviewKind.Yellow,
                    new Vector2(startX + i * slotPitch, 0f));
            }
        }
    }

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

    private void ShowPooledSlot(int idx, Sprite animalSprite, HabitatHoverPreviewKind kind, Vector2 localPosPx)
    {
        if (idx >= _iconPool.Count) return;
        var slot = _iconPool[idx];
        if (slot == null) return;

        slot.Show(animalSprite, kind, grayBackgroundColor, yellowBackgroundColor, greenBackgroundColor, iconColor);

        if (slot.RectTransform != null)
            slot.RectTransform.anchoredPosition = localPosPx;
    }

    private void HideAllIcons()
    {
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
        HideAllGhosts();
        HideAllIcons();
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
        return a.biome == b.biome && ReferenceEquals(a.prefab, b.prefab);
    }

    private static int IconSignature(HabitatHoverResult r)
    {
        unchecked
        {
            int h = (int)r.Kind * 397 ^ (int)r.GreenAnimal;
            if (r.YellowAnimals != null)
                foreach (var a in r.YellowAnimals)
                    h = h * 397 ^ (int)a;
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
}

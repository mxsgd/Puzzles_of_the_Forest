using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Tile = TileGrid.Tile;

/// <summary>
/// Nad wolnym, legalnym polem: duch następnego kafla z talii + ikony habitatu.
///
/// Ghost pool — każdy unikalny (biom, prefab) buduje się dokładnie raz przez cały runtime.
/// Zmiana karty → SetActive(false) na starym, SetActive(true) na nowym. Zero Populate per kafel.
/// Ikony: poolowane SpriteRendery, zero Destroy/Instantiate w runtime.
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
    [SerializeField, Min(0.01f), Tooltip("Skala całego ducha (kafel + dekoracje) względem postawionej instancji.")]
    private float ghostUniformScale = 1.35f;
    [SerializeField] private float iconsAboveTile = 2.2f;
    [SerializeField] private float iconSpacing = 0.55f;
    [SerializeField] private float iconScale = 0.45f;
    [SerializeField] private float greenBackdropScale = 0.62f;

    [Header("Kolory ikon habitatu")]
    [SerializeField] private Color greenBackdropColor = new(0.15f, 0.55f, 0.2f, 0.95f);
    [SerializeField] private Color yellowAnimalIconTint = new(1f, 0.95f, 0.55f, 1f);

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

    private static Sprite _whiteSprite;
    private static Material _iconOverlayMaterial;
    private static TileNextTileHoverPreview _activeInstance;

    // --- Ghost pool ---
    // Klucz: (biome, prefab instance ID) — unikalny per typ draw.
    // Wartość: zbudowany root z dekoracjami, nieaktywny gdy nie używany.
    private readonly Dictionary<(TileBiome, int), GameObject> _ghostPool = new();
    private GameObject _activeGhostRoot;   // aktualnie widoczny ghost
    private TileDraw   _activeGhostDraw;   // draw dla aktywnego ghosta (szybkie porównanie)

    // --- Icon pool ---
    private Transform _iconsRoot;
    private readonly List<GameObject> _iconPool = new();
    private const int IconPoolCapacity = 8;

    private Tile _lastHover;
    private TileDraw _lastDraw;
    private HabitatHoverResult _lastResult;
    private bool _placeableDirty = true;
    private bool _needsReevaluate = true;
    private int _lastIconSignature;

    private const int SortGhost    = 3100;
    private const int SortBackdrop = 3199;
    private const int SortIcons    = 3200;

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
        // Czyścimy tylko słownik, żeby nie trzymać martwych referencji.
        _ghostPool.Clear();
        _activeGhostRoot = null;
        _activeGhostDraw = null;
    }

    private void OnDeckEmptied() { HideAllGhosts(); HideAllIcons(); enabled = false; }

    private void OnTileStateChanged(Tile _) { _placeableDirty = true; _needsReevaluate = true; }

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
            HabitatHoverEvaluator.Evaluate(runtimeStore, tile, next, maxTiles, maxSteps, _scratch, out _lastResult);
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
            _activeGhostRoot.transform.localScale = Vector3.one * ghostUniformScale;
        }

        int sig = IconSignature(_lastResult);
        if (sig != _lastIconSignature)
        {
            _lastIconSignature = sig;
            RebuildIcons(tile, _lastResult);
        }
        else if (_iconsRoot != null)
            _iconsRoot.position = tile.worldPos + Vector3.up * iconsAboveTile;

        FaceIconsToCamera(cam);
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

        // Pozycja i skala muszą być ustawione przed Populate.
        root.transform.position = tile.worldPos + ghostWorldOffset;
        if (tileGrid != null) root.transform.rotation = tileGrid.transform.rotation;
        root.transform.localScale = Vector3.one * ghostUniformScale;

        var instance = Instantiate(draw.prefab, root.transform);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        if (draw.biome != TileBiome.None && biomePopulator != null)
        {
            var biomeRuntime = instance.GetComponent<TileBiomeRuntime>()
                ?? instance.AddComponent<TileBiomeRuntime>();

            float radius = tileGrid != null ? tileGrid.HexRadius : 1f;
            biomeRuntime.Initialize(draw.biome, radius);

            // Stały seed per biome — dekoracje stabilne, bez re-generate per hover.
            int seed = unchecked((int)draw.biome * 73856093);
            // decorationParentOverride = instance.transform → dekoracje pod ghostem,
            // nie pod globalnym decorationsParent z BiomeTilePopulator.
            biomePopulator.Populate(biomeRuntime, seed, instance.transform);

            StripGhostTintReceivers(instance);
        }

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
        return (instance, root);
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

    private static void StripGhostTintReceivers(GameObject root)
    {
        if (root == null) return;
        foreach (var recv in root.GetComponentsInChildren<BiomeDecorationTintReceiver>(true))
            if (recv != null) Destroy(recv);
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
            var go = new GameObject("HabitatHoverIcons");
            _iconsRoot = go.transform;
            _iconsRoot.SetParent(transform, false);
        }
        for (int i = _iconPool.Count; i < IconPoolCapacity; i++)
        {
            var go = new GameObject($"HabitatIcon_{i}");
            go.transform.SetParent(_iconsRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = GetIconOverlayMaterial();
            go.SetActive(false);
            _iconPool.Add(go);
        }
    }

    private void RebuildIcons(Tile tile, HabitatHoverResult result)
    {
        if (_iconsRoot == null) InitIconPool();
        _iconsRoot.position = tile.worldPos + Vector3.up * iconsAboveTile;

        foreach (var go in _iconPool)
            if (go != null) go.SetActive(false);

        int idx = 0;

        if (result.Kind == HabitatHoverPreviewKind.Green && result.GreenAnimal != HabitatAnimal.None
            && _iconByAnimal.TryGetValue(result.GreenAnimal, out var greenSp) && greenSp != null)
        {
            SetupPooledIcon(idx++, GetWhiteSprite(), greenBackdropColor, greenBackdropScale * iconScale, SortBackdrop, Vector3.zero);
            SetupPooledIcon(idx++, greenSp, Color.white, iconScale, SortIcons, Vector3.zero);
        }
        else if (result.Kind == HabitatHoverPreviewKind.Yellow && result.YellowAnimals?.Count > 0)
        {
            float startX = -0.5f * iconSpacing * Mathf.Max(0, result.YellowAnimals.Count - 1);
            for (int i = 0; i < result.YellowAnimals.Count && idx < _iconPool.Count; i++)
            {
                var animal = result.YellowAnimals[i];
                if (!_iconByAnimal.TryGetValue(animal, out var sp) || sp == null) continue;
                SetupPooledIcon(idx++, sp, yellowAnimalIconTint, iconScale * 0.9f, SortIcons,
                    new Vector3(startX + i * iconSpacing, 0f, 0f));
            }
        }
    }

    private void SetupPooledIcon(int idx, Sprite sprite, Color color, float scale, int sortOrder, Vector3 localPos)
    {
        if (idx >= _iconPool.Count) return;
        var go = _iconPool[idx];
        if (go == null) return;
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortOrder;
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one * scale;
        go.SetActive(true);
    }

    private void HideAllIcons()
    {
        if (_iconsRoot != null)
            foreach (var go in _iconPool)
                if (go != null) go.SetActive(false);
        _lastIconSignature = 0;
    }

    private void FaceIconsToCamera(Camera cam)
    {
        if (_iconsRoot == null) return;
        _iconsRoot.rotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);
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

    private static Material GetIconOverlayMaterial()
    {
        if (_iconOverlayMaterial != null) return _iconOverlayMaterial;
        Shader overlayShader = Shader.Find("IdleForest/Sprites/AlwaysOnTop");
        if (overlayShader == null) overlayShader = Shader.Find("Sprites/Default");
        if (overlayShader == null) return null;
        _iconOverlayMaterial = new Material(overlayShader) { name = "HabitatHoverIconOverlay_Mat" };
        return _iconOverlayMaterial;
    }

    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;
        var tex = Texture2D.whiteTexture;
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        return _whiteSprite;
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

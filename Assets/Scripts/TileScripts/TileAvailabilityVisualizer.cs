using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TileAvailabilityVisualizer : MonoBehaviour
{
    [Header("Context")]
    [SerializeField] private TileGrid grid;
    [SerializeField] private TileAvailabilityService availability;
    [SerializeField] private TilePlacementService placement;
    [SerializeField] private TileSelectionModel selection;
    [SerializeField] private TileQueryService query;
    [SerializeField] private TileRuntimeStore runtime;
    [SerializeField] private TileDeck tileDeck;
    [SerializeField] private BiomeHabitatClassifier classifier;
    [SerializeField, Tooltip("Opcjonalnie: jeśli przypisany, gotowy ghost jest bezpośrednio promowany na kafel (zero Instantiate/Populate przy stawianiu).")]
    private TileNextTileHoverPreview hoverPreview;

    [Header("Available Tiles (ghost)")]
    [SerializeField] private Transform availableTileParent;
    [SerializeField, Range(0f, 1f)] private float availableAlpha = 0.35f;
    [SerializeField] private string availableTag = "";


    [Header("Selection Highlight")]
    [SerializeField] private Transform selectedTileParent;
    [SerializeField, Range(0f, 1f)] private float selectedAlpha = 0.5f;
    [SerializeField] private string selectedTag = "";

    [Header("Placement Feedback - Pulse")]
    [SerializeField, Min(1f)] private float pulsePeakScale = 1.1f;
    [SerializeField, Min(0.02f)] private float pulseDuration = 0.18f;

    [Header("Placement Feedback - Audio")]
    [SerializeField] private AudioSource feedbackAudioSource;
    [SerializeField] private AudioClip habitatSuccessClip;
    [SerializeField] private AudioClip almostHabitatClip;
    [SerializeField] private AudioClip failedPlacementClip;
    [SerializeField, Range(0f, 1f)] private float habitatVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float almostVolume = 0.75f;
    [SerializeField, Range(0f, 1f)] private float failVolume = 0.65f;

    private enum PlacementFeedbackKind
    {
        Fail,
        Almost,
        Habitat
    }

    private readonly Dictionary<TileGrid.Tile, GameObject> _availableTiles = new Dictionary<TileGrid.Tile, GameObject>();
    private readonly HabitatHoverScratch _feedbackScratch = new HabitatHoverScratch();

    private readonly HashSet<TileGrid.Tile> _availableSet   = new HashSet<TileGrid.Tile>();
    private readonly List<TileGrid.Tile>    _removeBuffer   = new List<TileGrid.Tile>(); // reusable, avoids per-refresh alloc
    
    private TileGrid.Tile _highlightedTile;
    private GameObject _selectedTileInstance;

    private const int MousePointerId = -1;
    private bool _deckDepleted;

    private void Awake()
    {

        if (!availableTileParent && grid) availableTileParent = grid.transform;
        if (!selectedTileParent && grid) selectedTileParent = grid.transform;

        if (!availability)     availability  = FindAnyObjectByType<TileAvailabilityService>();
        if (!placement)        placement     = FindAnyObjectByType<TilePlacementService>();
        if (!selection)        selection     = FindAnyObjectByType<TileSelectionModel>();
        if (!query)            query         = FindAnyObjectByType<TileQueryService>();
        if (!runtime)          runtime       = FindAnyObjectByType<TileRuntimeStore>();
        if (!grid)             grid          = FindAnyObjectByType<TileGrid>();
        if (!tileDeck)         tileDeck      = FindAnyObjectByType<TileDeck>();
        if (!classifier)       classifier    = FindAnyObjectByType<BiomeHabitatClassifier>();
        if (!feedbackAudioSource) feedbackAudioSource = GetComponent<AudioSource>();
        if (!feedbackAudioSource) feedbackAudioSource = gameObject.AddComponent<AudioSource>();

    }

    private void OnEnable()
    {
        if (selection != null)
            selection.SelectionChanged += OnSelectionChanged;

        if (tileDeck != null)
            tileDeck.DeckEmptied += OnDeckEmptied;
        RefreshAvailability();
        UpdateSelectedTileHighlight(selection != null ? selection.Selected : null);
    }

    private void Start()
    {
        RefreshAvailability();
        UpdateSelectedTileHighlight(selection != null ? selection.Selected : null);
    }

    private void OnDisable()
    {
        if (selection != null)
            selection.SelectionChanged -= OnSelectionChanged;
        if (tileDeck != null)
            tileDeck.DeckEmptied -= OnDeckEmptied;
        _availableTiles.Clear();
        ClearSelectedTileHighlight();
        _availableSet.Clear();
    }

    private void Update()
    {
        TryPlaceHighlightedTile();
    }

    private void OnSelectionChanged(TileGrid.Tile tile)
    {
        UpdateSelectedTileHighlight(tile);
    }

    // Diff-based refresh: only remove tiles that are no longer available and add newly available ones.
    // Avoids Destroy+Instantiate for the entire available set on every placement.
    private void RefreshAvailability()
    {
        if (availability == null) return;

        var newAvailable = availability.GetAvailable();

        // Build new set for fast lookup.
        _availableSet.Clear();
        foreach (var t in newAvailable)
            if (t != null) _availableSet.Add(t);

        // Remove tiles that are no longer available.
        _removeBuffer.Clear();
        foreach (var tile in _availableTiles.Keys)
            if (!_availableSet.Contains(tile)) _removeBuffer.Add(tile);

        foreach (var tile in _removeBuffer)
        {
            placement?.RemoveAvailability(tile);
            _availableTiles.Remove(tile);
        }

        // Add newly available tiles (not yet tracked).
        foreach (var tile in _availableSet)
        {
            if (_availableTiles.ContainsKey(tile)) continue;
            var instance = placement?.PlaceAvailability(tile, availableAlpha, availableTag);
            if (instance != null) _availableTiles[tile] = instance;
        }
    }

    private void ClearAllGhosts()
    {
        foreach (var kvp in _availableTiles)
            placement?.RemoveAvailability(kvp.Key);

        _availableTiles.Clear();
        _availableSet.Clear();
    }

    private void UpdateSelectedTileHighlight(TileGrid.Tile tile)
    {
        if (tile != null)
        {
            var runtimeTile = runtime?.Get(tile);
            if (runtimeTile == null || runtimeTile.occupied || !runtimeTile.available)
            {
                selection?.ClearSelectedTile();
                tile = null;
            }
        }
        if (tile == _highlightedTile) return;

        if (_highlightedTile != null)
        {
            placement?.RemoveAvailability(_highlightedTile);

            if (_availableTiles.ContainsKey(_highlightedTile))
            {
                var ghost = placement.PlaceAvailability(_highlightedTile, availableAlpha, availableTag);
                _availableTiles[_highlightedTile] = ghost;
            }
            else
            {
                _availableTiles.Remove(_highlightedTile);
            }

        }
        _selectedTileInstance = null;

        if (tile == null) { _highlightedTile = null; return; }

        // Prevent double overlay: remove the regular "available" ghost for this tile
        // before showing the dedicated selected highlight ghost.
        if (_availableTiles.ContainsKey(tile))
        {
            placement?.RemoveAvailability(tile);
            _availableTiles.Remove(tile);
        }

        _selectedTileInstance = placement?.PlaceAvailabilitySelected(tile, selectedAlpha, selectedTag);

        _highlightedTile = tile;
    }

    private void ClearSelectedTileHighlight()
    {
        if (_highlightedTile != null)
            placement?.RemoveAvailability(_highlightedTile);
        _selectedTileInstance = null;
        _highlightedTile = null;
    }

    private void TryPlaceHighlightedTile()
    {
        if (!isActiveAndEnabled || !Application.isPlaying || _deckDepleted)
            return;

        if (!TryGetPointerDown(out var position, out var pointerId))
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId))
            return;

        if (!TryGetAvailableTileUnderPointer(position, out var targetTile))
            return;

        if (placement == null)
            return;

        if (_highlightedTile != targetTile)
            UpdateSelectedTileHighlight(targetTile);

        var runtimeTile = runtime?.Get(targetTile);
        if (runtimeTile == null || runtimeTile.occupied || !runtimeTile.available)
            return;

        var rotation = grid ? grid.transform.rotation : Quaternion.identity;
        TileDraw draw = null;
        if (tileDeck != null)
        {
            draw = tileDeck.DrawTile();
            if(draw == null)
                return;
        }

        int habitatCountBefore = runtimeTile.habitatIds != null ? runtimeTile.habitatIds.Count : 0;
        PlacementFeedbackKind prePlacementHint = EvaluatePrePlacementFeedback(targetTile, draw);

        // Promuj gotowego ghosta zamiast Instantiate+Populate — zero kosztu przy stawianiu.
        // Fallback na zwykłe PlaceOccupant jeśli hoverPreview nie jest przypisany lub ghost niedostępny.
        GameObject instance;
        if (hoverPreview != null)
        {
            var (prebuilt, ghostRoot) = hoverPreview.TakeActiveGhostForPlacement();
            instance = placement.PlaceOccupantFromPrebuilt(targetTile, rotation, draw, prebuilt, ghostRoot);
        }
        else
        {
            instance = placement.PlaceOccupant(targetTile, rotation, draw);
        }

        if (instance == null)
            return;

        var placedRuntime = runtime?.Get(targetTile);
        int habitatCountAfter = placedRuntime?.habitatIds != null ? placedRuntime.habitatIds.Count : habitatCountBefore;
        PlacementFeedbackKind finalFeedback =
            habitatCountAfter > habitatCountBefore ? PlacementFeedbackKind.Habitat : prePlacementHint;

        PlayPlacementFeedback(instance, finalFeedback);
        selection?.ClearSelectedTile();
        RefreshAvailability();
    
    }

    private bool TryGetAvailableTileUnderPointer(Vector2 screenPosition, out TileGrid.Tile tile)
    {
        tile = null;
        var camera = Camera.main;
        if (!camera || query == null) return false;
        var ray = camera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            return false;
            
        if (!query.TryGetNearestTile(hit.point, out var nearestTile, 500f))
            return false;

        if (!_availableSet.Contains(nearestTile))
            return false;

        tile = nearestTile;
        return true;
    }

    private bool TryGetPointerDown(out Vector2 position, out int pointerId)
    {
        position = default;
        pointerId = int.MinValue;

        var ts = Touchscreen.current;
        if (ts != null)
        {
            foreach (var touch in ts.touches)
            {
                if (touch == null || !touch.press.wasPressedThisFrame) continue;
                position = touch.position.ReadValue();
                pointerId = touch.touchId.ReadValue();
                return true;
            }
        }
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            position = mouse.position.ReadValue();
            pointerId = MousePointerId;
            return true;
        }
        var pen = Pen.current;
        if (pen != null && pen.tip.wasPressedThisFrame)
        {
            position = pen.position.ReadValue();
            pointerId = MousePointerId;
            return true;
        }
        return false;
    }
    private void OnDeckEmptied()
    {
        _deckDepleted = true;
        ClearSelectedTileHighlight();
        _availableTiles.Clear();
        enabled = false;
    }

    private PlacementFeedbackKind EvaluatePrePlacementFeedback(TileGrid.Tile targetTile, TileDraw draw)
    {
        if (runtime == null || targetTile == null || draw == null)
            return PlacementFeedbackKind.Fail;

        int maxTiles = classifier != null ? classifier.MaxTilesPerHabitat : 5;
        int maxSteps = classifier != null ? classifier.MaxGraphStepsFromPlacement : 5;
        HabitatHoverEvaluator.Evaluate(runtime, targetTile, draw, maxTiles, maxSteps, _feedbackScratch, out HabitatHoverResult hover);

        return hover.Kind switch
        {
            HabitatHoverPreviewKind.Green => PlacementFeedbackKind.Habitat,
            HabitatHoverPreviewKind.Yellow => PlacementFeedbackKind.Almost,
            _ => PlacementFeedbackKind.Fail
        };
    }

    private void PlayPlacementFeedback(GameObject instance, PlacementFeedbackKind kind)
    {
        if (instance != null)
            StartCoroutine(PulseRoutine(instance.transform, pulsePeakScale, pulseDuration));

        PlayFeedbackSfx(kind);
    }

    private IEnumerator PulseRoutine(Transform target, float peakScale, float duration)
    {
        if (target == null)
            yield break;

        Vector3 baseScale = target.localScale;
        float half = Mathf.Max(0.01f, duration * 0.5f);
        float t = 0f;
        while (t < half)
        {
            if (target == null) yield break;
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / half);
            target.localScale = Vector3.Lerp(baseScale, baseScale * peakScale, p);
            yield return null;
        }

        t = 0f;
        Vector3 peak = baseScale * peakScale;
        while (t < half)
        {
            if (target == null) yield break;
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / half);
            target.localScale = Vector3.Lerp(peak, baseScale, p);
            yield return null;
        }

        if (target != null)
            target.localScale = baseScale;
    }

    private void PlayFeedbackSfx(PlacementFeedbackKind kind)
    {
        if (feedbackAudioSource == null)
            return;

        AudioClip clip;
        float volume;
        switch (kind)
        {
            case PlacementFeedbackKind.Habitat:
                clip = habitatSuccessClip;
                volume = habitatVolume;
                break;
            case PlacementFeedbackKind.Almost:
                clip = almostHabitatClip;
                volume = almostVolume;
                break;
            default:
                clip = failedPlacementClip;
                volume = failVolume;
                break;
        }

        if (clip != null)
            feedbackAudioSource.PlayOneShot(clip, volume);
    }
}

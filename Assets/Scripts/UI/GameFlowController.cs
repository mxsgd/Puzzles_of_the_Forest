using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Puzzles of the Forest — przepływ sesji: menu → gra (30 kafli) → ekran końca.
/// </summary>
[DisallowMultipleComponent]
public class GameFlowController : MonoBehaviour
{
    public const int SessionTileCount = 30;

    [Header("References")]
    [SerializeField] private GameUI gameUI;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private TileGrid tileGrid;
    [SerializeField] private TileDeck tileDeck;
    [SerializeField] private TilePlacementService placement;
    [SerializeField] private TileRuntimeStore runtimeStore;
    [SerializeField] private TileAvailabilityService availability;
    [SerializeField] private TileAvailabilityVisualizer availabilityVisualizer;
    [SerializeField] private TileNextTileHoverPreview hoverPreview;
    [SerializeField] private TileClickSelector clickSelector;
    [SerializeField] private TileSelectionModel selection;
    [SerializeField] private HabitatGridManager habitatGridManager;
    [SerializeField] private PerkManager perkManager;
    [SerializeField] private CameraWASDController cameraController;

    [Header("Session")]
    [SerializeField] private bool bonusTilesOnHabitat = true;
    [SerializeField, Min(0)] private int bonusTilesPerHabitat = 3;

    private Canvas _menuCanvas;
    private RectTransform _mainMenuRoot;
    private RectTransform _gameOverRoot;
    private RectTransform _howToPlayRoot;

    private TextMeshProUGUI _gameOverScore;
    private TextMeshProUGUI _gameOverHabitats;
    private TextMeshProUGUI _gameOverBestChain;

    private bool _sessionActive;

    private void Awake()
    {
        if (!gameManager) gameManager = GetComponent<GameManager>();
        if (!gameManager) gameManager = FindAnyObjectByType<GameManager>();

        if (!gameUI) gameUI = FindAnyObjectByType<GameUI>();
        if (!tileGrid) tileGrid = FindAnyObjectByType<TileGrid>();
        if (!tileDeck) tileDeck = FindAnyObjectByType<TileDeck>();
        if (!placement) placement = FindAnyObjectByType<TilePlacementService>();
        if (!runtimeStore) runtimeStore = FindAnyObjectByType<TileRuntimeStore>();
        if (!availability) availability = FindAnyObjectByType<TileAvailabilityService>();
        if (!availabilityVisualizer) availabilityVisualizer = FindAnyObjectByType<TileAvailabilityVisualizer>();
        if (!hoverPreview) hoverPreview = FindAnyObjectByType<TileNextTileHoverPreview>();
        if (!clickSelector) clickSelector = FindAnyObjectByType<TileClickSelector>();
        if (!selection) selection = FindAnyObjectByType<TileSelectionModel>();
        if (!habitatGridManager) habitatGridManager = FindAnyObjectByType<HabitatGridManager>();
        if (!perkManager) perkManager = FindAnyObjectByType<PerkManager>();
        if (!cameraController) cameraController = FindAnyObjectByType<CameraWASDController>();
    }

    private void Start()
    {
        BuildMenuCanvas();
        SetGameplayEnabled(false);
        gameUI?.SetGameplayHudVisible(false);
        ShowMainMenu(clearBoard: false);
    }

    private void OnEnable()
    {
        if (tileDeck != null)
            tileDeck.DeckEmptied += OnDeckEmptied;
    }

    private void OnDisable()
    {
        if (tileDeck != null)
            tileDeck.DeckEmptied -= OnDeckEmptied;
    }

    private void OnDeckEmptied()
    {
        if (!_sessionActive) return;
        EndSession();
    }

    // -------------------------------------------------------------------------
    // Session
    // -------------------------------------------------------------------------

    private void StartSession()
    {
        _sessionActive = true;
        cameraController?.ResetToSessionStart();
        HideAllMenus();
        gameUI?.ResetSessionStats();
        gameUI?.SetGameplayHudVisible(true);
        SetGameplayEnabled(true);

        placement?.ClearBoard();
        perkManager?.OnSessionStart();
        tileDeck?.ConfigureSessionRewards(bonusTilesOnHabitat, bonusTilesPerHabitat);
        tileDeck?.RebuildDeck();
        habitatGridManager?.Rebuild();

        hoverPreview?.ResetForNewSession();
        HabitatAnimalPlacement.ResetForNewSession();
        selection?.ClearSelectedTile();
        clickSelector?.ClearSelection();

        if (!PlaceSessionStartTile())
            Debug.LogError("[GameFlow] Nie udało się postawić kafelka startowego po PLAY.");

        availability?.RebuildCache();
        availabilityVisualizer?.ResetForNewSession();
    }

    /// <summary>Stawia kafel startowy na środku — bez zużycia karty z talii.</summary>
    private bool PlaceSessionStartTile()
    {
        if (!tileGrid) tileGrid = FindAnyObjectByType<TileGrid>();
        if (!placement) placement = FindAnyObjectByType<TilePlacementService>();
        if (!runtimeStore) runtimeStore = FindAnyObjectByType<TileRuntimeStore>();
        if (!tileDeck) tileDeck = FindAnyObjectByType<TileDeck>();

        if (tileGrid == null || placement == null || runtimeStore == null)
        {
            Debug.LogError("[GameFlow] PlaceSessionStartTile: brak TileGrid / Placement / RuntimeStore.");
            return false;
        }

        var center = tileGrid.GetCenterTile();
        if (center == null)
        {
            tileGrid.BuildGrid();
            center = tileGrid.GetCenterTile();
        }
        if (center == null)
        {
            Debug.LogError("[GameFlow] PlaceSessionStartTile: brak kafelka środkowego.");
            return false;
        }

        if (runtimeStore.Get(center).occupied)
            return true;

        if (tileDeck != null && (tileDeck.IsEmpty || tileDeck.Current == null))
            tileDeck.RebuildDeck();

        TileDraw draw = tileDeck?.Current;
        if (draw == null || draw.prefab == null)
        {
            var fallbackBiome = TileBiome.Forested;
            var prefab = tileDeck != null ? tileDeck.GetPrefabFor(fallbackBiome) : null;
            if (prefab == null)
            {
                Debug.LogError("[GameFlow] PlaceSessionStartTile: brak prefabu (talia / TileDeck.baseTilePrefab).");
                return false;
            }
            draw = new TileDraw(fallbackBiome, prefab, null, "Start");
        }

        var instance = placement.PlaceOccupant(center, tileGrid.transform.rotation, draw);
        if (instance == null)
        {
            Debug.LogError("[GameFlow] PlaceSessionStartTile: PlaceOccupant zwrócił null.");
            return false;
        }

        return true;
    }

    private void EndSession()
    {
        if (!_sessionActive) return;
        _sessionActive = false;

        SetGameplayEnabled(false);
        gameUI?.SetGameplayHudVisible(false);
        ShowGameOver();
    }

    private void SetGameplayEnabled(bool enabled)
    {
        if (availabilityVisualizer != null) availabilityVisualizer.enabled = enabled;
        if (hoverPreview != null) hoverPreview.enabled = enabled;
        if (clickSelector != null) clickSelector.enabled = enabled;
    }

    // -------------------------------------------------------------------------
    // Menu visibility
    // -------------------------------------------------------------------------

    private void ShowMainMenu(bool clearBoard = true)
    {
        _sessionActive = false;
        Time.timeScale = 1f;
        HideAllMenus();
        SetGameplayEnabled(false);
        gameUI?.SetGameplayHudVisible(false);
        if (clearBoard)
            placement?.ClearBoard();
        selection?.ClearSelectedTile();
        if (_mainMenuRoot != null) _mainMenuRoot.gameObject.SetActive(true);
    }

    private void ShowGameOver()
    {
        HideAllMenus();
        if (_gameOverRoot == null) return;

        if (gameUI != null)
        {
            if (_gameOverScore != null)
                _gameOverScore.text = gameUI.Score.ToString();
            if (_gameOverHabitats != null)
                _gameOverHabitats.text = $"Habitats created: {gameUI.HabitatCount}";
            if (_gameOverBestChain != null)
                _gameOverBestChain.text = $"Largest habitat: {gameUI.BiggestHabitatChain} tiles";
        }

        _gameOverRoot.gameObject.SetActive(true);
    }

    private void ShowHowToPlay()
    {
        if (_howToPlayRoot != null)
            _howToPlayRoot.gameObject.SetActive(true);
    }

    private void HideHowToPlay()
    {
        if (_howToPlayRoot != null)
            _howToPlayRoot.gameObject.SetActive(false);
    }

    private void HideAllMenus()
    {
        if (_mainMenuRoot != null) _mainMenuRoot.gameObject.SetActive(false);
        if (_gameOverRoot != null) _gameOverRoot.gameObject.SetActive(false);
        HideHowToPlay();
    }

    // -------------------------------------------------------------------------
    // UI build
    // -------------------------------------------------------------------------

    private void BuildMenuCanvas()
    {
        var canvasGo = new GameObject("MenuFlow_Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        _menuCanvas = canvasGo.GetComponent<Canvas>();
        _menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _menuCanvas.sortingOrder = 200;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _mainMenuRoot = BuildMainMenu(canvasGo.transform);
        _gameOverRoot = BuildGameOverScreen(canvasGo.transform);
        _howToPlayRoot = BuildHowToPlayModal(canvasGo.transform);

        _gameOverRoot.gameObject.SetActive(false);
        _howToPlayRoot.gameObject.SetActive(false);
    }

    private RectTransform BuildMainMenu(Transform parent)
    {
        var root = CreateFullScreenPanel(parent, "MainMenu");
        var backdrop = AddBackdrop(root);

        var card = CreateCenterCard(backdrop, "MainMenuCard", new Vector2(520f, 520f));
        AddTitle(card, "Puzzles of the Forest", 36f, new Vector2(0f, -48f));
        AddSubtitle(card,
            "Place tiles, build habitats,\nscore as many points as you can",
            new Vector2(0f, -130f));

        AddMenuButton(card, "PLAY", UISpriteFactory.AccentGreen, UISpriteFactory.TextOnAccent,
            new Vector2(0f, -220f), StartSession);
        AddMenuButton(card, "HOW TO PLAY", UISpriteFactory.PanelLight, UISpriteFactory.TextPrimary,
            new Vector2(0f, -300f), ShowHowToPlay);
        AddMenuButton(card, "QUIT", UISpriteFactory.AccentRed, UISpriteFactory.TextOnAccent,
            new Vector2(0f, -380f), QuitApplication);

        return root;
    }

    private RectTransform BuildGameOverScreen(Transform parent)
    {
        var root = CreateFullScreenPanel(parent, "GameOver");
        var backdrop = AddBackdrop(root);

        var card = CreateCenterCard(backdrop, "GameOverCard", new Vector2(560f, 580f));
        AddTitle(card, "GAME OVER", 34f, new Vector2(0f, -40f));
        AddSubtitle(card, "Deck empty — session ended", new Vector2(0f, -95f));

        _gameOverScore = AddStatLine(card, "Score", "0", UISpriteFactory.ScoreValue, 40f, new Vector2(0f, -175f));
        UISpriteFactory.ApplyScoreValueStyle(_gameOverScore);
        _gameOverHabitats = AddStatLine(card, "Habitats", "—", UISpriteFactory.TextPrimary, 24f, new Vector2(0f, -250f));
        _gameOverBestChain = AddStatLine(card, "Largest habitat", "—", UISpriteFactory.TextPrimary, 24f, new Vector2(0f, -310f));

        AddMenuButton(card, "RESTART", UISpriteFactory.AccentGreen, UISpriteFactory.TextOnAccent,
            new Vector2(0f, -400f), StartSession);
        AddMenuButton(card, "MAIN MENU", UISpriteFactory.PanelLight, UISpriteFactory.TextPrimary,
            new Vector2(0f, -480f), () => ShowMainMenu(clearBoard: true));

        return root;
    }

    private RectTransform BuildHowToPlayModal(Transform parent)
    {
        var root = CreateFullScreenPanel(parent, "HowToPlay");
        var backdrop = AddBackdrop(root);

        var card = CreateCenterCard(backdrop, "HowToPlayCard", new Vector2(640f, 520f));
        AddTitle(card, "HOW TO PLAY", 30f, new Vector2(0f, -36f));

        string body =
            $"• You start with {SessionTileCount} tiles in the deck (+ free starting tile).\n" +
            $"• Each habitat adds {bonusTilesPerHabitat} random tiles to the deck.\n" +
            "• Place tiles on empty cells next to occupied ones.\n" +
            "• Build habitats — connected regions up to 5 tiles.\n" +
            "• Each biome has a vector (meadow, forest, bush, rock, water).\n" +
            "• Fewer tiles in a habitat means more points.\n" +
            "• When the deck runs out, the game ends.\n" +
            "• Reroll (3×) replaces the current card.";

        AddBodyText(card, body, new Vector2(0f, -120f), new Vector2(560f, 280f));
        AddMenuButton(card, "OK", UISpriteFactory.AccentGreen, UISpriteFactory.TextOnAccent,
            new Vector2(0f, -430f), HideHowToPlay);

        return root;
    }

    // -------------------------------------------------------------------------
    // UI helpers
    // -------------------------------------------------------------------------

    private static RectTransform CreateFullScreenPanel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static RectTransform AddBackdrop(RectTransform parent)
    {
        var go = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = UISpriteFactory.Backdrop;
        img.raycastTarget = true;
        return rt;
    }

    private static RectTransform CreateCenterCard(RectTransform parent, string name, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.sprite = UISpriteFactory.RoundedRect(16);
        img.type = Image.Type.Sliced;
        img.color = UISpriteFactory.PanelDark;
        img.raycastTarget = true;
        return rt;
    }

    private static void AddTitle(RectTransform card, string text, float fontSize, Vector2 pos)
    {
        var label = CreateTmp(card, "Title", text, fontSize, UISpriteFactory.TextPrimary, pos,
            new Vector2(card.sizeDelta.x - 48f, 56f), TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
    }

    private static void AddSubtitle(RectTransform card, string text, Vector2 pos)
    {
        CreateTmp(card, "Subtitle", text, 20f, UISpriteFactory.TextMuted, pos,
            new Vector2(card.sizeDelta.x - 56f, 72f), TextAlignmentOptions.Center);
    }

    private static void AddBodyText(RectTransform card, string text, Vector2 pos, Vector2 size)
    {
        var label = CreateTmp(card, "Body", text, 20f, UISpriteFactory.TextPrimary, pos, size,
            TextAlignmentOptions.TopLeft);
        label.enableWordWrapping = true;
        label.lineSpacing = 4f;
    }

    private static TextMeshProUGUI AddStatLine(RectTransform card, string caption, string value,
        Color valueColor, float valueSize, Vector2 pos)
    {
        CreateTmp(card, "Caption", caption, 16f, UISpriteFactory.TextMuted, pos + new Vector2(0f, 22f),
            new Vector2(card.sizeDelta.x - 48f, 24f), TextAlignmentOptions.Center);
        return CreateTmp(card, "Value", value, valueSize, valueColor, pos,
            new Vector2(card.sizeDelta.x - 48f, 48f), TextAlignmentOptions.Center);
    }

    private static void AddMenuButton(RectTransform card, string label, Color bg, Color textColor,
        Vector2 pos, Action onClick)
    {
        var go = new GameObject(label + "_Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(card, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(card.sizeDelta.x - 80f, 56f);
        rt.anchoredPosition = pos;

        var img = go.GetComponent<Image>();
        img.sprite = UISpriteFactory.RoundedRect(12);
        img.type = Image.Type.Sliced;
        img.color = bg;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.colors = UISpriteFactory.MakeButtonColors(bg);
        btn.onClick.AddListener(() => onClick?.Invoke());

        var tmp = CreateTmp((RectTransform)go.transform, "Label", label, 22f, textColor, Vector2.zero,
            Vector2.zero, TextAlignmentOptions.Center);
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 3f;
    }

    private static TextMeshProUGUI CreateTmp(RectTransform parent, string name, string text,
        float fontSize, Color color, Vector2 anchoredPos, Vector2 sizeDelta,
        TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta == Vector2.zero ? new Vector2(200f, 40f) : sizeDelta;

        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static void QuitApplication()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

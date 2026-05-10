using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Tile = TileGrid.Tile;

/// <summary>
/// Podstawowe UI gry:
///  - w prawym górnym rogu pokazuje następny kafel do położenia (ikona),
///  - liczy punkty wg HabitatRequirements (score ≈ basePoints / liczba kafli habitatu).
///
/// Wszystkie referencje (Canvas, Image, Text) są opcjonalne — jeśli nie zostaną
/// podpięte w Inspectorze, komponent sam zbuduje minimalne UI w trybie play.
/// </summary>
public class GameUI : MonoBehaviour
{
    [Header("Źródła danych")]
    [SerializeField] private TileDeck deck;

    [Header("UI (opcjonalnie — jeśli puste, zostanie utworzone w runtime)")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Image nextTileIcon;
    [SerializeField] private Text nextTileLabel;
    [SerializeField] private Text scoreLabel;

    [Header("Layout autobudowanego UI")]
    [SerializeField] private Vector2 panelSize = new Vector2(260f, 120f);
    [SerializeField] private Vector2 panelMargin = new Vector2(20f, 20f);
    [SerializeField] private Color panelBackground = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color textColor = Color.white;

    private int _score;
    private int _habitatCount;

    public int Score => _score;
    public int HabitatCount => _habitatCount;

    private void Awake()
    {
        if (!deck) deck = FindAnyObjectByType<TileDeck>();
    }

    private void Start()
    {
        EnsureUIBuilt();
        RefreshNextTile();
        RefreshScore();
    }

    private void OnEnable()
    {
        if (deck != null) deck.DeckChanged += OnDeckChanged;
        TileEvents.HabitatAssigned += OnHabitatAssigned;
    }

    private void OnDisable()
    {
        if (deck != null) deck.DeckChanged -= OnDeckChanged;
        TileEvents.HabitatAssigned -= OnHabitatAssigned;
    }

    private void OnDeckChanged(IReadOnlyList<TileDraw> _) => RefreshNextTile();

    private void OnHabitatAssigned(HabitatAssignmentData data)
    {
        if (data.Animal == HabitatAnimal.None) return;
        _habitatCount++;
        _score += data.PointsAwarded;
        RefreshScore();
    }

    public void ResetScore()
    {
        _score = 0;
        _habitatCount = 0;
        RefreshScore();
    }

    private void RefreshNextTile()
    {
        var next = deck != null ? deck.Current : null;
        int remaining = deck != null ? deck.GetQueuedTiles().Count : 0;

        if (nextTileIcon != null)
        {
            nextTileIcon.sprite = next?.icon;
            nextTileIcon.enabled = nextTileIcon.sprite != null;
            nextTileIcon.color = Color.white;
        }

        if (nextTileLabel != null)
        {
            nextTileLabel.text = $"Kafli w talii: {remaining}";
        }
    }

    private void RefreshScore()
    {
        if (scoreLabel != null)
            scoreLabel.text = $"Punkty: {_score}\nHabitaty: {_habitatCount}";
    }

    private void EnsureUIBuilt()
    {
        if (canvas == null)
        {
            var canvasGo = new GameObject("GameUI_Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (nextTileIcon == null || nextTileLabel == null)
            BuildNextTilePanel();

        if (scoreLabel == null)
            BuildScorePanel();
    }

    private void BuildNextTilePanel()
    {
        var panel = CreatePanel("NextTilePanel", panelSize,
            anchorMin: new Vector2(1f, 1f),
            anchorMax: new Vector2(1f, 1f),
            pivot:     new Vector2(1f, 1f),
            anchoredPosition: new Vector2(-panelMargin.x, -panelMargin.y));

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(panel, false);
        var iconRT = iconGo.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0f, 0.5f);
        iconRT.anchorMax = new Vector2(0f, 0.5f);
        iconRT.pivot = new Vector2(0f, 0.5f);
        iconRT.anchoredPosition = new Vector2(10f, 0f);
        iconRT.sizeDelta = new Vector2(panelSize.y - 20f, panelSize.y - 20f);
        var icon = iconGo.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.enabled = false;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(panel, false);
        var labelRT = labelGo.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 0f);
        labelRT.anchorMax = new Vector2(1f, 1f);
        labelRT.pivot = new Vector2(0.5f, 0.5f);
        labelRT.offsetMin = new Vector2(panelSize.y, 8f);
        labelRT.offsetMax = new Vector2(-10f, -8f);
        var label = labelGo.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 22;
        label.color = textColor;
        label.alignment = TextAnchor.MiddleLeft;
        label.text = "Kafli w talii: 0";

        nextTileIcon = icon;
        nextTileLabel = label;
    }

    private void BuildScorePanel()
    {
        var size = new Vector2(panelSize.x, 80f);
        var panel = CreatePanel("ScorePanel", size,
            anchorMin: new Vector2(1f, 1f),
            anchorMax: new Vector2(1f, 1f),
            pivot:     new Vector2(1f, 1f),
            anchoredPosition: new Vector2(-panelMargin.x, -(panelMargin.y + panelSize.y + 10f)));

        var labelGo = new GameObject("Score", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(panel, false);
        var rt = labelGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(12f, 8f);
        rt.offsetMax = new Vector2(-12f, -8f);

        var text = labelGo.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 24;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleLeft;
        text.text = "Punkty: 0\nHabitaty: 0";

        scoreLabel = text;
    }

    private RectTransform CreatePanel(
        string name,
        Vector2 size,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPosition;

        var bg = go.GetComponent<Image>();
        bg.color = panelBackground;
        bg.raycastTarget = false;

        return rt;
    }
}

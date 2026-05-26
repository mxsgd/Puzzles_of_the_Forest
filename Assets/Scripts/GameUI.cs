using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Główne UI gry — kompletnie proceduralne, brak ręcznego przeciągania referencji.
///
/// Layout:
///  ┌────────────────────────────────────────────────────────────┐
///  │ [Score card]                            [Pause btn]        │
///  │                                                            │
///  │                              [Next tile card]              │
///  │                              [Reroll btn (3)]              │
///  └────────────────────────────────────────────────────────────┘
///
/// Komponenty satelitarne:
///  - PauseMenuController (auto-dodawany) — modal z Resume/Settings/Quit + ESC.
///  - TileDeck.RerollCurrent — wymienia aktualną kartę za koszt 1 rerolla.
/// </summary>
public class GameUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TileDeck deck;

    [Header("Rerolle")]
    [SerializeField, Min(0)] private int startingRerolls = 3;

    private int _score;
    private int _habitatCount;
    private int _biggestHabitatChain;
    private int _scoreDisplayed;
    private int _rerollsLeft;

    private Canvas _canvas;
    private PauseMenuController _pauseMenu;

    // Score card.
    private TextMeshProUGUI _scoreValueLabel;
    private TextMeshProUGUI _habitatCountLabel;

    // Next tile card.
    private Image _nextTileIcon;
    private TextMeshProUGUI _nextTileName;
    private TextMeshProUGUI _nextTileQueue;

    // Reroll.
    private Button _rerollButton;
    private TextMeshProUGUI _rerollLabel;

    public int Score => _score;
    public int HabitatCount => _habitatCount;
    public int BiggestHabitatChain => _biggestHabitatChain;
    public int RerollsLeft => _rerollsLeft;

    private void Awake()
    {
        if (!deck) deck = FindAnyObjectByType<TileDeck>();
        _rerollsLeft = startingRerolls;
        BuildUI();
    }

    private void Start()
    {
        RefreshNextTile();
        RefreshScore(animate: false);
        RefreshReroll();
    }

    public void SetGameplayHudVisible(bool visible)
    {
        if (_canvas != null)
            _canvas.gameObject.SetActive(visible);
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
        if (data.TileCount > _biggestHabitatChain)
            _biggestHabitatChain = data.TileCount;
        RefreshScore(animate: true);
    }

    public void ResetSessionStats()
    {
        _score = 0;
        _habitatCount = 0;
        _biggestHabitatChain = 0;
        _scoreDisplayed = 0;
        _rerollsLeft = PerkManager.Instance != null
            ? PerkManager.Instance.GetTotalStartingRerolls(startingRerolls)
            : startingRerolls;
        RefreshScore(animate: false);
        RefreshReroll();
        RefreshNextTile();
    }

    // ── UI build ────────────────────────────────────────────────────────────
    private void BuildUI()
    {
        var canvasGo = new GameObject("GameUI_Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        BuildScoreCard();
        BuildNextTileCard();
        BuildPauseButton();
        BuildPauseMenu();

        // Domyślnie ukryte — GameFlowController pokazuje po Play.
        _canvas.gameObject.SetActive(false);
    }

    // ── Score card (top-left) ───────────────────────────────────────────────
    private void BuildScoreCard()
    {
        var card = CreateCard("ScoreCard", new Vector2(300f, 130f),
            anchor: new Vector2(0f, 1f),
            pivot:  new Vector2(0f, 1f),
            anchoredPosition: new Vector2(28f, -28f));

        // Score label (small caption).
        var caption = CreateLabel(card, "WYNIK", 16f, UISpriteFactory.TextMuted,
            anchorMin: new Vector2(0f, 1f),
            anchorMax: new Vector2(1f, 1f),
            pivot:     new Vector2(0.5f, 1f),
            sizeDelta: new Vector2(0f, 24f),
            anchoredPosition: new Vector2(0f, -14f),
            alignment: TextAlignmentOptions.Center);
        caption.fontStyle = FontStyles.Bold;
        caption.characterSpacing = 6f;

        // Big score value.
        _scoreValueLabel = CreateLabel(card, "0", 44f, UISpriteFactory.AccentGold,
            anchorMin: new Vector2(0f, 0.4f),
            anchorMax: new Vector2(1f, 0.85f),
            pivot:     new Vector2(0.5f, 0.5f),
            sizeDelta: Vector2.zero,
            anchoredPosition: Vector2.zero,
            alignment: TextAlignmentOptions.Center);
        _scoreValueLabel.fontStyle = FontStyles.Bold;

        // Habitat count.
        _habitatCountLabel = CreateLabel(card, "Habitaty: 0", 18f, UISpriteFactory.TextPrimary,
            anchorMin: new Vector2(0f, 0f),
            anchorMax: new Vector2(1f, 0.35f),
            pivot:     new Vector2(0.5f, 0.5f),
            sizeDelta: Vector2.zero,
            anchoredPosition: Vector2.zero,
            alignment: TextAlignmentOptions.Center);
    }

    // ── Next tile card (top-right) + reroll button ──────────────────────────
    private void BuildNextTileCard()
    {
        var card = CreateCard("NextTileCard", new Vector2(320f, 190f),
            anchor: new Vector2(1f, 1f),
            pivot:  new Vector2(1f, 1f),
            anchoredPosition: new Vector2(-28f, -28f));

        // Caption.
        var caption = CreateLabel(card, "NASTĘPNY KAFEL", 14f, UISpriteFactory.TextMuted,
            anchorMin: new Vector2(0f, 1f),
            anchorMax: new Vector2(1f, 1f),
            pivot:     new Vector2(0.5f, 1f),
            sizeDelta: new Vector2(0f, 22f),
            anchoredPosition: new Vector2(0f, -12f),
            alignment: TextAlignmentOptions.Center);
        caption.fontStyle = FontStyles.Bold;
        caption.characterSpacing = 4f;

        // Icon container (left).
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(card, false);
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.anchorMin = new Vector2(0f, 0.15f);
        iconRt.anchorMax = new Vector2(0f, 0.85f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.sizeDelta = new Vector2(110f, 0f);
        iconRt.anchoredPosition = new Vector2(20f, -5f);
        _nextTileIcon = iconGo.GetComponent<Image>();
        _nextTileIcon.preserveAspect = true;
        _nextTileIcon.enabled = false;

        // Name + queue (right).
        _nextTileName = CreateLabel(card, "—", 22f, UISpriteFactory.TextPrimary,
            anchorMin: new Vector2(0.4f, 0.45f),
            anchorMax: new Vector2(1f, 0.85f),
            pivot:     new Vector2(0.5f, 0.5f),
            sizeDelta: Vector2.zero,
            anchoredPosition: new Vector2(-10f, 0f),
            alignment: TextAlignmentOptions.MidlineLeft);
        _nextTileName.fontStyle = FontStyles.Bold;

        _nextTileQueue = CreateLabel(card, "w talii: 0", 16f, UISpriteFactory.TextMuted,
            anchorMin: new Vector2(0.4f, 0.15f),
            anchorMax: new Vector2(1f, 0.45f),
            pivot:     new Vector2(0.5f, 0.5f),
            sizeDelta: Vector2.zero,
            anchoredPosition: new Vector2(-10f, 0f),
            alignment: TextAlignmentOptions.MidlineLeft);

        // Reroll button (below the card).
        BuildRerollButton(card);
    }

    private void BuildRerollButton(RectTransform anchorCard)
    {
        var btnGo = new GameObject("RerollBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(_canvas.transform, false);
        var rt = (RectTransform)btnGo.transform;
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(320f, 60f);
        rt.anchoredPosition = new Vector2(-28f, -228f);

        var img = btnGo.GetComponent<Image>();
        img.sprite = UISpriteFactory.RoundedRect(12);
        img.type = Image.Type.Sliced;
        img.color = UISpriteFactory.AccentGold;

        _rerollButton = btnGo.GetComponent<Button>();
        _rerollButton.targetGraphic = img;
        _rerollButton.colors = UISpriteFactory.MakeButtonColors(UISpriteFactory.AccentGold);
        _rerollButton.onClick.AddListener(TryReroll);

        _rerollLabel = CreateLabel((RectTransform)btnGo.transform, "REROLL  (3)", 22f, UISpriteFactory.TextOnAccent,
            anchorMin: Vector2.zero,
            anchorMax: Vector2.one,
            pivot:     new Vector2(0.5f, 0.5f),
            sizeDelta: Vector2.zero,
            anchoredPosition: Vector2.zero,
            alignment: TextAlignmentOptions.Center);
        _rerollLabel.fontStyle = FontStyles.Bold;
        _rerollLabel.characterSpacing = 4f;
    }

    // ── Pause button (bottom-left corner) ──────────────────────────────────
    private void BuildPauseButton()
    {
        var btnGo = new GameObject("PauseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(_canvas.transform, false);
        var rt = (RectTransform)btnGo.transform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(72f, 72f);
        rt.anchoredPosition = new Vector2(28f, 28f);

        var img = btnGo.GetComponent<Image>();
        img.sprite = UISpriteFactory.RoundedRect(14);
        img.type = Image.Type.Sliced;
        img.color = UISpriteFactory.PanelDark;

        var btn = btnGo.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.colors = UISpriteFactory.MakeButtonColors(UISpriteFactory.PanelDark);
        btn.onClick.AddListener(() => _pauseMenu?.Toggle());

        var icon = CreateLabel((RectTransform)btnGo.transform, "II", 30f, UISpriteFactory.TextPrimary,
            anchorMin: Vector2.zero,
            anchorMax: Vector2.one,
            pivot:     new Vector2(0.5f, 0.5f),
            sizeDelta: Vector2.zero,
            anchoredPosition: Vector2.zero,
            alignment: TextAlignmentOptions.Center);
        icon.fontStyle = FontStyles.Bold;
        icon.characterSpacing = 4f;
    }

    private void BuildPauseMenu()
    {
        _pauseMenu = gameObject.GetComponent<PauseMenuController>()
        ?? gameObject.AddComponent<PauseMenuController>();
        _pauseMenu.Initialize(_canvas);
    }

    // ── Reroll ──────────────────────────────────────────────────────────────
    private void TryReroll()
    {
        bool isFree = PerkManager.Instance != null && PerkManager.Instance.QueryRerollIsFree();
        if (!isFree && _rerollsLeft <= 0) return;
        if (deck == null) return;
        if (!deck.RerollCurrent()) return;
        if (!isFree) _rerollsLeft--;
        RefreshReroll();
    }

    private void RefreshReroll()
    {
        if (_rerollLabel != null)
            _rerollLabel.text = $"REROLL  ({_rerollsLeft})";
        if (_rerollButton != null)
            _rerollButton.interactable = _rerollsLeft > 0 && deck != null && deck.Current != null;
    }

    // ── Refresh ─────────────────────────────────────────────────────────────
    private void RefreshNextTile()
    {
        var next = deck != null ? deck.Current : null;
        int remaining = deck != null ? deck.GetQueuedTiles().Count : 0;

        if (_nextTileIcon != null)
        {
            _nextTileIcon.sprite = next?.icon;
            _nextTileIcon.enabled = _nextTileIcon.sprite != null;
            _nextTileIcon.color = Color.white;
        }
        if (_nextTileName != null)
            _nextTileName.text = next != null ? next.displayName : "—";
        if (_nextTileQueue != null)
            _nextTileQueue.text = $"w talii: {remaining} / {GameFlowController.SessionTileCount}";

        RefreshReroll();
    }

    private Coroutine _scoreTween;
    private void RefreshScore(bool animate)
    {
        if (_habitatCountLabel != null)
            _habitatCountLabel.text = $"Habitaty: {_habitatCount}";

        if (!animate || _scoreValueLabel == null)
        {
            _scoreDisplayed = _score;
            if (_scoreValueLabel != null) _scoreValueLabel.text = _score.ToString();
            return;
        }

        if (_scoreTween != null) StopCoroutine(_scoreTween);
        _scoreTween = StartCoroutine(AnimateScore(_scoreDisplayed, _score));
    }

    private IEnumerator AnimateScore(int from, int to)
    {
        const float dur = 0.55f;
        float t = 0f;
        Vector3 baseScale = _scoreValueLabel.transform.localScale;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            int val = Mathf.RoundToInt(Mathf.Lerp(from, to, p));
            _scoreValueLabel.text = val.ToString();
            float pulse = 1f + 0.15f * Mathf.Sin(p * Mathf.PI);
            _scoreValueLabel.transform.localScale = baseScale * pulse;
            yield return null;
        }
        _scoreValueLabel.text = to.ToString();
        _scoreValueLabel.transform.localScale = baseScale;
        _scoreDisplayed = to;
        _scoreTween = null;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private RectTransform CreateCard(string name, Vector2 size,
        Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_canvas.transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPosition;

        var img = go.GetComponent<Image>();
        img.sprite = UISpriteFactory.RoundedRect(14);
        img.type = Image.Type.Sliced;
        img.color = UISpriteFactory.PanelDark;
        img.raycastTarget = false;
        return rt;
    }

    private static TextMeshProUGUI CreateLabel(
        RectTransform parent, string text, float fontSize, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 sizeDelta, Vector2 anchoredPosition,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = anchoredPosition;

        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }
}

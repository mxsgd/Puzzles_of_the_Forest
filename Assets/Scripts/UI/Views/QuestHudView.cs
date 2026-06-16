using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lewy panel HUD z aktywnym main questem i side questem.
/// Wygeneruj hierarchię: Idle Forest → UI → Generate Quest HUD.
/// </summary>
[DisallowMultipleComponent]
public class QuestHudView : MonoBehaviour
{
    // ── Inspector (opcjonalne) ──────────────────────────────────────────────
    [Header("Main Quest")]
    [SerializeField] private TextMeshProUGUI mainQuestTitle;
    [SerializeField] private TextMeshProUGUI mainQuestGoal;
    [SerializeField] private TextMeshProUGUI mainQuestProgress;
    [SerializeField] private Slider mainQuestBar;
    [SerializeField] private TextMeshProUGUI mainQuestReward;

    [Header("Side Quest")]
    [SerializeField] private TextMeshProUGUI sideQuestTitle;
    [SerializeField] private TextMeshProUGUI sideQuestGoal;
    [SerializeField] private TextMeshProUGUI sideQuestProgress;
    [SerializeField] private Slider sideQuestBar;
    [SerializeField] private TextMeshProUGUI sideQuestReward;

    [Header("Complete Banner")]
    [SerializeField] private GameObject completeBanner;
    [SerializeField] private TextMeshProUGUI completeBannerText;

    [Header("Style")]
    [SerializeField] private Color headerColor    = new Color(0.9f, 0.75f, 0.35f);
    [SerializeField] private Color titleColor     = Color.white;
    [SerializeField] private Color goalColor      = new Color(0.85f, 0.85f, 0.85f);
    [SerializeField] private Color progressColor  = new Color(0.6f, 0.95f, 0.6f);
    [SerializeField] private Color rewardColor    = new Color(0.9f, 0.75f, 0.35f);
    [SerializeField] private Color barFillColor   = new Color(0.35f, 0.75f, 0.4f);
    [SerializeField] private Color panelBgColor   = new Color(0.06f, 0.06f, 0.08f, 0.35f);
    [SerializeField] private float bannerDuration = 2.5f;

    [Header("Canvas (ref or auto-created)")]
    [SerializeField] private Canvas questCanvas;

    private QuestManager _manager;
    private Coroutine _bannerRoutine;
    private bool _panelVisible;
    private bool _built;

    public bool IsConfigured =>
        ResolveCanvas() != null
        && mainQuestTitle != null
        && mainQuestGoal != null
        && mainQuestProgress != null
        && mainQuestBar != null
        && mainQuestReward != null
        && sideQuestTitle != null
        && sideQuestGoal != null
        && sideQuestProgress != null
        && sideQuestBar != null
        && sideQuestReward != null;

    public bool HasMinimumBindings =>
        ResolveCanvas() != null && mainQuestTitle != null && sideQuestTitle != null;

    public Canvas ResolveCanvas()
    {
        if (questCanvas == null)
            questCanvas = GetComponent<Canvas>();
        return questCanvas;
    }

    public bool TryAutoBindFromHierarchy()
    {
        ResolveCanvas();
        if (questCanvas == null) return false;

        mainQuestTitle    ??= FindTmp("mq_title");
        mainQuestGoal     ??= FindTmp("mq_goal");
        mainQuestProgress ??= FindTmp("mq_prog");
        mainQuestBar      ??= FindSlider("mq_bar");
        mainQuestReward   ??= FindTmp("mq_rew");
        sideQuestTitle    ??= FindTmp("sq_title");
        sideQuestGoal     ??= FindTmp("sq_goal");
        sideQuestProgress ??= FindTmp("sq_prog");
        sideQuestBar      ??= FindSlider("sq_bar");
        sideQuestReward   ??= FindTmp("sq_rew");

        if (completeBanner == null)
        {
            var banner = transform.Find("QuestCompleteBanner");
            if (banner != null) completeBanner = banner.gameObject;
        }

        completeBannerText ??= FindTmp("BannerText");

        return HasMinimumBindings;
    }

    public string FormatMissingRefs()
    {
        var missing = new List<string>(12);
        if (ResolveCanvas() == null) missing.Add(nameof(questCanvas));
        if (mainQuestTitle == null) missing.Add(nameof(mainQuestTitle));
        if (mainQuestGoal == null) missing.Add(nameof(mainQuestGoal));
        if (mainQuestProgress == null) missing.Add(nameof(mainQuestProgress));
        if (mainQuestBar == null) missing.Add(nameof(mainQuestBar));
        if (mainQuestReward == null) missing.Add(nameof(mainQuestReward));
        if (sideQuestTitle == null) missing.Add(nameof(sideQuestTitle));
        if (sideQuestGoal == null) missing.Add(nameof(sideQuestGoal));
        if (sideQuestProgress == null) missing.Add(nameof(sideQuestProgress));
        if (sideQuestBar == null) missing.Add(nameof(sideQuestBar));
        if (sideQuestReward == null) missing.Add(nameof(sideQuestReward));
        return missing.Count == 0 ? "none" : string.Join(", ", missing);
    }

    private TextMeshProUGUI FindTmp(string goName)
    {
        foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.gameObject.name == goName) return tmp;
        }
        return null;
    }

    private Slider FindSlider(string goName)
    {
        foreach (var slider in GetComponentsInChildren<Slider>(true))
        {
            if (slider.gameObject.name == goName) return slider;
        }
        return null;
    }

    private void Awake()
    {
        if (ResolveCanvas() == null)
        {
            foreach (var other in GetComponentsInChildren<QuestHudView>(true))
            {
                if (other != this && other.ResolveCanvas() != null)
                {
                    enabled = false;
                    return;
                }
            }
        }
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        QuestManager.QuestProgressChanged += OnProgressChanged;
        QuestManager.QuestCompleted       += OnQuestCompleted;
    }

    private void OnDisable()
    {
        QuestManager.QuestProgressChanged -= OnProgressChanged;
        QuestManager.QuestCompleted       -= OnQuestCompleted;
    }

    private void Start()
    {
        StartCoroutine(InitRoutine());
    }

    private IEnumerator InitRoutine()
    {
        TryAutoBindFromHierarchy();

        if (ResolveCanvas() != null)
        {
            if (!IsConfigured)
            {
                Debug.LogWarning(
                    $"[QuestHudView] Scene quest HUD '{name}' is missing refs: {FormatMissingRefs()}. " +
                    "Wire QuestHudView in Inspector or run Idle Forest → UI → Generate Quest HUD.",
                    this);
            }

            ConfigureAllTmpRefs();
        }
        else if (!IsConfigured && !_built)
        {
            BuildPanel();
            _built = true;
        }
        else
        {
            ConfigureAllTmpRefs();
        }

        // Poczekaj jedną klatkę — font TMP musi załadować się na main thread.
        yield return null;

        _manager = QuestManager.Instance ?? QuestManager.EnsureInstance();
        RefreshAll();
    }

    private void ConfigureAllTmpRefs()
    {
        ConfigureTmp(mainQuestTitle);
        ConfigureTmp(mainQuestGoal);
        ConfigureTmp(mainQuestProgress);
        ConfigureTmp(mainQuestReward);
        ConfigureTmp(sideQuestTitle);
        ConfigureTmp(sideQuestGoal);
        ConfigureTmp(sideQuestProgress);
        ConfigureTmp(sideQuestReward);
        ConfigureTmp(completeBannerText);
    }

    public void SetVisible(bool visible)
    {
        _panelVisible = visible;
        var canvas = ResolveCanvas();
        if (canvas != null)
            canvas.gameObject.SetActive(visible);
    }

    // ── Odświeżanie ──────────────────────────────────────────────────────────
    private void OnProgressChanged() => RefreshAll();

    private void OnQuestCompleted(QuestDefinition quest, string rewardLabel)
    {
        RefreshAll();
        ShowCompleteBanner($"Quest complete!\n{quest.displayName}\n{rewardLabel}");
    }

    private void RefreshAll()
    {
        if (_manager == null)
            _manager = QuestManager.Instance;
        if (_manager == null) return;

        RefreshQuest(
            _manager.ActiveMainQuest,
            _manager.MainQuestCurrentValue,
            _manager.MainQuestProgress,
            mainQuestTitle, mainQuestGoal, mainQuestProgress, mainQuestBar, mainQuestReward,
            "MAIN QUEST");

        RefreshQuest(
            _manager.ActiveSideQuest,
            _manager.SideQuestCurrentValue,
            _manager.SideQuestProgress,
            sideQuestTitle, sideQuestGoal, sideQuestProgress, sideQuestBar, sideQuestReward,
            "SIDE QUEST");
    }

    private static void RefreshQuest(
        QuestDefinition quest,
        int currentValue,
        float progress,
        TextMeshProUGUI titleLabel,
        TextMeshProUGUI goalLabel,
        TextMeshProUGUI progressLabel,
        Slider bar,
        TextMeshProUGUI rewardLabel,
        string fallbackHeader)
    {
        if (quest == null)
        {
            if (titleLabel) titleLabel.text = fallbackHeader;
            if (goalLabel)  goalLabel.text  = "All quests completed!";
            if (progressLabel) progressLabel.text = "";
            if (bar)        bar.value = 1f;
            if (rewardLabel) rewardLabel.text = "";
            return;
        }

        if (titleLabel)    titleLabel.text    = quest.displayName;
        if (goalLabel)     goalLabel.text     = quest.description;
        if (progressLabel) progressLabel.text = $"{currentValue} / {quest.conditionTarget}";
        if (bar)           bar.value          = progress;
        if (rewardLabel)   rewardLabel.text   = $"↳ {quest.RewardsLabel()}";
    }

    // ── Banner ───────────────────────────────────────────────────────────────
    private void ShowCompleteBanner(string message)
    {
        if (completeBanner == null) return;
        if (completeBannerText != null) completeBannerText.text = message;

        if (_bannerRoutine != null)
            StopCoroutine(_bannerRoutine);
        _bannerRoutine = StartCoroutine(BannerRoutine());
    }

    private IEnumerator BannerRoutine()
    {
        completeBanner.SetActive(true);
        yield return new WaitForSecondsRealtime(bannerDuration);
        completeBanner.SetActive(false);
        _bannerRoutine = null;
    }

    // ── Procedural build ─────────────────────────────────────────────────────
    private void BuildPanel()
    {
        // Canvas — szukaj rodzica lub stwórz nowy
        var canvasGo = new GameObject("QuestHud_Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        questCanvas = canvasGo.GetComponent<Canvas>();
        questCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        questCanvas.sortingOrder = 99;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Root panel
        var panel = MakeRect("QuestPanel", canvasGo.transform);
        panel.anchorMin = new Vector2(0f, 0.5f);
        panel.anchorMax = new Vector2(0f, 0.5f);
        panel.pivot     = new Vector2(0f, 0.5f);
        // Wejście “wyżej” względem środka ekranu.
        panel.anchoredPosition = new Vector2(12f, 120f);
        panel.sizeDelta = new Vector2(270f, 320f);

        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = panelBgColor;
        MakeRoundedCorners(bg);

        float y = -16f;

        // ── Main Quest ──
        MakeHeader("MAIN QUEST", panel, headerColor, ref y);
        mainQuestTitle    = MakeLabel("mq_title", panel, 16f, titleColor,    FontStyles.Bold, ref y, 2f);
        mainQuestGoal     = MakeLabel("mq_goal",  panel, 12f, goalColor,     FontStyles.Normal, ref y, 4f);
        mainQuestProgress = MakeLabel("mq_prog",  panel, 11f, progressColor, FontStyles.Normal, ref y, 2f);
        mainQuestBar      = MakeSlider("mq_bar",  panel, barFillColor, ref y, 8f);
        mainQuestReward   = MakeLabel("mq_rew",   panel, 11f, rewardColor,   FontStyles.Italic, ref y, 12f);

        MakeSeparator(panel, ref y);

        // ── Side Quest ──
        MakeHeader("SIDE QUEST", panel, headerColor, ref y);
        sideQuestTitle    = MakeLabel("sq_title", panel, 16f, titleColor,    FontStyles.Bold, ref y, 2f);
        sideQuestGoal     = MakeLabel("sq_goal",  panel, 12f, goalColor,     FontStyles.Normal, ref y, 4f);
        sideQuestProgress = MakeLabel("sq_prog",  panel, 11f, progressColor, FontStyles.Normal, ref y, 2f);
        sideQuestBar      = MakeSlider("sq_bar",  panel, barFillColor, ref y, 8f);
        sideQuestReward   = MakeLabel("sq_rew",   panel, 11f, rewardColor,   FontStyles.Italic, ref y, 4f);

        // Dopasuj wysokość panelu do zawartości
        panel.sizeDelta = new Vector2(270f, -y + 16f);

        // ── Complete Banner ──
        BuildCompleteBanner(canvasGo.transform);
    }

    private void BuildCompleteBanner(Transform canvasRoot)
    {
        var bannerGo = new GameObject("QuestCompleteBanner",
            typeof(RectTransform), typeof(Image));
        bannerGo.transform.SetParent(canvasRoot, false);

        var rt = bannerGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(500f, 90f);
        rt.anchoredPosition = new Vector2(0f, -20f);

        bannerGo.GetComponent<Image>().color = new Color(0.1f, 0.55f, 0.2f, 0.92f);

        var textGo = new GameObject("BannerText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(bannerGo.transform, false);
        FillRect(textGo.GetComponent<RectTransform>(), 8f);

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        ConfigureTmp(tmp);
        tmp.text = "Quest complete!";
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        completeBanner     = bannerGo;
        completeBannerText = tmp;
        bannerGo.SetActive(false);
    }

    // ── UI helpers ────────────────────────────────────────────────────────────
    private static RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private void MakeHeader(string text, RectTransform parent, Color color, ref float yOffset)
    {
        var go = new GameObject("Header_" + text, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(12, 0);
        rt.offsetMax = new Vector2(-12, 0);
        rt.anchoredPosition = new Vector2(0, yOffset);
        rt.sizeDelta        = new Vector2(rt.sizeDelta.x, 20f);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        ConfigureTmp(tmp);
        tmp.text      = text;
        tmp.fontSize  = 10f;
        tmp.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        tmp.color     = color;
        tmp.characterSpacing = 3f;

        yOffset -= 22f;
    }

    private TextMeshProUGUI MakeLabel(string name, RectTransform parent, float size, Color color,
        FontStyles style, ref float yOffset, float extraPad = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(12, 0);
        rt.offsetMax = new Vector2(-12, 0);
        rt.anchoredPosition = new Vector2(0, yOffset);
        rt.sizeDelta        = new Vector2(rt.sizeDelta.x, size * 1.6f);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        ConfigureTmp(tmp);
        tmp.fontSize  = size;
        tmp.fontStyle = style;
        tmp.color     = color;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.enableWordWrapping = true;
        tmp.text = "";

        yOffset -= size * 1.6f + extraPad;
        return tmp;
    }

    private Slider MakeSlider(string name, RectTransform parent, Color fillColor,
        ref float yOffset, float extraPad = 0f)
    {
        var height = 6f;
        var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(12, 0);
        rt.offsetMax = new Vector2(-12, 0);
        rt.anchoredPosition = new Vector2(0, yOffset);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);

        var slider = go.GetComponent<Slider>();
        slider.minValue  = 0f;
        slider.maxValue  = 1f;
        slider.value     = 0f;
        slider.interactable = false;
        slider.transition   = Selectable.Transition.None;

        // Background
        var bgGo = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(go.transform, false);
        FillRect(bgGo.GetComponent<RectTransform>());
        bgGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);
        slider.targetGraphic = bgGo.GetComponent<Image>();

        // Fill area
        var fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        var faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero;
        faRt.anchorMax = Vector2.one;
        faRt.sizeDelta = Vector2.zero;
        faRt.offsetMin = new Vector2(0, 0);
        faRt.offsetMax = new Vector2(-height / 2f, 0);

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(fillArea.transform, false);
        FillRect(fillGo.GetComponent<RectTransform>());
        var fillImg = fillGo.GetComponent<Image>();
        fillImg.color = fillColor;

        slider.fillRect = fillGo.GetComponent<RectTransform>();

        yOffset -= height + extraPad;
        return slider;
    }

    private void MakeSeparator(RectTransform parent, ref float yOffset)
    {
        var go = new GameObject("Sep", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(12, 0);
        rt.offsetMax = new Vector2(-12, 0);
        rt.anchoredPosition = new Vector2(0, yOffset);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, 1f);

        go.GetComponent<Image>().color = new Color(1, 1, 1, 0.12f);
        yOffset -= 13f;
    }

    private static void FillRect(RectTransform rt, float margin = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margin, margin);
        rt.offsetMax = new Vector2(-margin, -margin);
    }

    private static void ConfigureTmp(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;

        var font = TMP_Settings.defaultFontAsset;
        if (font != null)
            tmp.font = font;

        tmp.raycastTarget = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.enableWordWrapping = true;
    }

    private static void MakeRoundedCorners(Image img)
    {
        // Standard Unity Image — rounded corners via sprite/shader is complex;
        // we use a plain solid color for now.
        img.type = Image.Type.Sliced;
    }
}

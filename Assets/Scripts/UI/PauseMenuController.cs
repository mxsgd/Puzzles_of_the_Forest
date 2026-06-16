using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Modal pauzy: backdrop + karta z przyciskami Resume / Settings / Quit.
/// Podsekcja Settings: suwaki głośności (Music / SFX) bindowane do AudioListener i PlayerPrefs.
///
/// - ESC otwiera/zamyka pauzę.
/// - Time.timeScale = 0 podczas pauzy; animacje używają unscaledDeltaTime.
/// - Modal blokuje raycasty na całym ekranie (raycastTarget = true).
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    private const string PrefMusic = "idle_forest.music_volume";
    private const string PrefSfx   = "idle_forest.sfx_volume";

    [SerializeField] private PauseMenuView view;

    private Canvas _hostCanvas;
    private RectTransform _backdrop;
    private CanvasGroup   _backdropGroup;
    private RectTransform _menuCard;
    private RectTransform _settingsCard;

    private Slider _musicSlider;
    private Slider _sfxSlider;

    private bool _isOpen;
    private bool _inSettings;
    private Coroutine _anim;

    public bool IsOpen => _isOpen;

    public void Bind(PauseMenuView menuView)
    {
        view = menuView;
        if (view == null || !view.IsConfigured)
        {
            Debug.LogWarning("[PauseMenuController] PauseMenuView nie jest skonfigurowany.", this);
            return;
        }

        _backdrop = view.Backdrop;
        _backdropGroup = view.BackdropGroup;
        _menuCard = view.MenuCard;
        _settingsCard = view.SettingsCard;
        _musicSlider = view.MusicSlider;
        _sfxSlider = view.SfxSlider;
        _hostCanvas = view.Backdrop != null
            ? view.Backdrop.GetComponentInParent<Canvas>()
            : null;

        view.ResumeButton.onClick.RemoveAllListeners();
        view.ResumeButton.onClick.AddListener(Close);
        view.SettingsButton.onClick.RemoveAllListeners();
        view.SettingsButton.onClick.AddListener(ShowSettings);
        view.QuitButton.onClick.RemoveAllListeners();
        view.QuitButton.onClick.AddListener(Quit);
        view.SettingsBackButton.onClick.RemoveAllListeners();
        view.SettingsBackButton.onClick.AddListener(ShowMenuFromSettings);

        _musicSlider.onValueChanged.RemoveAllListeners();
        _sfxSlider.onValueChanged.RemoveAllListeners();
        _musicSlider.onValueChanged.AddListener(OnMusicChanged);
        _sfxSlider.onValueChanged.AddListener(OnSfxChanged);

        ApplyPersistedVolumes();
        SetMenuVisible(false, instant: true);
    }

    public void Initialize(Canvas hostCanvas)
    {
        _hostCanvas = hostCanvas;
        Build();
        ApplyPersistedVolumes();
        SetMenuVisible(false, instant: true);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            if (_inSettings) ShowMenuFromSettings();
            else Toggle();
        }
    }

    // ── Public API ──────────────────────────────────────────────────────────
    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;
        Time.timeScale = 0f;
        SetMenuVisible(true, instant: false);
        ShowMenuFromSettings();
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        _inSettings = false;
        Time.timeScale = 1f;
        SetMenuVisible(false, instant: false);
    }

    public void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    // ── Build ───────────────────────────────────────────────────────────────
    private void Build()
    {
        _backdrop = CreateFullScreen("PauseBackdrop");
        var img = _backdrop.gameObject.AddComponent<Image>();
        img.color = UISpriteFactory.Backdrop;
        img.raycastTarget = true;

        _backdropGroup = _backdrop.gameObject.AddComponent<CanvasGroup>();
        _backdropGroup.alpha = 0f;
        _backdropGroup.blocksRaycasts = false;

        _menuCard     = BuildMenuCard(_backdrop);
        _settingsCard = BuildSettingsCard(_backdrop);
        _settingsCard.gameObject.SetActive(false);
    }

    private RectTransform BuildMenuCard(RectTransform parent)
    {
        var card = CreateCard(parent, "PauseMenuCard", new Vector2(420f, 460f));
        AddTitle(card, "PAUSED", new Vector2(0f, -36f));

        AddButton(card, "RESUME",   UISpriteFactory.AccentGreen, UISpriteFactory.TextOnAccent,  new Vector2(0f, -120f), Close);
        AddButton(card, "SETTINGS", UISpriteFactory.PanelLight,  UISpriteFactory.TextPrimary,   new Vector2(0f, -200f), ShowSettings);
        AddButton(card, "QUIT",     UISpriteFactory.AccentRed,   UISpriteFactory.TextOnAccent,  new Vector2(0f, -280f), Quit);

        return card;
    }

    private RectTransform BuildSettingsCard(RectTransform parent)
    {
        var card = CreateCard(parent, "SettingsCard", new Vector2(460f, 380f));
        AddTitle(card, "SETTINGS", new Vector2(0f, -36f));

        _musicSlider = AddSlider(card, "Music", new Vector2(0f, -130f));
        _sfxSlider   = AddSlider(card, "SFX", new Vector2(0f, -210f));

        _musicSlider.onValueChanged.AddListener(v => OnMusicChanged(v));
        _sfxSlider.onValueChanged.AddListener(v => OnSfxChanged(v));

        AddButton(card, "BACK", UISpriteFactory.PanelLight, UISpriteFactory.TextPrimary, new Vector2(0f, -310f), ShowMenuFromSettings);
        return card;
    }

    private RectTransform CreateFullScreen(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_hostCanvas.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static RectTransform CreateCard(RectTransform parent, string name, Vector2 size)
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

        // Subtle outline.
        var outline = new GameObject("CardOutline", typeof(RectTransform), typeof(Image));
        outline.transform.SetParent(go.transform, false);
        var ort = (RectTransform)outline.transform;
        ort.anchorMin = Vector2.zero;
        ort.anchorMax = Vector2.one;
        ort.offsetMin = new Vector2(-2f, -2f);
        ort.offsetMax = new Vector2(2f, 2f);
        var oimg = outline.GetComponent<Image>();
        oimg.sprite = UISpriteFactory.RoundedRect(16);
        oimg.type = Image.Type.Sliced;
        oimg.color = new Color(UISpriteFactory.Border.r, UISpriteFactory.Border.g, UISpriteFactory.Border.b, 0.5f);
        oimg.raycastTarget = false;
        outline.transform.SetAsFirstSibling();

        return rt;
    }

    private static void AddTitle(RectTransform card, string text, Vector2 anchoredPosition)
    {
        var go = new GameObject("Title", typeof(RectTransform));
        go.transform.SetParent(card, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(card.sizeDelta.x - 40f, 60f);
        rt.anchoredPosition = anchoredPosition;

        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 36f;
        label.fontStyle = FontStyles.Bold;
        label.color = UISpriteFactory.TextPrimary;
        label.alignment = TextAlignmentOptions.Center;
        label.characterSpacing = 8f;
    }

    private static Button AddButton(RectTransform card, string label, Color baseColor, Color textColor, Vector2 anchoredPosition, System.Action onClick)
    {
        var go = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(card, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(card.sizeDelta.x - 64f, 64f);
        rt.anchoredPosition = anchoredPosition;

        var img = go.GetComponent<Image>();
        img.sprite = UISpriteFactory.RoundedRect(12);
        img.type = Image.Type.Sliced;
        img.color = baseColor;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        btn.colors = UISpriteFactory.MakeButtonColors(baseColor);
        btn.onClick.AddListener(() => onClick?.Invoke());

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 26f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return btn;
    }

    private static Slider AddSlider(RectTransform card, string label, Vector2 anchoredPosition)
    {
        // Row container.
        var row = new GameObject($"Row_{label}", typeof(RectTransform));
        row.transform.SetParent(card, false);
        var rrt = (RectTransform)row.transform;
        rrt.anchorMin = new Vector2(0.5f, 1f);
        rrt.anchorMax = new Vector2(0.5f, 1f);
        rrt.pivot = new Vector2(0.5f, 1f);
        rrt.sizeDelta = new Vector2(card.sizeDelta.x - 64f, 56f);
        rrt.anchoredPosition = anchoredPosition;

        // Label.
        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(rrt, false);
        var lrt = (RectTransform)labelGo.transform;
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(0.35f, 1f);
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        var lt = labelGo.AddComponent<TextMeshProUGUI>();
        lt.text = label;
        lt.fontSize = 22f;
        lt.color = UISpriteFactory.TextMuted;
        lt.alignment = TextAlignmentOptions.MidlineLeft;
        lt.raycastTarget = false;

        // Slider container.
        var slGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        slGo.transform.SetParent(rrt, false);
        var srt = (RectTransform)slGo.transform;
        srt.anchorMin = new Vector2(0.36f, 0.25f);
        srt.anchorMax = new Vector2(1f, 0.75f);
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = Vector2.zero;

        // Background.
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(srt, false);
        var bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImg = bg.GetComponent<Image>();
        bgImg.sprite = UISpriteFactory.RoundedRect(8);
        bgImg.type = Image.Type.Sliced;
        bgImg.color = UISpriteFactory.PanelLight;

        // Fill.
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(srt, false);
        var farRt = (RectTransform)fillArea.transform;
        farRt.anchorMin = Vector2.zero;
        farRt.anchorMax = Vector2.one;
        farRt.offsetMin = new Vector2(2f, 2f);
        farRt.offsetMax = new Vector2(-2f, -2f);

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(farRt, false);
        var fillRt = (RectTransform)fill.transform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        var fillImg = fill.GetComponent<Image>();
        fillImg.sprite = UISpriteFactory.RoundedRect(8);
        fillImg.type = Image.Type.Sliced;
        fillImg.color = UISpriteFactory.AccentGreen;

        // Handle.
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(srt, false);
        var haRt = (RectTransform)handleArea.transform;
        haRt.anchorMin = Vector2.zero;
        haRt.anchorMax = Vector2.one;
        haRt.offsetMin = new Vector2(12f, 0f);
        haRt.offsetMax = new Vector2(-12f, 0f);

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(haRt, false);
        var hRt = (RectTransform)handle.transform;
        hRt.anchorMin = new Vector2(0f, 0.5f);
        hRt.anchorMax = new Vector2(0f, 0.5f);
        hRt.sizeDelta = new Vector2(28f, 28f);
        var hImg = handle.GetComponent<Image>();
        hImg.sprite = UISpriteFactory.Circle(20);
        hImg.color = UISpriteFactory.TextPrimary;

        var slider = slGo.GetComponent<Slider>();
        slider.targetGraphic = hImg;
        slider.fillRect = fillRt;
        slider.handleRect = hRt;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        return slider;
    }

    // ── Volume ─────────────────────────────────────────────────────────────
    private void ApplyPersistedVolumes()
    {
        float music = PlayerPrefs.GetFloat(PrefMusic, 1f);
        float sfx   = PlayerPrefs.GetFloat(PrefSfx,   1f);
        if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(music);
        if (_sfxSlider   != null) _sfxSlider.SetValueWithoutNotify(sfx);
        AudioListener.volume = Mathf.Max(music, sfx);
    }

    private void OnMusicChanged(float v)
    {
        PlayerPrefs.SetFloat(PrefMusic, v);
        AudioListener.volume = Mathf.Max(v, _sfxSlider != null ? _sfxSlider.value : 1f);
    }

    private void OnSfxChanged(float v)
    {
        PlayerPrefs.SetFloat(PrefSfx, v);
        AudioListener.volume = Mathf.Max(v, _musicSlider != null ? _musicSlider.value : 1f);
    }

    // ── Animation ──────────────────────────────────────────────────────────
    private void SetMenuVisible(bool visible, bool instant)
    {
        if (_anim != null) StopCoroutine(_anim);
        _backdropGroup.blocksRaycasts = visible;
        if (instant)
        {
            _backdropGroup.alpha = visible ? 1f : 0f;
            _menuCard.localScale = Vector3.one * (visible ? 1f : 0.8f);
            _backdrop.gameObject.SetActive(visible);
            return;
        }
        _backdrop.gameObject.SetActive(true);
        _anim = StartCoroutine(AnimateMenu(visible));
    }

    private IEnumerator AnimateMenu(bool visible)
    {
        const float dur = 0.18f;
        float t = 0f;
        float fromAlpha = _backdropGroup.alpha;
        float toAlpha   = visible ? 1f : 0f;
        Vector3 fromScale = _menuCard.localScale;
        Vector3 toScale   = Vector3.one * (visible ? 1f : 0.8f);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            _backdropGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, p);
            _menuCard.localScale = Vector3.Lerp(fromScale, toScale, p);
            yield return null;
        }

        _backdropGroup.alpha = toAlpha;
        _menuCard.localScale = toScale;
        if (!visible) _backdrop.gameObject.SetActive(false);
        _anim = null;
    }

    private void ShowSettings()
    {
        _inSettings = true;
        _menuCard.gameObject.SetActive(false);
        _settingsCard.gameObject.SetActive(true);
        _settingsCard.localScale = Vector3.one;
    }

    private void ShowMenuFromSettings()
    {
        _inSettings = false;
        _settingsCard.gameObject.SetActive(false);
        _menuCard.gameObject.SetActive(true);
        _menuCard.localScale = Vector3.one;
    }

    private void Quit()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Procedural UI for the perk draft screen. Shown when PerkManager fires DraftTriggered.
/// Three perk cards with icon, name, description; reroll buttons per slot; pick confirms.
/// Follows the project's pattern of fully procedural UI (no prefabs).
/// </summary>
[DisallowMultipleComponent]
public class PerkDraftUI : MonoBehaviour
{
    [SerializeField] private PerkManager perkManager;

    private Canvas _canvas;
    private RectTransform _root;
    private RectTransform _backdrop;
    private RectTransform _card;
    private TextMeshProUGUI _titleLabel;
    private TextMeshProUGUI _rerollsLeftLabel;

    private readonly SlotUI[] _slots = new SlotUI[3];

    private struct SlotUI
    {
        public RectTransform Root;
        public Image IconImage;
        public TextMeshProUGUI NameLabel;
        public TextMeshProUGUI DescLabel;
        public Button PickButton;
        public Button RerollButton;
    }

    private void Awake()
    {
        if (!perkManager) perkManager = FindAnyObjectByType<PerkManager>();
        BuildUI();
        Hide();
    }

    private void OnEnable()
    {
        if (perkManager != null)
            perkManager.DraftTriggered += OnDraftTriggered;
    }

    private void OnDisable()
    {
        if (perkManager != null)
            perkManager.DraftTriggered -= OnDraftTriggered;
    }

    private void OnDraftTriggered()
    {
        Refresh();
        Show();
    }

    private void Show()
    {
        if (_root != null) _root.gameObject.SetActive(true);
        Time.timeScale = 0f;
    }

    private void Hide()
    {
        if (_root != null) _root.gameObject.SetActive(false);
        Time.timeScale = 1f;
    }

    private void Refresh()
    {
        var state = perkManager?.RunState;
        if (state == null) return;

        for (int i = 0; i < 3; i++)
        {
            var def = state.CurrentOffer[i];
            RefreshSlot(i, def);
        }

        if (_rerollsLeftLabel != null)
            _rerollsLeftLabel.text = $"Draft rerolls: {state.DraftRerollsRemaining}";
    }

    private void RefreshSlot(int index, PerkDefinition def)
    {
        var slot = _slots[index];
        if (def == null)
        {
            if (slot.NameLabel != null) slot.NameLabel.text = "---";
            if (slot.DescLabel != null) slot.DescLabel.text = "";
            if (slot.IconImage != null) slot.IconImage.enabled = false;
            if (slot.PickButton != null) slot.PickButton.interactable = false;
            if (slot.RerollButton != null) slot.RerollButton.interactable = false;
            return;
        }

        if (slot.NameLabel != null) slot.NameLabel.text = def.displayName;
        if (slot.DescLabel != null) slot.DescLabel.text = def.description;
        if (slot.IconImage != null)
        {
            slot.IconImage.sprite = def.icon;
            slot.IconImage.enabled = def.icon != null;
        }
        if (slot.PickButton != null) slot.PickButton.interactable = true;

        var state = perkManager?.RunState;
        if (slot.RerollButton != null)
            slot.RerollButton.interactable = state != null && state.DraftRerollsRemaining > 0;
    }

    private void OnPickSlot(int index)
    {
        var picked = perkManager?.DraftConfirmPick(index);
        if (picked != null)
            Hide();
    }

    private void OnRerollSlot(int index)
    {
        if (perkManager == null) return;
        if (perkManager.DraftRerollSlot(index))
            Refresh();
    }

    // -------------------------------------------------------------------------
    // UI construction
    // -------------------------------------------------------------------------

    private void BuildUI()
    {
        var canvasGo = new GameObject("PerkDraftUI_Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 300;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _root = CreateFullScreenPanel(canvasGo.transform, "DraftRoot");

        // Backdrop
        var bdGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        bdGo.transform.SetParent(_root, false);
        var bdRt = StretchFull((RectTransform)bdGo.transform);
        bdGo.GetComponent<Image>().color = UISpriteFactory.Backdrop;

        // Center card
        _card = CreateCenterCard(_root, "DraftCard", new Vector2(900f, 600f));

        // Title
        _titleLabel = CreateTmp(_card, "Title", "CHOOSE A PERK", 30f,
            UISpriteFactory.TextPrimary, new Vector2(0f, -30f), new Vector2(800f, 48f));
        _titleLabel.fontStyle = FontStyles.Bold;

        // Rerolls left label
        _rerollsLeftLabel = CreateTmp(_card, "RerollsLeft", "Draft rerolls: 3", 18f,
            UISpriteFactory.TextMuted, new Vector2(0f, -68f), new Vector2(400f, 30f));

        // 3 slot cards
        float slotW = 250f;
        float slotH = 400f;
        float gap = 30f;
        float totalW = slotW * 3 + gap * 2;
        float startX = -totalW / 2f + slotW / 2f;

        for (int i = 0; i < 3; i++)
        {
            float x = startX + i * (slotW + gap);
            _slots[i] = BuildSlotUI(_card, i, new Vector2(x, -100f), new Vector2(slotW, slotH));
        }
    }

    private SlotUI BuildSlotUI(RectTransform parent, int index, Vector2 pos, Vector2 size)
    {
        var go = new GameObject($"Slot_{index}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        var img = go.GetComponent<Image>();
        img.sprite = UISpriteFactory.RoundedRect(10);
        img.type = Image.Type.Sliced;
        img.color = UISpriteFactory.PanelLight;
        img.raycastTarget = false;

        // Icon
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(rt, false);
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 1f);
        iconRt.pivot = new Vector2(0.5f, 1f);
        iconRt.sizeDelta = new Vector2(80f, 80f);
        iconRt.anchoredPosition = new Vector2(0f, -20f);
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.enabled = false;
        iconImg.raycastTarget = false;

        // Name
        var nameLabel = CreateTmp(rt, "Name", "---", 20f,
            UISpriteFactory.TextPrimary, new Vector2(0f, -110f), new Vector2(size.x - 20f, 32f));
        nameLabel.fontStyle = FontStyles.Bold;

        // Desc
        var descLabel = CreateTmp(rt, "Desc", "", 16f,
            UISpriteFactory.TextMuted, new Vector2(0f, -150f), new Vector2(size.x - 24f, 110f));
        descLabel.alignment = TextAlignmentOptions.Top;

        // Pick button
        var pickBtn = CreateButton(rt, "CHOOSE", UISpriteFactory.AccentGreen, UISpriteFactory.TextOnAccent,
            new Vector2(0f, -300f), new Vector2(size.x - 30f, 44f));
        int capturedIndex = index;
        pickBtn.onClick.AddListener(() => OnPickSlot(capturedIndex));

        // Reroll button
        var rerollBtn = CreateButton(rt, "REROLL", UISpriteFactory.AccentGold, UISpriteFactory.TextOnAccent,
            new Vector2(0f, -352f), new Vector2(size.x - 30f, 36f));
        rerollBtn.onClick.AddListener(() => OnRerollSlot(capturedIndex));

        return new SlotUI
        {
            Root = rt,
            IconImage = iconImg,
            NameLabel = nameLabel,
            DescLabel = descLabel,
            PickButton = pickBtn,
            RerollButton = rerollBtn
        };
    }

    // -------------------------------------------------------------------------
    // UI helpers
    // -------------------------------------------------------------------------

    private static RectTransform CreateFullScreenPanel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return StretchFull((RectTransform)go.transform);
    }

    private static RectTransform StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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

    private static TextMeshProUGUI CreateTmp(RectTransform parent, string name, string text,
        float fontSize, Color color, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static Button CreateButton(RectTransform parent, string label, Color bg, Color textColor,
        Vector2 pos, Vector2 size)
    {
        var go = new GameObject(label + "_Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        var img = go.GetComponent<Image>();
        img.sprite = UISpriteFactory.RoundedRect(10);
        img.type = Image.Type.Sliced;
        img.color = bg;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.colors = UISpriteFactory.MakeButtonColors(bg);

        var tmp = CreateTmp(rt, "Label", label, 18f, textColor, Vector2.zero, Vector2.zero);
        var tmpRt = (RectTransform)tmp.transform;
        tmpRt.anchorMin = Vector2.zero;
        tmpRt.anchorMax = Vector2.one;
        tmpRt.offsetMin = Vector2.zero;
        tmpRt.offsetMax = Vector2.zero;
        tmp.fontStyle = FontStyles.Bold;

        return btn;
    }
}

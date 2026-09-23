using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small row of active-perk icons anchored to the bottom-right corner, growing leftward as more
/// perks are picked. The most recently picked perk always sits closest to the corner; older picks
/// get pushed left. Fully procedural, no prefabs — same convention as GameUI / PerkDraftUI /
/// QuestHudView. Auto-attached by GameManager; visibility driven by GameFlowController alongside
/// the rest of the gameplay HUD.
/// </summary>
[DisallowMultipleComponent]
public class ActivePerksHudView : MonoBehaviour
{
    [SerializeField] private PerkManager perkManager;

    [Header("Layout")]
    [SerializeField] private float iconSize = 44f;
    [SerializeField] private float spacing = 8f;
    [SerializeField] private Vector2 cornerMargin = new(24f, 24f);

    private struct IconSlot
    {
        public RectTransform Root;
        public Image Icon;
    }

    private Canvas _canvas;
    private RectTransform _root;
    private readonly List<IconSlot> _slots = new();

    private void Awake()
    {
        if (!perkManager) perkManager = FindAnyObjectByType<PerkManager>();
        BuildUI();
        SetVisible(false);
    }

    private void OnEnable()
    {
        if (perkManager != null)
            perkManager.PerkActivated += OnPerkActivated;
    }

    private void OnDisable()
    {
        if (perkManager != null)
            perkManager.PerkActivated -= OnPerkActivated;
    }

    private void OnPerkActivated(PerkDefinition _) => Refresh();

    /// <summary>Show/hide alongside the rest of the gameplay HUD; also refreshes content.</summary>
    public void SetVisible(bool visible)
    {
        if (_canvas != null) _canvas.gameObject.SetActive(visible);
        if (visible) Refresh();
    }

    private void Refresh()
    {
        if (!perkManager) perkManager = FindAnyObjectByType<PerkManager>();
        var active = perkManager != null ? perkManager.RunState?.ActivePerks : null;
        int count = active?.Count ?? 0;

        EnsureSlotCount(count);

        for (int i = 0; i < _slots.Count; i++)
        {
            bool show = i < count;
            _slots[i].Root.gameObject.SetActive(show);
            if (!show) continue;

            // Slot 0 (closest to the corner) = most recently picked = last entry in the list.
            var def = active[count - 1 - i];
            _slots[i].Icon.sprite = def != null ? def.icon : null;
            _slots[i].Icon.enabled = def != null && def.icon != null;
        }
    }

    private void EnsureSlotCount(int count)
    {
        while (_slots.Count < count)
            _slots.Add(CreateSlot(_slots.Count));
    }

    // -------------------------------------------------------------------------
    // UI construction
    // -------------------------------------------------------------------------

    private void BuildUI()
    {
        var canvasGo = new GameObject("ActivePerksHud_Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var rootGo = new GameObject("ActivePerksRoot", typeof(RectTransform));
        rootGo.transform.SetParent(canvasGo.transform, false);
        _root = (RectTransform)rootGo.transform;
        _root.anchorMin = _root.anchorMax = new Vector2(1f, 0f);
        _root.pivot = new Vector2(1f, 0f);
        _root.anchoredPosition = new Vector2(-cornerMargin.x, cornerMargin.y);
        _root.sizeDelta = Vector2.zero;
    }

    private IconSlot CreateSlot(int index)
    {
        var go = new GameObject($"PerkIcon_{index}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_root, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(iconSize, iconSize);
        rt.anchoredPosition = new Vector2(-index * (iconSize + spacing), 0f);

        var bg = go.GetComponent<Image>();
        bg.sprite = UISpriteFactory.RoundedRect(8);
        bg.type = Image.Type.Sliced;
        bg.color = UISpriteFactory.PanelDark;
        bg.raycastTarget = false;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(rt, false);
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = new Vector2(4f, 4f);
        iconRt.offsetMax = new Vector2(-4f, -4f);

        var iconImg = iconGo.GetComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        iconImg.enabled = false;

        return new IconSlot { Root = rt, Icon = iconImg };
    }
}

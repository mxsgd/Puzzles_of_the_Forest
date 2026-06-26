using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Jeden slot podglądu habitatu: zaokrąglone tło (status) + ikona zwierzęcia (bez tintu statusu).
/// </summary>
public class HabitatPreviewSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI deficitHintLabel;

    [SerializeField, Min(8f)] private float backgroundSize = 96f;
    [SerializeField, Min(8f)] private float iconSize = 64f;
    [SerializeField, Min(1)] private int backgroundCornerRadius = 24;
    [SerializeField, Min(40f)] private float deficitHintWidth = 140f;
    [SerializeField, FormerlySerializedAs("deficitHintBelowSlot")]
    private float deficitHintAboveSlot = 10f;

    public RectTransform RectTransform => transform as RectTransform;
    public HabitatAnimal Animal { get; private set; }
    public HabitatHoverPreviewKind PreviewKind { get; private set; }

    public event Action<HabitatPreviewSlot> PointerEntered;
    public event Action<HabitatPreviewSlot> PointerExited;

    public void ConfigureSizes(float bgSize, float icSize, int cornerRadius, float hintFontSize, Color hintColor,
        float hintAboveSlot = 10f, float hintWidth = 140f)
    {
        backgroundSize = bgSize;
        iconSize = icSize;
        backgroundCornerRadius = cornerRadius;
        deficitHintAboveSlot = hintAboveSlot;
        deficitHintWidth = hintWidth;
        if (RectTransform != null)
            RectTransform.sizeDelta = new Vector2(backgroundSize, backgroundSize);

        ApplyHintLayout(hintFontSize, hintColor);
    }

    private void ApplyHintLayout(float hintFontSize, Color hintColor)
    {
        if (deficitHintLabel == null)
            return;

        deficitHintLabel.fontSize = hintFontSize;
        deficitHintLabel.color = hintColor;
        var hintRt = deficitHintLabel.rectTransform;
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.sizeDelta = new Vector2(deficitHintWidth, hintFontSize * 2.4f);
        hintRt.anchoredPosition = new Vector2(0f, backgroundSize * 0.5f + deficitHintAboveSlot);
    }

    public void EnsureBuilt()
    {
        if (backgroundImage != null && iconImage != null)
            return;

        var rt = RectTransform;
        if (rt == null)
            rt = gameObject.AddComponent<RectTransform>();

        rt.sizeDelta = new Vector2(backgroundSize, backgroundSize);
        if (backgroundImage == null)
        {
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(transform, false);
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            backgroundImage = bgGo.GetComponent<Image>();
        }

        if (iconImage == null)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(transform, false);
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(iconSize, iconSize);
            iconRt.anchoredPosition = Vector2.zero;
            iconImage = iconGo.GetComponent<Image>();
        }

        if (deficitHintLabel == null)
        {
            var hintGo = new GameObject("DeficitHint", typeof(RectTransform));
            hintGo.transform.SetParent(transform, false);
            var hintRt = (RectTransform)hintGo.transform;
            hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 0.5f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.sizeDelta = new Vector2(deficitHintWidth, 36f);
            hintRt.anchoredPosition = new Vector2(0f, backgroundSize * 0.5f + deficitHintAboveSlot);

            deficitHintLabel = hintGo.AddComponent<TextMeshProUGUI>();
            deficitHintLabel.alignment = TextAlignmentOptions.Center;
            deficitHintLabel.enableWordWrapping = true;
            deficitHintLabel.overflowMode = TextOverflowModes.Truncate;
            deficitHintLabel.raycastTarget = false;
            deficitHintLabel.fontSize = 16f;
        }
    }

    public void Show(Sprite animalSprite, HabitatHoverPreviewKind kind, HabitatAnimal animal,
        Color grayBackground, Color yellowBackground, Color greenBackground, Color iconTint,
        string deficitHint = null)
    {
        EnsureBuilt();
        Animal = animal;
        PreviewKind = kind;

        if (backgroundImage != null)
        {
            backgroundImage.sprite = UISpriteFactory.RoundedRect(backgroundCornerRadius);
            backgroundImage.type = Image.Type.Sliced;
            backgroundImage.color = GetBackgroundColor(kind, grayBackground, yellowBackground, greenBackground);
            backgroundImage.raycastTarget = true;
            backgroundImage.enabled = true;
            backgroundImage.material = null;
        }

        if (iconImage != null)
        {
            var iconRt = (RectTransform)iconImage.transform;
            iconRt.sizeDelta = new Vector2(iconSize, iconSize);
            iconRt.SetAsLastSibling();

            iconImage.sprite = animalSprite;
            iconImage.type = Image.Type.Simple;
            iconImage.color = new Color(iconTint.r, iconTint.g, iconTint.b, 1f);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.enabled = animalSprite != null;
            iconImage.material = null;
            iconImage.SetAllDirty();
        }

        if (deficitHintLabel != null)
        {
            bool showHint = kind == HabitatHoverPreviewKind.Yellow
                && !string.IsNullOrEmpty(deficitHint);
            deficitHintLabel.text = showHint ? deficitHint : string.Empty;
            deficitHintLabel.enabled = showHint;
            if (showHint)
                ApplyHintLayout(deficitHintLabel.fontSize, deficitHintLabel.color);
        }

        gameObject.SetActive(true);
    }

    public void Clear()
    {
        Animal = HabitatAnimal.None;
        PreviewKind = HabitatHoverPreviewKind.Gray;

        if (backgroundImage != null)
        {
            backgroundImage.enabled = false;
            backgroundImage.raycastTarget = false;
        }

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (deficitHintLabel != null)
        {
            deficitHintLabel.text = string.Empty;
            deficitHintLabel.enabled = false;
        }

        gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData) => PointerEntered?.Invoke(this);

    public void OnPointerExit(PointerEventData eventData) => PointerExited?.Invoke(this);

    public static Color GetBackgroundColor(HabitatHoverPreviewKind kind,
        Color gray, Color yellow, Color green)
    {
        return kind switch
        {
            HabitatHoverPreviewKind.Green  => green,
            HabitatHoverPreviewKind.Yellow => yellow,
            _                              => gray
        };
    }
}

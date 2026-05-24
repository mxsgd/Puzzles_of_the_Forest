using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Jeden slot podglądu habitatu: zaokrąglone tło (status) + ikona zwierzęcia (bez tintu statusu).
/// </summary>
public class HabitatPreviewSlot : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;

    [SerializeField, Min(8f)] private float backgroundSize = 96f;
    [SerializeField, Min(8f)] private float iconSize = 64f;
    [SerializeField, Min(1)] private int backgroundCornerRadius = 24;

    public RectTransform RectTransform => transform as RectTransform;

    public void ConfigureSizes(float bgSize, float icSize, int cornerRadius)
    {
        backgroundSize = bgSize;
        iconSize = icSize;
        backgroundCornerRadius = cornerRadius;
        if (RectTransform != null)
            RectTransform.sizeDelta = new Vector2(backgroundSize, backgroundSize);
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
    }

    public void Show(Sprite animalSprite, HabitatHoverPreviewKind kind,
        Color grayBackground, Color yellowBackground, Color greenBackground, Color iconTint)
    {
        EnsureBuilt();

        if (backgroundImage != null)
        {
            backgroundImage.sprite = UISpriteFactory.RoundedRect(backgroundCornerRadius);
            backgroundImage.type = Image.Type.Sliced;
            backgroundImage.color = GetBackgroundColor(kind, grayBackground, yellowBackground, greenBackground);
            backgroundImage.raycastTarget = false;
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

        gameObject.SetActive(true);
    }

    public void Clear()
    {
        if (backgroundImage != null)
            backgroundImage.enabled = false;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        gameObject.SetActive(false);
    }

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

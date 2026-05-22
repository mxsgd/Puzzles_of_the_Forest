using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Paleta + proceduralne sprite'y dla GameUI / PauseMenu.
/// Wszystkie sprite'y są generowane raz, cache'owane przez cały runtime.
/// </summary>
public static class UISpriteFactory
{
    // ── Paleta (warm beige / forest theme) ──────────────────────────────────
    public static readonly Color PanelDark      = new Color32(0xE5, 0xD5, 0xB7, 0xF2); // ciepły beż, 95% alpha
    public static readonly Color PanelMid       = new Color32(0xD4, 0xC1, 0x9E, 0xFA); // głębszy beż
    public static readonly Color PanelLight     = new Color32(0xF0, 0xE2, 0xC4, 0xFF); // jasny beż
    public static readonly Color Border         = new Color32(0xA8, 0x92, 0x70, 0xFF); // brąz
    public static readonly Color AccentGreen    = new Color32(0x4F, 0xA5, 0x57, 0xFF);
    public static readonly Color AccentGold     = new Color32(0xD0, 0x8E, 0x1A, 0xFF);
    public static readonly Color AccentRed      = new Color32(0xB8, 0x44, 0x36, 0xFF);
    public static readonly Color TextPrimary    = new Color32(0x3A, 0x2A, 0x1A, 0xFF); // ciemny brąz
    public static readonly Color TextMuted      = new Color32(0x7A, 0x6A, 0x4F, 0xFF); // stonowany brąz
    public static readonly Color TextOnAccent   = new Color32(0xFF, 0xF5, 0xE1, 0xFF); // krem (na ciemnych accentach)
    public static readonly Color Backdrop       = new Color(0f, 0f, 0f, 0.65f);

    private static Sprite _roundedSprite;
    private static Sprite _circleSprite;
    private static Sprite _whiteSprite;
    private static Sprite _iconBracketSprite;

    // ── Rounded rect (9-sliced) ─────────────────────────────────────────────
    public static Sprite RoundedRect(int radius = 12)
    {
        if (_roundedSprite != null) return _roundedSprite;

        int size = radius * 2 + 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32 fill = Color.white;
        Color32 transparent = new Color32(255, 255, 255, 0);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = 0f, dy = 0f;
            if (x < radius)        dx = radius - x;
            else if (x >= size - radius) dx = x - (size - radius - 1);
            if (y < radius)        dy = radius - y;
            else if (y >= size - radius) dy = y - (size - radius - 1);

            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist <= radius)
            {
                // Soft anti-aliasing on the edge.
                float alpha = Mathf.Clamp01(radius - dist);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            else
            {
                tex.SetPixel(x, y, transparent);
            }
        }
        tex.Apply();
        tex.name = "UI_RoundedRect";

        var border = new Vector4(radius, radius, radius, radius);
        _roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        _roundedSprite.name = "UI_RoundedRect_Sprite";
        return _roundedSprite;
    }

    public static Sprite Circle(int radius = 32)
    {
        if (_circleSprite != null) return _circleSprite;

        int size = radius * 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2(radius, radius);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
            float alpha = Mathf.Clamp01(radius - dist);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        tex.Apply();
        tex.name = "UI_Circle";

        _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 100f);
        _circleSprite.name = "UI_Circle_Sprite";
        return _circleSprite;
    }

    public static Sprite White()
    {
        if (_whiteSprite != null) return _whiteSprite;
        var tex = Texture2D.whiteTexture;
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f);
        _whiteSprite.name = "UI_White_Sprite";
        return _whiteSprite;
    }

    /// <summary>
    /// Narożne „bracketi” (L-kształty) — ramka wokół ikony zwierzęcia, jak w UI.
    /// Środek przezroczysty; kolor ustawiasz przez tint / MaterialPropertyBlock.
    /// </summary>
    public static Sprite IconBracket(int size = 64, int armLen = 14, int thickness = 4, int inset = 4)
    {
        if (_iconBracketSprite != null) return _iconBracketSprite;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "UI_IconBracket"
        };

        var clear = new Color32(255, 255, 255, 0);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            tex.SetPixel(x, y, clear);

        void StampH(int x0, int x1, int yRow)
        {
            for (int x = x0; x <= x1; x++)
            for (int dy = 0; dy < thickness; dy++)
            {
                int y = yRow + dy;
                if (y < 0 || y >= size || x < 0 || x >= size) continue;
                tex.SetPixel(x, y, Color.white);
            }
        }

        void StampV(int y0, int y1, int xCol)
        {
            for (int y = y0; y <= y1; y++)
            for (int dx = 0; dx < thickness; dx++)
            {
                int x = xCol + dx;
                if (y < 0 || y >= size || x < 0 || x >= size) continue;
                tex.SetPixel(x, y, Color.white);
            }
        }

        int left = inset;
        int right = size - inset - thickness;
        int bottom = inset;
        int top = size - inset - thickness;

        // Top-left
        StampH(left, left + armLen, top);
        StampV(top - armLen, top, left);
        // Top-right
        StampH(right - armLen, right, top);
        StampV(top - armLen, top, right);
        // Bottom-left
        StampH(left, left + armLen, bottom);
        StampV(bottom, bottom + armLen, left);
        // Bottom-right
        StampH(right - armLen, right, bottom);
        StampV(bottom, bottom + armLen, right);

        tex.Apply();

        _iconBracketSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 100f);
        _iconBracketSprite.name = "UI_IconBracket_Sprite";
        return _iconBracketSprite;
    }

    // ── Color helpers ───────────────────────────────────────────────────────
    public static Color Lighten(Color c, float amount = 0.12f)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        v = Mathf.Clamp01(v + amount);
        var lighter = Color.HSVToRGB(h, s, v);
        lighter.a = c.a;
        return lighter;
    }

    public static Color Darken(Color c, float amount = 0.12f) => Lighten(c, -amount);

    public static ColorBlock MakeButtonColors(Color baseColor)
    {
        return new ColorBlock
        {
            normalColor      = baseColor,
            highlightedColor = Lighten(baseColor, 0.12f),
            pressedColor     = Darken(baseColor, 0.15f),
            selectedColor    = Lighten(baseColor, 0.06f),
            disabledColor    = new Color(baseColor.r, baseColor.g, baseColor.b, 0.4f),
            colorMultiplier  = 1f,
            fadeDuration     = 0.08f
        };
    }
}

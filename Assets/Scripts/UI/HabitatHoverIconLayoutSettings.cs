using System;
using UnityEngine;

/// <summary>
/// Parametry układu ikon podglądu habitatu (hover nad kaflem).
/// Edytowane na <see cref="HabitatHoverIconLayoutMockup"/> w scenie, potem kopiowane do <see cref="TileNextTileHoverPreview"/>.
/// </summary>
[Serializable]
public class HabitatHoverIconLayoutSettings
{
    [Header("Pozycja nad kaflem (świat)")]
    public float iconsAboveTile = 2.2f;

    [Header("Układ poziomy (px ref. 1920)")]
    [Tooltip("Pitch = ScaledBackgroundSize + iconSpacing")]
    public float iconSpacing = 56f;
    [Range(0.5f, 1.25f)]
    public float iconSizeMultiplier = 0.88f;

    [Header("Rozmiary slotu (px ref. 1920)")]
    [Min(8f)] public float backgroundSize = 48f;
    [Min(8f)] public float iconSize = 32f;
    [Min(1)] public int backgroundCornerRadius = 12;

    [Header("Tekst pod żółtą ikoną")]
    [Min(10f)] public float deficitHintFontSize = 18f;
    public Color deficitHintColor = new(1f, 0.96f, 0.75f, 0.95f);
    [Min(40f)] public float deficitHintWidth = 140f;
    [Tooltip("Odstęp tekstu nad ikoną zwierzęcia (px ref. 1920).")]
    public float deficitHintAboveSlot = 10f;

    [Header("Kolory")]
    public Color grayBackgroundColor = new(0.55f, 0.55f, 0.55f, 0.85f);
    public Color yellowBackgroundColor = new(0.96f, 0.86f, 0.35f, 0.95f);
    public Color greenBackgroundColor = new(0.45f, 0.82f, 0.48f, 0.95f);
    public Color iconColor = Color.white;

    public float ScaledBackgroundSize => backgroundSize * iconSizeMultiplier;
    public float ScaledIconSize => iconSize * iconSizeMultiplier;
    public float SlotPitch => ScaledBackgroundSize + iconSpacing;

    public void ConfigureSlot(HabitatPreviewSlot slot)
    {
        if (slot == null) return;
        slot.ConfigureSizes(ScaledBackgroundSize, ScaledIconSize, backgroundCornerRadius,
            deficitHintFontSize, deficitHintColor, deficitHintAboveSlot, deficitHintWidth);
        slot.EnsureBuilt();
    }
}

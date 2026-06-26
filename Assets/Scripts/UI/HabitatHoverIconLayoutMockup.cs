using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Edytorowy podgląd ikon hover nad kaflem — ustaw układ w scenie, potem „Zastosuj do gry”.
/// Ukrywany w Play Mode (chyba że włączysz showInPlayMode).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class HabitatHoverIconLayoutMockup : MonoBehaviour
{
    [SerializeField] private TileNextTileHoverPreview hoverPreview;
    [SerializeField] private HabitatHoverIconLayoutSettings layout = new();

    [Header("Hierarchia podglądu")]
    [SerializeField] private RectTransform iconsAnchor;
    [SerializeField] private HabitatPreviewSlot slotGreen;
    [SerializeField] private HabitatPreviewSlot slotYellowLeft;
    [SerializeField] private HabitatPreviewSlot slotYellowCenter;
    [SerializeField] private HabitatPreviewSlot slotYellowRight;

    [Header("Przykładowe sprite'y (opcjonalnie — inaczej z hover preview)")]
    [SerializeField] private Sprite sampleGreenSprite;
    [SerializeField] private Sprite sampleYellowSprite;
    [SerializeField, TextArea] private string sampleYellowHint = "+2 Forest";

    [Header("Edytor")]
    [SerializeField, Tooltip("Gdy włączone — sloty ustawiane według iconSpacing. Wyłącz, żeby ręcznie przesuwać sloty w scenie.")]
    private bool syncSlotPositionsFromLayout = true;
    [SerializeField, Tooltip("Po przesunięciu slotów żółtych w scenie — odczytaj pitch przy „Zastosuj do gry”.")]
    private bool readSpacingFromSlotPositions;
    [SerializeField] private bool showInPlayMode;

    public bool ReadSpacingFromSlotPositions => readSpacingFromSlotPositions;

    public HabitatHoverIconLayoutSettings Layout => layout;
    public TileNextTileHoverPreview HoverPreview => hoverPreview;

    private void OnEnable()
    {
        if (!hoverPreview)
            hoverPreview = FindAnyObjectByType<TileNextTileHoverPreview>();
        RefreshVisuals();
    }

    private void OnValidate() => RefreshVisuals();

    private void LateUpdate()
    {
        if (!Application.isPlaying || showInPlayMode)
            return;

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    public void RefreshVisuals()
    {
        if (iconsAnchor == null)
            return;

        var greenSp = ResolveSprite(sampleGreenSprite, HabitatAnimal.Deer);
        var yellowSp = ResolveSprite(sampleYellowSprite, HabitatAnimal.Beaver);

        bool sync = syncSlotPositionsFromLayout;
        float pitch = layout.SlotPitch;
        float startX = -pitch;

        ConfigureAndShow(slotGreen, greenSp, HabitatHoverPreviewKind.Green, HabitatAnimal.Deer,
            sync ? Vector2.zero : null);
        ConfigureAndShow(slotYellowLeft, yellowSp, HabitatHoverPreviewKind.Yellow, HabitatAnimal.Beaver,
            sync ? new Vector2(startX, 0f) : null, sampleYellowHint);
        ConfigureAndShow(slotYellowCenter, yellowSp, HabitatHoverPreviewKind.Yellow, HabitatAnimal.Bees,
            sync ? Vector2.zero : null, sampleYellowHint);
        ConfigureAndShow(slotYellowRight, yellowSp, HabitatHoverPreviewKind.Yellow, HabitatAnimal.Bear,
            sync ? new Vector2(startX + 2f * pitch, 0f) : null, sampleYellowHint);
    }

    /// <summary>Odczytaj odstęp z pozycji slotów żółtych (lewy → środek).</summary>
    public void CaptureSpacingFromSlots()
    {
        if (slotYellowLeft == null || slotYellowCenter == null)
            return;

        var leftRt = slotYellowLeft.RectTransform;
        var centerRt = slotYellowCenter.RectTransform;
        if (leftRt == null || centerRt == null)
            return;

        float pitch = centerRt.anchoredPosition.x - leftRt.anchoredPosition.x;
        if (pitch > 1f)
            layout.iconSpacing = pitch - layout.ScaledBackgroundSize;

        if (slotGreen?.RectTransform != null)
        {
            float scaledBg = slotGreen.RectTransform.sizeDelta.x;
            if (scaledBg > 1f && layout.iconSizeMultiplier > 0.01f)
                layout.backgroundSize = scaledBg / layout.iconSizeMultiplier;
        }
    }

    private void ConfigureAndShow(HabitatPreviewSlot slot, Sprite sprite, HabitatHoverPreviewKind kind,
        HabitatAnimal animal, Vector2? localPos, string hint = null)
    {
        if (slot == null) return;

        layout.ConfigureSlot(slot);
        slot.Show(sprite, kind, animal,
            layout.grayBackgroundColor, layout.yellowBackgroundColor, layout.greenBackgroundColor,
            layout.iconColor, hint);

        if (localPos.HasValue && slot.RectTransform != null)
            slot.RectTransform.anchoredPosition = localPos.Value;
    }

    private Sprite ResolveSprite(Sprite overrideSprite, HabitatAnimal fallbackAnimal)
    {
        if (overrideSprite != null)
            return overrideSprite;

        if (hoverPreview == null)
            return null;

        return hoverPreview.TryGetHabitatSprite(fallbackAnimal);
    }
}

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class HabitatHoverIconLayoutEditorUtility
{
    private static readonly string[] LayoutPropertyNames =
    {
        "iconsAboveTile",
        "iconSpacing",
        "iconSizeMultiplier",
        "backgroundSize",
        "iconSize",
        "backgroundCornerRadius",
        "deficitHintFontSize",
        "deficitHintColor",
        "deficitHintWidth",
        "deficitHintAboveSlot",
        "grayBackgroundColor",
        "yellowBackgroundColor",
        "greenBackgroundColor",
        "iconColor"
    };

    public static void PullFromHoverPreview(HabitatHoverIconLayoutMockup mockup)
    {
        if (mockup == null) return;

        var preview = mockup.HoverPreview ?? Object.FindAnyObjectByType<TileNextTileHoverPreview>();
        if (preview == null)
        {
            Debug.LogWarning("HabitatHoverIconLayout: brak TileNextTileHoverPreview w scenie.");
            return;
        }

        var previewSo = new SerializedObject(preview);
        var layoutSo = new SerializedObject(mockup);
        var layoutProp = layoutSo.FindProperty("layout");

        foreach (var name in LayoutPropertyNames)
        {
            var src = previewSo.FindProperty(name);
            var dst = layoutProp.FindPropertyRelative(name);
            if (src != null && dst != null)
                CopyProperty(src, dst);
        }

        layoutSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(mockup);
        mockup.RefreshVisuals();
        Debug.Log("HabitatHoverIconLayout: wczytano ustawienia z TileNextTileHoverPreview.");
    }

    public static void PushToHoverPreview(HabitatHoverIconLayoutMockup mockup)
    {
        if (mockup == null) return;

        if (mockup.Layout == null)
            return;

        if (mockup.ReadSpacingFromSlotPositions)
            mockup.CaptureSpacingFromSlots();

        var preview = mockup.HoverPreview ?? Object.FindAnyObjectByType<TileNextTileHoverPreview>();
        if (preview == null)
        {
            Debug.LogWarning("HabitatHoverIconLayout: brak TileNextTileHoverPreview w scenie.");
            return;
        }

        var previewSo = new SerializedObject(preview);
        var layoutSo = new SerializedObject(mockup);
        var layoutProp = layoutSo.FindProperty("layout");

        foreach (var name in LayoutPropertyNames)
        {
            var dst = previewSo.FindProperty(name);
            var src = layoutProp.FindPropertyRelative(name);
            if (src != null && dst != null)
                CopyProperty(src, dst);
        }

        previewSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(preview);
        Debug.Log("HabitatHoverIconLayout: zastosowano ustawienia na TileNextTileHoverPreview (GameManager).");
    }

    private static void CopyProperty(SerializedProperty src, SerializedProperty dst)
    {
        switch (src.propertyType)
        {
            case SerializedPropertyType.Float:
                dst.floatValue = src.floatValue;
                break;
            case SerializedPropertyType.Integer:
                dst.intValue = src.intValue;
                break;
            case SerializedPropertyType.Color:
                dst.colorValue = src.colorValue;
                break;
            default:
                Debug.LogWarning($"HabitatHoverIconLayout: nieobsługiwany typ pola {src.name}.");
                break;
        }
    }

    [MenuItem("Idle Forest/UI/Create Habitat Hover Icon Layout Mockup")]
    public static void CreateMockupInScene()
    {
        var existing = GameObject.Find("HabitatHoverIconLayoutMockup");
        if (existing != null)
        {
            Selection.activeGameObject = existing;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        var root = new GameObject("HabitatHoverIconLayoutMockup",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
            typeof(HabitatHoverIconLayoutMockup));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var rootRt = (RectTransform)root.transform;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        var anchorGo = new GameObject("IconsAnchor", typeof(RectTransform));
        anchorGo.transform.SetParent(root.transform, false);
        var anchorRt = anchorGo.GetComponent<RectTransform>();
        anchorRt.anchorMin = anchorRt.anchorMax = new Vector2(0.5f, 0.55f);
        anchorRt.pivot = new Vector2(0.5f, 0.5f);
        anchorRt.anchoredPosition = Vector2.zero;
        anchorRt.sizeDelta = Vector2.zero;

        var slotGreen = CreatePreviewSlot(anchorRt, "Slot_Green");
        var slotY0 = CreatePreviewSlot(anchorRt, "Slot_Yellow_Left");
        var slotY1 = CreatePreviewSlot(anchorRt, "Slot_Yellow_Center");
        var slotY2 = CreatePreviewSlot(anchorRt, "Slot_Yellow_Right");

        var mockup = root.GetComponent<HabitatHoverIconLayoutMockup>();
        var so = new SerializedObject(mockup);
        so.FindProperty("iconsAnchor").objectReferenceValue = anchorRt;
        so.FindProperty("slotGreen").objectReferenceValue = slotGreen;
        so.FindProperty("slotYellowLeft").objectReferenceValue = slotY0;
        so.FindProperty("slotYellowCenter").objectReferenceValue = slotY1;
        so.FindProperty("slotYellowRight").objectReferenceValue = slotY2;
        so.FindProperty("hoverPreview").objectReferenceValue =
            Object.FindAnyObjectByType<TileNextTileHoverPreview>();
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(root, "Create Habitat Hover Icon Layout Mockup");
        Selection.activeGameObject = root;
        HabitatHoverIconLayoutEditorUtility.PullFromHoverPreview(mockup);
        mockup.RefreshVisuals();

        if (!Application.isPlaying)
            EditorSceneManagerMarkDirty();
    }

    private static HabitatPreviewSlot CreatePreviewSlot(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return go.AddComponent<HabitatPreviewSlot>();
    }

    private static void EditorSceneManagerMarkDirty()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    }
}

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HabitatHoverIconLayoutMockup))]
public class HabitatHoverIconLayoutMockupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var mockup = (HabitatHoverIconLayoutMockup)target;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Synchronizacja z grą", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Wczytaj z TileNextTileHoverPreview"))
                HabitatHoverIconLayoutEditorUtility.PullFromHoverPreview(mockup);

            if (GUILayout.Button("Zastosuj do gry"))
                HabitatHoverIconLayoutEditorUtility.PushToHoverPreview(mockup);
        }

        if (GUILayout.Button("Odśwież podgląd"))
            mockup.RefreshVisuals();

        if (GUILayout.Button("Ustaw pozycje slotów z Layout"))
        {
            var so = new SerializedObject(mockup);
            so.FindProperty("syncSlotPositionsFromLayout").boolValue = true;
            so.ApplyModifiedProperties();
            mockup.RefreshVisuals();
        }

        EditorGUILayout.HelpBox(
            "1. Dostosuj Layout (rozmiary, kolory, odstępy) lub przesuń sloty żółte w hierarchii.\n" +
            "2. Wyłącz „Sync Slot Positions From Layout”, jeśli ręcznie przesuwasz sloty.\n" +
            "3. Włącz „Read Spacing From Slot Positions”, jeśli odstęp liczysz z pozycji slotów.\n" +
            "4. Kliknij „Zastosuj do gry” — wartości trafią na GameManager → TileNextTileHoverPreview.\n" +
            "Mockup znika w Play Mode (chyba że włączysz Show In Play Mode).",
            MessageType.Info);
    }
}

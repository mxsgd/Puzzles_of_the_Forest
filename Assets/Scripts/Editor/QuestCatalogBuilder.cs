using UnityEditor;
using UnityEngine;

public static class QuestCatalogBuilder
{
    private const string AssetPath = "Assets/Resources/QuestCatalog.asset";

    [MenuItem("Idle Forest/Quests/Ensure Quest Catalog")]
    public static void EnsureQuestCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<QuestCatalog>(AssetPath);

        if (catalog == null)
        {
            EnsureResourcesFolder();
            catalog = ScriptableObject.CreateInstance<QuestCatalog>();
            catalog.PopulateDefaults();
            AssetDatabase.CreateAsset(catalog, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[QuestCatalog] Stworzono nowy katalog questów: {AssetPath}");
        }
        else
        {
            catalog.PopulateDefaults();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[QuestCatalog] Zaktualizowano istniejący katalog: {AssetPath}");
        }

        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);
    }

    [MenuItem("Idle Forest/Quests/Select Quest Catalog")]
    public static void SelectQuestCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<QuestCatalog>(AssetPath);
        if (catalog == null)
        {
            Debug.LogWarning("[QuestCatalog] Brak katalogu. Uruchom Idle Forest → Quests → Ensure Quest Catalog.");
            return;
        }
        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);
    }

    private static void EnsureResourcesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
    }
}

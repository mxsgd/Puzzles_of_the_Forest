using System.IO;
using UnityEditor;
using UnityEngine;

public static class GameSfxCatalogBuilder
{
    private const string CatalogPath = "Assets/Resources/GameSfxCatalog.asset";

    [MenuItem("Idle Forest/Audio/Ensure Game Sfx Catalog")]
    public static void EnsureCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameSfxCatalog>(CatalogPath);
        if (catalog == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            catalog = ScriptableObject.CreateInstance<GameSfxCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.leavesClip = LoadClip("Assets/Audio/SFX/LeavesSFX.wav");
        catalog.rocksClip = LoadClip("Assets/Audio/SFX/RocksSFX.wav");
        catalog.waterClip = LoadClip("Assets/Audio/SFX/WaterSFX.wav");
        catalog.chainTileClip = LoadClip("Assets/Audio/SFX/ScoreSFX_2.wav");
        catalog.popClip = LoadClip("Assets/Audio/SFX/PopSFX.wav");

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        WireSceneComponents(catalog);
        Debug.Log("[GameSfxCatalog] Katalog SFX zaktualizowany: " + CatalogPath);
    }

    private static void WireSceneComponents(GameSfxCatalog catalog)
    {
        var placementSfx = Object.FindAnyObjectByType<TilePlacementSfx>();
        if (placementSfx != null)
        {
            var so = new SerializedObject(placementSfx);
            so.FindProperty("sfxCatalog").objectReferenceValue = catalog;
            so.FindProperty("biomeClips").arraySize = 5;
            SetBiomeClip(so, 0, TileBiome.Forested, catalog.leavesClip, catalog.placementVolume);
            SetBiomeClip(so, 1, TileBiome.Meadow, catalog.leavesClip, catalog.placementVolume);
            SetBiomeClip(so, 2, TileBiome.Bushy, catalog.leavesClip, catalog.placementVolume);
            SetBiomeClip(so, 3, TileBiome.Rocks, catalog.rocksClip, catalog.placementVolume);
            SetBiomeClip(so, 4, TileBiome.Water, catalog.waterClip, catalog.placementVolume);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(placementSfx);
        }

        var chain = Object.FindAnyObjectByType<HabitatChainReactionAnimator>();
        if (chain != null)
        {
            var so = new SerializedObject(chain);
            so.FindProperty("sfxCatalog").objectReferenceValue = catalog;
            so.FindProperty("chainTileClip").objectReferenceValue = catalog.chainTileClip;
            so.FindProperty("chainVolume").floatValue = catalog.chainVolume;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(chain);
        }

        foreach (var animalPlacement in Object.FindObjectsByType<HabitatAnimalPlacement>(FindObjectsSortMode.None))
        {
            var so = new SerializedObject(animalPlacement);
            so.FindProperty("sfxCatalog").objectReferenceValue = catalog;
            so.FindProperty("popClip").objectReferenceValue = catalog.popClip;
            so.FindProperty("popVolume").floatValue = catalog.popVolume;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(animalPlacement);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    private static void SetBiomeClip(SerializedObject so, int index, TileBiome biome, AudioClip clip, float volume)
    {
        var element = so.FindProperty("biomeClips").GetArrayElementAtIndex(index);
        element.FindPropertyRelative("biome").enumValueIndex = (int)biome;
        element.FindPropertyRelative("clip").objectReferenceValue = clip;
        element.FindPropertyRelative("volume").floatValue = volume;
    }

    private static AudioClip LoadClip(string path)
        => AssetDatabase.LoadAssetAtPath<AudioClip>(path);
}

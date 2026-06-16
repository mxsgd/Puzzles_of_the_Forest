using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Zwalnia pule GPU po Play Mode i udostępnia ręczne przebiegi budżetu VRAM.
/// </summary>
[InitializeOnLoad]
public static class EditorPlayModeStability
{
    static EditorPlayModeStability()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode)
            return;

        TileNextTileHoverPreview.PurgeGhostPool();

        if (GpuResourceBudget.Instance != null)
            GpuResourceBudget.Instance.RunHeavyTrim();

        EditorApplication.delayCall += () =>
        {
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
        };
    }
}

public static class IdleForestGraphicsStabilityMenu
{
    [MenuItem("Idle Forest/Stability/Run GPU Budget Pass (Heavy)")]
    public static void RunGpuBudgetPass()
    {
        var budget = GpuResourceBudget.EnsureInstance();
        budget.RunHeavyTrim();
        Debug.Log("[Idle Forest] Wykonano ciężki przebieg GpuResourceBudget (trim pul + UnloadUnusedAssets).");
    }

    [MenuItem("Idle Forest/Stability/Purge Hover Ghost Pool")]
    public static void PurgeHoverGhostPool()
    {
        TileNextTileHoverPreview.PurgeGhostPool();
        GpuResourceBudget.EnsureInstance()?.RunLightTrim();
        Resources.UnloadUnusedAssets();
        Debug.Log("[Idle Forest] Wyczyszczono pulę ghost-preview i przycięto pozostałe pule GPU.");
    }

    [MenuItem("Idle Forest/Stability/Force Standalone DX11 (opcjonalnie)")]
    public static void ForceStandaloneDx11()
    {
        var apis = new[] { GraphicsDeviceType.Direct3D11 };
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, apis);
        Debug.Log("[Idle Forest] Standalone Windows: wymuszono Direct3D11 (tylko buildy gry). " +
                  "Preferuj GpuResourceBudget w edytorze zamiast zmiany API.");
    }
}

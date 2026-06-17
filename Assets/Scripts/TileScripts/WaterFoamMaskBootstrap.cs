using UnityEngine;

/// <summary>Refreshes water-tile foam edge masks after the grid is ready.</summary>
public class WaterFoamMaskBootstrap : MonoBehaviour
{
    [SerializeField] private TileRuntimeStore runtimeStore;
    [SerializeField] private TileGrid grid;

    private void Awake()
    {
        runtimeStore ??= FindFirstObjectByType<TileRuntimeStore>();
        grid ??= FindFirstObjectByType<TileGrid>();
    }

    private void Start()
    {
        WaterFoamMaskUtility.RefreshAllWater(runtimeStore, grid);
    }
}

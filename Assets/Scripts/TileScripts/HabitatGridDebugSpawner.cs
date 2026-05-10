using UnityEngine;

public class HabitatGridDebugSpawner : MonoBehaviour
{
    [SerializeField] private HabitatGridManager manager;
    [SerializeField] private TileGrid grid;
    [SerializeField] private HabitatAnimal sourceAnimal = HabitatAnimal.Deer;
    [SerializeField] private Color sourceColorFallback = new Color(0.6f, 0.9f, 0.5f, 1f);
    [SerializeField, Range(0f, 1f)] private float sourceStrength = 1f;
    [SerializeField] private KeyCode spawnAtCenterKey = KeyCode.H;
    [SerializeField] private KeyCode clearSourcesKey = KeyCode.J;

    private void Awake()
    {
        if (manager == null)
            manager = FindAnyObjectByType<HabitatGridManager>();
        if (grid == null)
            grid = FindAnyObjectByType<TileGrid>();
    }

    private void Update()
    {
        if (manager == null || grid == null)
            return;

        if (Input.GetKeyDown(spawnAtCenterKey))
        {
            TileGrid.Tile center = grid.GetCenterTile();
            if (center != null)
            {
                if (sourceAnimal == HabitatAnimal.None)
                    manager.AddOrUpdateSource(new Vector2Int(center.q, center.r), sourceColorFallback, sourceStrength);
                else
                    manager.AddOrUpdateSource(new Vector2Int(center.q, center.r), sourceAnimal, sourceStrength);
            }
        }

        if (Input.GetKeyDown(clearSourcesKey))
            manager.ClearSources();
    }
}

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class HabitatGridManager : MonoBehaviour
{
    private static readonly int HabitatColorId = Shader.PropertyToID("_HabitatColor");
    private static readonly int HabitatStrengthId = Shader.PropertyToID("_HabitatStrength");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("References")]
    [SerializeField] private TileGrid grid;
    [SerializeField] private TileRuntimeStore runtimeStore;
    [SerializeField] private HabitatTintProfile habitatTintProfile;

    [Header("Tint Settings")]
    [SerializeField, Range(0f, 1f)] private float infectionCommitThreshold = 0.85f;
    [Header("Fallback Tint (materials without habitat properties)")]
    [SerializeField] private bool enableBaseColorFallbackTint = true;
    [SerializeField, Range(0f, 1f)] private float baseColorFallbackStrength = 0.35f;

    [Header("Initial Sources")]
    [SerializeField] private List<HabitatSource> initialSources = new();
    [Header("Chain Reaction")]
    [Tooltip("Gdy true, HabitatAssigned nie infekuje od razu — HabitatChainReactionAnimator kontroluje kolejność.")]
    [SerializeField] private bool deferInfectionToAnimator = false;

    [Header("Debug")]
    [SerializeField] private bool verboseTintDebug;

    private readonly List<HabitatTile> _tiles = new();
    private readonly Dictionary<Vector2Int, int> _indexByPos = new();
    private readonly Dictionary<int, List<Renderer>> _renderersByTileIndex = new();
    private bool[] _infected;
    private Color[] _infectedColor;
    private MaterialPropertyBlock _sharedBlock;
    private bool _dirty;

    public IReadOnlyList<HabitatTile> Tiles => _tiles;

    private void Awake()
    {
        Rebuild();
    }

    private void OnEnable()
    {
        TileEvents.HabitatAssigned += OnHabitatAssigned;
        TileEvents.TileStateChanged += OnTileStateChanged;
        if (_tiles.Count == 0)
            Rebuild();
        _dirty = true;
    }

    private void OnDisable()
    {
        TileEvents.HabitatAssigned -= OnHabitatAssigned;
        TileEvents.TileStateChanged -= OnTileStateChanged;
    }

    private void OnValidate()
    {
        infectionCommitThreshold = Mathf.Clamp01(infectionCommitThreshold);
        baseColorFallbackStrength = Mathf.Clamp01(baseColorFallbackStrength);
    }

    private void Update()
    {
        if (!_dirty || _tiles.Count == 0)
            return;

        ApplyAllToRenderers();
        _dirty = false;
    }

    [ContextMenu("Rebuild Grid Cache")]
    public void Rebuild()
    {
        if (grid == null)
            grid = FindAnyObjectByType<TileGrid>();
        if (grid == null || grid.tiles == null || grid.tiles.Count == 0)
            return;

        _tiles.Clear();
        _indexByPos.Clear();
        _renderersByTileIndex.Clear();

        for (int i = 0; i < grid.tiles.Count; i++)
        {
            TileGrid.Tile tile = grid.tiles[i];
            Vector2Int pos = new(tile.q, tile.r);
            _indexByPos[pos] = i;
            _tiles.Add(new HabitatTile
            {
                gridPos = pos,
                influence = 0f,
                targetInfluence = 0f,
                habitatColor = Color.white
            });
        }

        _infected = new bool[_tiles.Count];
        _infectedColor = new Color[_tiles.Count];
        CacheTileRenderers();
        LoadInitialSources();
        _dirty = true;
    }

    public void AddOrUpdateSource(Vector2Int position, Color color, float strength = 1f)
    {
        InfectTile(position, color, Mathf.Clamp01(strength));
    }

    public void AddOrUpdateSource(Vector2Int position, HabitatAnimal animal, float strength = 1f)
    {
        InfectTile(position, ResolveSourceColor(animal, Color.white), Mathf.Clamp01(strength));
    }

    public void RemoveSource(Vector2Int position) { }
    public void ClearSources() { }

    private void LoadInitialSources()
    {
        if (initialSources == null)
            return;
        for (int i = 0; i < initialSources.Count; i++)
        {
            HabitatSource src = initialSources[i];
            if (src == null)
                continue;
            src.strength = Mathf.Clamp01(src.strength);
            InfectTile(src.position, ResolveSourceColor(src.animal, src.color), src.strength);
        }
    }

    private Color ResolveSourceColor(HabitatAnimal animal, Color fallbackColor)
    {
        if (animal != HabitatAnimal.None &&
            habitatTintProfile != null &&
            habitatTintProfile.TryGetColor(animal, out Color profileColor))
            return profileColor;
        return fallbackColor;
    }

    private void OnTileStateChanged(TileGrid.Tile tile)
    {
        if (tile == null || runtimeStore == null)
            return;

        if (!_indexByPos.TryGetValue(new Vector2Int(tile.q, tile.r), out int tileIndex))
            return;

        TileRuntimeStore.Runtime rt = runtimeStore.Get(tile);
        if (rt == null || !rt.occupied || rt.occupantInstance == null)
        {
            _renderersByTileIndex.Remove(tileIndex);
            return;
        }

        Renderer[] renderers = ResolveHabitatRenderers(rt.occupantInstance);
        if (renderers.Length > 0)
        {
            if (!_renderersByTileIndex.TryGetValue(tileIndex, out List<Renderer> list))
            {
                list = new List<Renderer>(renderers.Length);
                _renderersByTileIndex[tileIndex] = list;
            }
            else
            {
                list.Clear();
            }
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) list.Add(renderers[i]);
        }
        else
        {
            _renderersByTileIndex.Remove(tileIndex);
        }
    }

    private void OnHabitatAssigned(HabitatAssignmentData data)
    {
        if (data.Tiles == null || data.Tiles.Count == 0)
            return;

        if (deferInfectionToAnimator)
            return;

        Color color = ResolveSourceColor(data.Animal, Color.white);
        for (int i = 0; i < data.Tiles.Count; i++)
        {
            TileGrid.Tile tile = data.Tiles[i];
            if (tile == null)
                continue;
            InfectTile(new Vector2Int(tile.q, tile.r), color, 1f);
        }
    }

    /// <summary>Publiczne API dla HabitatChainReactionAnimator.</summary>
    public void InfectTilePublic(Vector2Int position, Color color, float strength)
        => InfectTile(position, color, strength);

    private void InfectTile(Vector2Int position, Color color, float strength)
    {
        if (!_indexByPos.TryGetValue(position, out int idx))
            return;

        strength = Mathf.Clamp01(strength);
        HabitatTile tile = _tiles[idx];
        tile.influence = Mathf.Max(tile.influence, strength);
        tile.targetInfluence = tile.influence;
        tile.habitatColor = color;

        if (tile.influence >= infectionCommitThreshold)
        {
            _infected[idx] = true;
            _infectedColor[idx] = color;
            tile.influence = 1f;
            tile.targetInfluence = 1f;
        }

        _dirty = true;
    }

    private void CacheTileRenderers()
    {
        if (runtimeStore == null)
            runtimeStore = FindAnyObjectByType<TileRuntimeStore>();

        _renderersByTileIndex.Clear();
        TileObject[] tileObjects = FindObjectsByType<TileObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int mapped = 0;
        int withoutCompatibleRenderer = 0;
        for (int i = 0; i < tileObjects.Length; i++)
        {
            TileObject obj = tileObjects[i];
            if (obj == null || obj.Tile == null)
                continue;

            Vector2Int pos = new(obj.Tile.q, obj.Tile.r);
            if (!_indexByPos.TryGetValue(pos, out int tileIndex))
                continue;

            Renderer[] renderers = ResolveHabitatRenderers(obj.gameObject);
            if (renderers.Length > 0)
                mapped += AddRenderersForTile(tileIndex, renderers);
            else
            {
                withoutCompatibleRenderer++;
                if (verboseTintDebug)
                    Debug.LogWarning($"[HabitatGridManager] No compatible renderer for tile [{obj.Tile.q},{obj.Tile.r}] on '{obj.name}'.", obj);
            }
        }

        if (runtimeStore == null)
            return;

        foreach (var item in runtimeStore.GetOccupiedTilesWithRuntime())
        {
            TileGrid.Tile tile = item.tile;
            TileRuntimeStore.Runtime rt = item.runtime;
            if (tile == null || rt == null || rt.occupantInstance == null)
                continue;

            Vector2Int pos = new(tile.q, tile.r);
            if (!_indexByPos.TryGetValue(pos, out int tileIndex))
                continue;

            Renderer[] renderers = ResolveHabitatRenderers(rt.occupantInstance);
            if (renderers.Length > 0)
                mapped += AddRenderersForTile(tileIndex, renderers);
            else
            {
                withoutCompatibleRenderer++;
                if (verboseTintDebug)
                    Debug.LogWarning($"[HabitatGridManager] Runtime occupant '{rt.occupantInstance.name}' on [{tile.q},{tile.r}] has no compatible renderer.", rt.occupantInstance);
            }
        }

        if (verboseTintDebug)
            Debug.Log($"[HabitatGridManager] CacheTileRenderers mapped={mapped}, missing={withoutCompatibleRenderer}", this);
    }

    private static Renderer[] ResolveHabitatRenderers(GameObject root)
    {
        if (root == null)
            return System.Array.Empty<Renderer>();

        var compatible = new List<Renderer>();
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Material[] mats = renderer.sharedMaterials;
            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null)
                    continue;

                if (mat.HasProperty(HabitatColorId) && mat.HasProperty(HabitatStrengthId))
                {
                    compatible.Add(renderer);
                    break;
                }
            }
        }
        return compatible.ToArray();
    }

    private int AddRenderersForTile(int tileIndex, Renderer[] renderers)
    {
        if (!_renderersByTileIndex.TryGetValue(tileIndex, out List<Renderer> list))
        {
            list = new List<Renderer>();
            _renderersByTileIndex[tileIndex] = list;
        }

        int added = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || list.Contains(renderer))
                continue;
            list.Add(renderer);
            added++;
        }
        return added;
    }

    private void ApplyAllToRenderers()
    {

        if (_sharedBlock == null)
            _sharedBlock = new MaterialPropertyBlock();

        foreach (KeyValuePair<int, List<Renderer>> kv in _renderersByTileIndex)
        {
            int idx = kv.Key;
            List<Renderer> renderers = kv.Value;
            if (renderers == null || idx < 0 || idx >= _tiles.Count)
                continue;

            HabitatTile tile = _tiles[idx];
            bool appliedToAnyMaterial = false;
            for (int r = 0; r < renderers.Count; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null)
                    continue;

                Material[] mats = renderer.sharedMaterials;
                for (int matIndex = 0; matIndex < mats.Length; matIndex++)
                {
                    Material mat = mats[matIndex];
                    if (mat == null)
                        continue;

                    bool hasHabitatProps = mat.HasProperty(HabitatColorId) && mat.HasProperty(HabitatStrengthId);
                    int fallbackColorProperty = ResolveFallbackColorProperty(mat);
                    bool canFallbackTint = enableBaseColorFallbackTint && fallbackColorProperty != -1;
                    if (!hasHabitatProps && !canFallbackTint)
                        continue;

                    renderer.GetPropertyBlock(_sharedBlock, matIndex);
                    if (hasHabitatProps)
                    {
                        _sharedBlock.SetColor(HabitatColorId, tile.habitatColor);
                        _sharedBlock.SetFloat(HabitatStrengthId, tile.influence);
                        appliedToAnyMaterial = true;
                    }
                    else
                    {
                        float fallbackBlend = Mathf.Clamp01(tile.influence * baseColorFallbackStrength);
                        Color baseColor = mat.GetColor(fallbackColorProperty);
                        Color tintedColor = Color.Lerp(baseColor, baseColor * tile.habitatColor, fallbackBlend);
                        _sharedBlock.SetColor(fallbackColorProperty, tintedColor);
                        appliedToAnyMaterial = true;
                    }
                    renderer.SetPropertyBlock(_sharedBlock, matIndex);

                    if (verboseTintDebug && tile.influence > 0.01f)
                        Debug.Log($"[HabitatGridManager] tile={tile.gridPos} inf={tile.influence:F2} renderer='{renderer.name}' mat='{mat.name}'", renderer);
                }
            }

            if (verboseTintDebug && !appliedToAnyMaterial && tile.influence > 0.01f)
                Debug.LogWarning($"[HabitatGridManager] Tile {tile.gridPos} inf={tile.influence:F2} — no compatible materials on mapped renderers.");
        }
    }

    private static int ResolveFallbackColorProperty(Material mat)
    {
        if (mat == null)
            return -1;
        if (mat.HasProperty(BaseColorId))
            return BaseColorId;
        if (mat.HasProperty(ColorId))
            return ColorId;
        return -1;
    }
}

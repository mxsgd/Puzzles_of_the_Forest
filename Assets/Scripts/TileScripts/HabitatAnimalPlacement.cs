using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Po utworzeniu habitatu stawia jeden model zwierzęcia na głównym kafelku rdzeniowym (nie wodnym).
/// </summary>
[DisallowMultipleComponent]
public class HabitatAnimalPlacement : MonoBehaviour
{
    [System.Serializable]
    public struct AnimalPrefabEntry
    {
        public HabitatAnimal animal;
        public GameObject prefab;
        [Tooltip("Korekta forward prefabu (stopnie Y).")]
        public float yRotationOffsetDegrees;
        [Tooltip("Dodatkowa wysokość (m) dodawana do globalnego Position Offset Y.")]
        public float heightOffset;
    }

    private struct AnimalSpawnSettings
    {
        public GameObject Prefab;
        public float YRotationDegrees;
        public float HeightOffset;
    }

    [Header("References")]
    [SerializeField] private TileRuntimeStore runtimeStore;
    [SerializeField] private HabitatRulesProfile rulesProfile;
    [SerializeField] private BiomeHabitatClassifier classifier;
    [SerializeField] private TileGrid tileGrid;
    [SerializeField, Tooltip("Rodzic instancji zwierząt (np. _occupants). Nie używaj obiektu ze skalą siatki.")]
    private Transform spawnParent;

    [Header("Animal prefabs")]
    [SerializeField] private AnimalPrefabEntry[] animalPrefabs = new AnimalPrefabEntry[0];

    [Header("Spawn")]
    [SerializeField] private Vector3 positionOffset = new(0f, 0.2f, 0f);
    [SerializeField, Tooltip("Patrz od środka habitatu w stronę kafelka rdzeniowego.")]
    private bool faceOutwardFromHabitatCenter = true;
    [SerializeField] private bool alignToGridRotation = true;
    [SerializeField] private float yRotationOffsetDegrees;

    [Header("Spawn VFX")]
    [SerializeField] private GameObject habitatSpawnVfxPrefab;
    [SerializeField] private Vector3 vfxPositionOffset = Vector3.zero;
    [SerializeField, Min(0f)] private float vfxLifetimeFallback = 3f;

    private static HabitatAnimalPlacement _activeInstance;
    private static readonly Dictionary<int, GameObject> s_spawnedByHabitatId = new();

    private readonly Dictionary<HabitatAnimal, AnimalSpawnSettings> _settingsByAnimal = new();
    private readonly HabitatRegionScratch _coreScratch = new();
    private Transform _resolvedSpawnParent;

    private void Awake()
    {
        if (!runtimeStore) runtimeStore = FindAnyObjectByType<TileRuntimeStore>();
        if (!tileGrid) tileGrid = FindAnyObjectByType<TileGrid>();
        if (!classifier) classifier = FindAnyObjectByType<BiomeHabitatClassifier>();

        _settingsByAnimal.Clear();
        if (animalPrefabs != null)
        {
            for (int i = 0; i < animalPrefabs.Length; i++)
            {
                var e = animalPrefabs[i];
                if (e.animal == HabitatAnimal.None || e.prefab == null)
                    continue;
                _settingsByAnimal[e.animal] = new AnimalSpawnSettings
                {
                    Prefab = e.prefab,
                    YRotationDegrees = e.yRotationOffsetDegrees,
                    HeightOffset = e.heightOffset
                };
            }
        }

        EnsureSpawnParent();
    }

    private void OnEnable()
    {
        if (_activeInstance != null && _activeInstance != this)
        {
            enabled = false;
            return;
        }

        _activeInstance = this;
        TileEvents.HabitatAssigned += OnHabitatAssigned;
    }

    private void OnDisable()
    {
        TileEvents.HabitatAssigned -= OnHabitatAssigned;
        if (_activeInstance == this)
            _activeInstance = null;
    }

    public static void ResetForNewSession()
    {
        if (!Application.isPlaying)
        {
            s_spawnedByHabitatId.Clear();
            return;
        }

        var toDestroy = new List<GameObject>(s_spawnedByHabitatId.Count);
        foreach (var kv in s_spawnedByHabitatId)
        {
            if (kv.Value != null)
                toDestroy.Add(kv.Value);
        }

        for (int i = 0; i < toDestroy.Count; i++)
            Destroy(toDestroy[i]);

        s_spawnedByHabitatId.Clear();

        if (_activeInstance != null)
            _activeInstance._resolvedSpawnParent = null;
    }

    private void OnHabitatAssigned(HabitatAssignmentData data)
    {
        if (!isActiveAndEnabled || runtimeStore == null)
            return;

        if (data.Animal == HabitatAnimal.None || data.Tiles == null || data.Tiles.Count == 0)
            return;

        PruneDestroyedSpawns();

        if (s_spawnedByHabitatId.ContainsKey(data.HabitatId))
            return;

        if (!TryResolveSpawnCoreTile(data, out var coreTile))
            return;

        if (!_settingsByAnimal.TryGetValue(data.Animal, out var settings) || settings.Prefab == null)
        {
            Debug.LogWarning(
                $"[HabitatAnimalPlacement] Brak prefabu zwierzęcia dla {data.Animal} — przypisz w Inspectorze.",
                this);
            return;
        }

        Vector3 motionAnchor = coreTile.worldPos + BuildPositionOffset(settings.HeightOffset);
        Vector3 spawnPos = motionAnchor;
        Quaternion spawnRot = data.Animal == HabitatAnimal.Bees
            ? GetGridAlignedRotation()
            : ComputeSpawnRotation(coreTile, data.Tiles, settings.YRotationDegrees);

        EnsureSpawnParent();
        if (_resolvedSpawnParent == null)
            return;

        GameObject instance = Instantiate(settings.Prefab, spawnPos, spawnRot);
        if (instance == null)
            return;

        instance.name = $"{data.Animal}_Habitat{data.HabitatId}";

        HabitatAnimalAnimationSetup.Attach(
            instance,
            data.Animal,
            motionAnchor,
            GetGridAlignedRotation());

        instance.transform.SetParent(_resolvedSpawnParent, true);

        s_spawnedByHabitatId[data.HabitatId] = instance;
        PlaySpawnVfx(spawnPos);
    }

    private static void PruneDestroyedSpawns()
    {
        if (s_spawnedByHabitatId.Count == 0)
            return;

        s_pruneIds.Clear();
        foreach (var kv in s_spawnedByHabitatId)
        {
            if (kv.Value == null)
                s_pruneIds.Add(kv.Key);
        }

        for (int i = 0; i < s_pruneIds.Count; i++)
            s_spawnedByHabitatId.Remove(s_pruneIds[i]);
    }

    private static readonly List<int> s_pruneIds = new();

    private Vector3 BuildPositionOffset(float animalHeightOffset)
    {
        var offset = positionOffset;
        offset.y += animalHeightOffset;
        return offset;
    }

    private bool TryResolveSpawnCoreTile(HabitatAssignmentData data, out Tile coreTile)
    {
        coreTile = null;

        if (TryAcceptCoreTile(data.PrimaryCoreTile, out coreTile))
            return true;

        var rules = rulesProfile != null ? rulesProfile : classifier != null ? classifier.RulesProfile : null;
        if (HabitatCoreValidation.TryGetPrimaryCoreTile(
                data.Tiles, data.Animal, rules, _coreScratch, out coreTile, runtimeStore)
            && TryAcceptCoreTile(coreTile, out coreTile))
            return true;

        Debug.LogWarning(
            $"[HabitatAnimalPlacement] Brak suchego kafelka rdzeniowego dla {data.Animal} (habitat {data.HabitatId}).",
            this);
        return false;
    }

    private bool TryAcceptCoreTile(Tile candidate, out Tile accepted)
    {
        accepted = null;
        if (candidate == null || runtimeStore == null)
            return false;

        if (HabitatCoreValidation.IsWaterTile(runtimeStore, candidate))
            return false;

        var rt = runtimeStore.Get(candidate);
        if (rt == null || !rt.occupied)
            return false;

        accepted = candidate;
        return true;
    }

    private Quaternion ComputeSpawnRotation(Tile coreTile, IReadOnlyList<Tile> habitatTiles, float prefabYOffset)
    {
        if (coreTile == null)
            return Quaternion.identity;

        Quaternion rot;

        if (faceOutwardFromHabitatCenter && habitatTiles != null && habitatTiles.Count > 0)
        {
            Vector3 centroid = Vector3.zero;
            int count = 0;
            for (int i = 0; i < habitatTiles.Count; i++)
            {
                var t = habitatTiles[i];
                if (t == null) continue;
                centroid += t.worldPos;
                count++;
            }

            if (count > 0)
            {
                centroid /= count;
                Vector3 flat = coreTile.worldPos - centroid;
                flat.y = 0f;
                rot = flat.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(flat.normalized, Vector3.up)
                    : GetGridAlignedRotation();
            }
            else
            {
                rot = GetGridAlignedRotation();
            }
        }
        else
        {
            rot = GetGridAlignedRotation();
        }

        float totalY = prefabYOffset + yRotationOffsetDegrees;
        if (Mathf.Abs(totalY) > 0.001f)
            rot = rot * Quaternion.Euler(0f, totalY, 0f);

        return rot;
    }

    private Quaternion GetGridAlignedRotation()
        => alignToGridRotation && tileGrid != null ? tileGrid.transform.rotation : Quaternion.identity;

    private void PlaySpawnVfx(Vector3 spawnPos)
    {
        if (habitatSpawnVfxPrefab == null || _resolvedSpawnParent == null)
            return;

        Vector3 vfxPos = spawnPos + vfxPositionOffset;
        GameObject vfx = Instantiate(habitatSpawnVfxPrefab, vfxPos, Quaternion.identity);
        if (vfx == null)
            return;

        vfx.transform.SetParent(_resolvedSpawnParent, true);

        ScheduleVfxDestroy(vfx, vfxLifetimeFallback);
    }

    private static void ScheduleVfxDestroy(GameObject vfxRoot, float lifetimeFallback)
    {
        if (vfxRoot == null)
            return;

        float lifetime = 0f;
        var systems = vfxRoot.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] == null)
                continue;
            var main = systems[i].main;
            lifetime = Mathf.Max(lifetime, main.duration + main.startLifetime.constantMax);
        }

        if (lifetime <= 0f)
            lifetime = lifetimeFallback;

        Destroy(vfxRoot, lifetime);
    }

    private void EnsureSpawnParent()
    {
        if (_resolvedSpawnParent != null)
            return;

        if (spawnParent == null)
        {
            Debug.LogWarning(
                "[HabitatAnimalPlacement] Brak spawnParent — przypisz _occupants (skala 1), inaczej zwierzęta mogą być ogromne.",
                this);
            _resolvedSpawnParent = new GameObject("HabitatAnimalInstances").transform;
            return;
        }

        var existing = spawnParent.Find("HabitatAnimalInstances");
        if (existing != null)
        {
            _resolvedSpawnParent = existing;
            return;
        }

        var container = new GameObject("HabitatAnimalInstances");
        container.transform.SetParent(spawnParent, false);
        _resolvedSpawnParent = container.transform;
    }
}

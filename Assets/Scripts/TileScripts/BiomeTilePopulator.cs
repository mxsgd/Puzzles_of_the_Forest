using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wypełnia 12 slotów (trójkątów) świeżo postawionego kafla obiektami zgodnie z biomem.
/// Konfiguracja odbywa się w Inspectorze: dla każdego biomu lista par (tag → prefaby + density).
/// Brakujące tagi (np. Flower, Grass) są pomijane — slot zostaje pusty z tagiem null.
/// </summary>
public class BiomeTilePopulator : MonoBehaviour
{
    [Serializable]
    public class PrefabEntry
    {
        [Tooltip("Prefab do spawnu dla tego wpisu.")]
        public GameObject prefab;

        [Min(1), Tooltip("Ile trójkątów zajmuje to wystąpienie prefabu. Ustaw 3 dla dużych assetów.")]
        public int slotsPerSpawn = 1;

        [Min(0), Tooltip("Ile razy ten konkretny prefab ma pojawić się na jednym kaflu.")]
        public int spawnCountPerTile = 0;
    }

    [Serializable]
    public class ContentPrefab
    {
        [Tooltip("Tag treści: Tree, Bush, Flower, Grass, Rock — patrz TileContentTags.")]
        public string tag;

        [Tooltip("Pula prefabów dla danego tagu. Każdy wpis ma własny rozmiar zajętości i liczność per kafel.")]
        public List<PrefabEntry> prefabs = new();

        [Tooltip("Losowy zakres skali (przemnaża skalę prefabu).")]
        public Vector2 randomScaleRange = new Vector2(0.85f, 1.1f);

        [Range(0f, 360f)] public float maxRandomYRotation = 360f;

        [Min(0f), Tooltip("Maksymalne losowe przesunięcie w kierunku środka kafla (w jednostkach world).")]
        public float jitterTowardTileCenter = 0.05f;

        [Tooltip("Jeśli włączone, obiekt jest ustawiany od środka kafla zamiast od centroidu trójkąta slotu.")]
        public bool placeAtTileCenter = false;
    }

    [Serializable]
    public class BiomeContent
    {
        public TileBiome biome = TileBiome.None;
        public List<ContentPrefab> contents = new();
    }

    [Header("Konfiguracja per-biom")]
    [SerializeField] private List<BiomeContent> biomeContents = new();

    [Header("Hierarchia")]
    [SerializeField, Tooltip("Opcjonalny rodzic dla zinstancjonowanych dekoracji. Jeśli null — rodzicem jest sam kafel.")]
    private Transform decorationsParent;

    private readonly Dictionary<TileBiome, BiomeContent> _byBiome = new();

    private void Awake()    => RebuildIndex();
    private void OnValidate() => RebuildIndex();

    private void RebuildIndex()
    {
        _byBiome.Clear();
        if (biomeContents == null) return;
        foreach (var bc in biomeContents)
            if (bc != null) _byBiome[bc.biome] = bc;
    }

    /// <summary>
    /// Wypełnia kafel (każdy z 12 trójkątów dostaje szansę na obiekt zgodnie z biomem).
    /// Reguły dodatkowe:
    ///   - sąsiednie sloty (w cyklu 12 trójkątów) nie mogą być oba zajęte — obiekt musi być
    ///     przynajmniej jeden trójkąt dalej, żeby nie spawnowały się sklejone w klastry,
    ///   - na kaflu spawnuje się zawsze co najmniej jeden obiekt (jeśli biom ma jakąkolwiek
    ///     dostępną treść z prefabami).
    /// Można podać deterministyczny seed (np. hash z (tile.q, tile.r) dla powtarzalności po zapisie).
    /// </summary>
    /// <param name="decorationParentOverride">
    /// Gdy nie null — rodzic instancji dekoracji (np. podgląd ducha musi być pod prefabem, nie pod globalnym <see cref="decorationsParent"/>).
    /// </param>
    public void Populate(TileBiomeRuntime tile, int? randomSeed = null, Transform decorationParentOverride = null)
    {
        if (tile == null) return;
        if (_byBiome.Count == 0) RebuildIndex();

        if (!_byBiome.TryGetValue(tile.Biome, out var content) || content == null)
        {
            Debug.LogWarning($"[BiomeTilePopulator] Brak konfiguracji dla biomu {tile.Biome} — sloty zostają puste.", this);
            return;
        }

        var allowed = TileBiomeRules.GetAllowedTags(tile.Biome);
        if (allowed == null || allowed.Count == 0) return;

        var rng = randomSeed.HasValue ? new System.Random(randomSeed.Value) : new System.Random();
        var parent = decorationParentOverride != null
            ? decorationParentOverride
            : (decorationsParent != null ? decorationsParent : tile.transform);

        var slotById = BuildSlotMap(tile.Slots);
        if (slotById.Count == 0) return;

        var plan = BuildPlacementPlan(content, allowed);
        Shuffle(plan, rng);

        for (int i = 0; i < plan.Count; i++)
            TryPlacePlanEntry(tile, slotById, plan[i], parent, rng);

        FillRemainingSlots(tile, slotById, content, allowed, parent, rng);
    }

    private void FillRemainingSlots(
        TileBiomeRuntime tile,
        Dictionary<string, TileTriangleSlot> slotById,
        BiomeContent content,
        IReadOnlyList<string> allowed,
        Transform parent,
        System.Random rng)
    {
        int guard = 0;
        while (TryFindEmptySlot(slotById, out var emptySlot) && guard++ < 256)
        {
            var fallback = PickRandomSingleSlotDefinition(content, allowed, rng);
            if (!fallback.HasValue)
                break;
            var fallbackValue = fallback.Value;

            if (!TrySpawnInSingleSlot(tile, emptySlot, fallbackValue.Tag, fallbackValue.Definition, fallbackValue.PrefabEntry, parent, rng))
                break;
        }
    }

    private static Dictionary<string, TileTriangleSlot> BuildSlotMap(IReadOnlyList<TileTriangleSlot> slots)
    {
        var map = new Dictionary<string, TileTriangleSlot>();
        if (slots == null) return map;
        foreach (var s in slots)
            if (s != null) map[s.Id] = s;
        return map;
    }

    private static bool TryFindEmptySlot(Dictionary<string, TileTriangleSlot> slotById, out TileTriangleSlot slot)
    {
        foreach (var kv in slotById)
        {
            if (kv.Value != null && kv.Value.IsEmpty)
            {
                slot = kv.Value;
                return true;
            }
        }
        slot = null;
        return false;
    }

    private List<PlacementRequest> BuildPlacementPlan(BiomeContent content, IReadOnlyList<string> allowed)
    {
        var result = new List<PlacementRequest>();
        if (allowed == null) return result;
        for (int i = 0; i < allowed.Count; i++)
        {
            var tag = allowed[i];
            var def = FindContent(content, tag);
            if (def == null || def.prefabs == null || def.prefabs.Count == 0) continue;

            for (int p = 0; p < def.prefabs.Count; p++)
            {
                var prefabEntry = def.prefabs[p];
                if (prefabEntry == null || prefabEntry.prefab == null) continue;

                int count = Mathf.Max(0, prefabEntry.spawnCountPerTile);
                int slotsPerSpawn = Mathf.Max(1, prefabEntry.slotsPerSpawn);
                for (int c = 0; c < count; c++)
                    result.Add(new PlacementRequest(tag, def, prefabEntry, slotsPerSpawn));
            }
        }
        return result;
    }

    private bool TryPlacePlanEntry(
        TileBiomeRuntime tile,
        Dictionary<string, TileTriangleSlot> slotById,
        PlacementRequest request,
        Transform parent,
        System.Random rng)
    {
        if (request.SlotsPerSpawn <= 1)
        {
            var candidates = CollectEmptySlots(slotById);
            Shuffle(candidates, rng);
            for (int i = 0; i < candidates.Count; i++)
                if (TrySpawnInSingleSlot(tile, candidates[i], request.Tag, request.Definition, request.PrefabEntry, parent, rng))
                    return true;
            return false;
        }

        var anchors = CollectEmptySlots(slotById);
        Shuffle(anchors, rng);
        for (int i = 0; i < anchors.Count; i++)
        {
            var anchor = anchors[i];
            if (!TryGetTriple(anchor, slotById, out var left, out var right))
                continue;
            if (!left.IsEmpty || !right.IsEmpty || !anchor.IsEmpty)
                continue;

            return TrySpawnInTriple(tile, anchor, left, right, request.Tag, request.Definition, request.PrefabEntry, parent, rng);
        }
        return false;
    }

    private static List<TileTriangleSlot> CollectEmptySlots(Dictionary<string, TileTriangleSlot> slotById)
    {
        var result = new List<TileTriangleSlot>();
        foreach (var kv in slotById)
            if (kv.Value != null && kv.Value.IsEmpty) result.Add(kv.Value);
        return result;
    }

    private static bool TryGetTriple(
        TileTriangleSlot anchor,
        Dictionary<string, TileTriangleSlot> slotById,
        out TileTriangleSlot left,
        out TileTriangleSlot right)
    {
        left = null;
        right = null;
        if (anchor == null) return false;

        var neighbors = HexTileLayout.GetTriangleNeighbors(anchor.sideEdge, anchor.sideHypo);
        if (neighbors == null || neighbors.Length < 2) return false;

        string idA = HexTileLayout.TriangleId(neighbors[0].sideEdge, neighbors[0].sideHypo);
        string idB = HexTileLayout.TriangleId(neighbors[1].sideEdge, neighbors[1].sideHypo);
        return slotById.TryGetValue(idA, out left) && slotById.TryGetValue(idB, out right);
    }

    private bool TrySpawnInSingleSlot(
        TileBiomeRuntime tile,
        TileTriangleSlot slot,
        string chosenTag,
        ContentPrefab def,
        PrefabEntry forcedPrefabEntry,
        Transform parent,
        System.Random rng)
    {
        var prefabEntry = forcedPrefabEntry;
        if (prefabEntry == null)
        {
            if (def.prefabs == null || def.prefabs.Count == 0) return false;
            prefabEntry = def.prefabs[rng.Next(def.prefabs.Count)];
        }
        var prefab = prefabEntry?.prefab;
        if (prefab == null) return false;

        var basePos = def.placeAtTileCenter
            ? tile.transform.position
            : tile.GetWorldPosition(slot);
        var jitter = ComputeTowardCenterJitter(basePos, tile.transform.position, def.jitterTowardTileCenter, rng);

        float yRot = (float)rng.NextDouble() * def.maxRandomYRotation;
        float scl = Mathf.Lerp(
            def.randomScaleRange.x,
            def.randomScaleRange.y,
            (float)rng.NextDouble());

        var go = Instantiate(prefab, basePos + jitter, Quaternion.Euler(0f, yRot, 0f), parent);
        go.transform.localScale = prefab.transform.localScale * scl;
        go.name = $"{chosenTag}_{slot.Id}";


        slot.contentTag = chosenTag;
        slot.contentInstance = go;
        return true;
    }

    private bool TrySpawnInTriple(
        TileBiomeRuntime tile,
        TileTriangleSlot anchor,
        TileTriangleSlot neighborA,
        TileTriangleSlot neighborB,
        string chosenTag,
        ContentPrefab def,
        PrefabEntry forcedPrefabEntry,
        Transform parent,
        System.Random rng)
    {
        var prefabEntry = forcedPrefabEntry;
        if (prefabEntry == null)
        {
            if (def.prefabs == null || def.prefabs.Count == 0) return false;
            prefabEntry = def.prefabs[rng.Next(def.prefabs.Count)];
        }
        var prefab = prefabEntry?.prefab;
        if (prefab == null) return false;

        Vector3 a = tile.GetWorldPosition(anchor);
        Vector3 b = tile.GetWorldPosition(neighborA);
        Vector3 c = tile.GetWorldPosition(neighborB);
        var basePos = (a + b + c) / 3f;

        var jitter = ComputeTowardCenterJitter(basePos, tile.transform.position, def.jitterTowardTileCenter, rng);

        float yRot = (float)rng.NextDouble() * def.maxRandomYRotation;
        float scl = Mathf.Lerp(def.randomScaleRange.x, def.randomScaleRange.y, (float)rng.NextDouble());

        var go = Instantiate(prefab, basePos + jitter, Quaternion.Euler(0f, yRot, 0f), parent);
        go.transform.localScale = prefab.transform.localScale * scl;
        go.name = $"{chosenTag}_{anchor.Id}_{neighborA.Id}_{neighborB.Id}";


        anchor.contentTag = chosenTag;
        anchor.contentInstance = go;
        neighborA.contentTag = chosenTag;
        neighborB.contentTag = chosenTag;
        return true;
    }


    private static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static Vector3 ComputeTowardCenterJitter(Vector3 fromWorldPos, Vector3 tileCenterWorldPos, float maxDistance, System.Random rng)
    {
        if (maxDistance <= 0f) return Vector3.zero;

        var toCenter = tileCenterWorldPos - fromWorldPos;
        toCenter.y = 0f;
        if (toCenter.sqrMagnitude < 0.000001f) return Vector3.zero;

        float distance = (float)rng.NextDouble() * maxDistance;
        return toCenter.normalized * distance;
    }

    public void Clear(TileBiomeRuntime tile)
    {
        if (tile == null) return;
        tile.ClearAll();
    }

    private static ContentPrefab FindContent(BiomeContent bc, string tag)
    {
        if (bc?.contents == null) return null;
        foreach (var c in bc.contents)
            if (c != null && c.tag == tag) return c;
        return null;
    }

    private (string Tag, ContentPrefab Definition, PrefabEntry PrefabEntry)? PickRandomSingleSlotDefinition(
        BiomeContent content,
        IReadOnlyList<string> allowed,
        System.Random rng)
    {
        var options = new List<(string Tag, ContentPrefab Definition, PrefabEntry PrefabEntry)>();
        if (allowed == null) return null;

        for (int i = 0; i < allowed.Count; i++)
        {
            var tag = allowed[i];
            var def = FindContent(content, tag);
            if (def == null || def.prefabs == null || def.prefabs.Count == 0) continue;
            if (def.placeAtTileCenter) continue;
            for (int p = 0; p < def.prefabs.Count; p++)
            {
                var prefabEntry = def.prefabs[p];
                if (prefabEntry == null || prefabEntry.prefab == null) continue;
                if (Mathf.Max(1, prefabEntry.slotsPerSpawn) != 1) continue;
                options.Add((tag, def, prefabEntry));
            }
        }

        if (options.Count == 0) return null;
        return options[rng.Next(options.Count)];
    }

    private readonly struct PlacementRequest
    {
        public readonly string Tag;
        public readonly ContentPrefab Definition;
        public readonly PrefabEntry PrefabEntry;
        public readonly int SlotsPerSpawn;

        public PlacementRequest(string tag, ContentPrefab definition, PrefabEntry prefabEntry, int slotsPerSpawn)
        {
            Tag = tag;
            Definition = definition;
            PrefabEntry = prefabEntry;
            SlotsPerSpawn = Mathf.Max(1, slotsPerSpawn);
        }
    }
}

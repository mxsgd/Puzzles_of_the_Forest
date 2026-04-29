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
    public class ContentPrefab
    {
        [Tooltip("Tag treści: Tree, Bush, Flower, Grass, Rock — patrz TileContentTags.")]
        public string tag;

        [Tooltip("Pula prefabów. Wybierany losowo. Jeśli pusta — slot pominięty.")]
        public List<GameObject> prefabs = new();

        [Range(0f, 1f), Tooltip("Szansa na faktyczne wstawienie obiektu w slocie (gęstość).")]
        public float spawnChance = 0.5f;

        [Tooltip("Losowy zakres skali (przemnaża skalę prefabu).")]
        public Vector2 randomScaleRange = new Vector2(0.85f, 1.1f);

        [Range(0f, 360f)] public float maxRandomYRotation = 360f;

        [Tooltip("Losowe przesunięcie pozycji wewnątrz slotu (XYZ, w jednostkach world).")]
        public Vector3 positionJitter = new Vector3(0.05f, 0f, 0.05f);

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
    public void Populate(TileBiomeRuntime tile, int? randomSeed = null)
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
        var parent = decorationsParent != null ? decorationsParent : tile.transform;

        var candidates = new List<TileTriangleSlot>();
        foreach (var s in tile.Slots)
            if (s != null && s.IsEmpty) candidates.Add(s);
        Shuffle(candidates, rng);

        var filledIds = new HashSet<string>();
        int spawnedCount = 0;

        foreach (var slot in candidates)
        {
            if (HasAdjacentFilledSlot(slot, filledIds)) continue;

            string chosenTag = allowed[rng.Next(allowed.Count)];
            var def = FindContent(content, chosenTag);
            if (def == null || def.prefabs == null || def.prefabs.Count == 0) continue;
            if (rng.NextDouble() > def.spawnChance) continue;

            if (TrySpawnInSlot(tile, slot, chosenTag, def, parent, rng))
            {
                filledIds.Add(slot.Id);
                spawnedCount++;
            }
        }

        if (spawnedCount == 0)
            ForceSpawnAtLeastOne(tile, candidates, content, allowed, parent, rng);
    }

    /// <summary>Gwarantuje co najmniej jeden obiekt na kaflu — szuka pierwszego pasującego slotu i taga z prefabami.</summary>
    private void ForceSpawnAtLeastOne(
        TileBiomeRuntime tile,
        List<TileTriangleSlot> candidates,
        BiomeContent content,
        IReadOnlyList<string> allowed,
        Transform parent,
        System.Random rng)
    {
        var tagOrder = new List<string>(allowed);
        Shuffle(tagOrder, rng);

        foreach (var slot in candidates)
        {
            if (slot == null || !slot.IsEmpty) continue;

            foreach (var tag in tagOrder)
            {
                var def = FindContent(content, tag);
                if (def == null || def.prefabs == null || def.prefabs.Count == 0) continue;
                if (TrySpawnInSlot(tile, slot, tag, def, parent, rng))
                    return;
            }
        }
    }

    private bool TrySpawnInSlot(
        TileBiomeRuntime tile,
        TileTriangleSlot slot,
        string chosenTag,
        ContentPrefab def,
        Transform parent,
        System.Random rng)
    {
        var prefab = def.prefabs[rng.Next(def.prefabs.Count)];
        if (prefab == null) return false;

        var basePos = def.placeAtTileCenter
            ? tile.transform.position
            : tile.GetWorldPosition(slot);
        var jitter = new Vector3(
            ((float)rng.NextDouble() - 0.5f) * 2f * def.positionJitter.x,
            ((float)rng.NextDouble() - 0.5f) * 2f * def.positionJitter.y,
            ((float)rng.NextDouble() - 0.5f) * 2f * def.positionJitter.z);

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

    private static bool HasAdjacentFilledSlot(TileTriangleSlot slot, HashSet<string> filledIds)
    {
        if (filledIds.Count == 0) return false;
        foreach (var (e, h) in HexTileLayout.GetTriangleNeighbors(slot.sideEdge, slot.sideHypo))
            if (filledIds.Contains(HexTileLayout.TriangleId(e, h))) return true;
        return false;
    }

    private static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
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
}

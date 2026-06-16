using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Tile = TileGrid.Tile;

public class TilePlacementService : MonoBehaviour
{
    [Serializable]
    private struct BiomeParticleEntry
    {
        public TileBiome biome;
        public ParticleSystem prefab;
    }

    [SerializeField] private TileRuntimeStore runtimeStore;
    [SerializeField] private TileDeck deck;
    [SerializeField] private BiomeTilePopulator biomePopulator;
    [SerializeField] private Transform occupantsParent;
    [SerializeField] private Transform availabilityParent;
    [SerializeField] private GameObject availabilityPrefab;
    [SerializeField] private GameObject availabilitySelectedTilePrefab;
    [SerializeField] private GameObject occupantPrefab;
    [SerializeField] private Vector3 occupantOffset = new(0, 1f, 0);
    [SerializeField] private Vector3 availabilityOffset = new(0, 1f, 0);
    [Header("Availability Pool")]
    [SerializeField, Min(1)] private int maxPooledPerPrefab = 24;
    [Header("Placement Feedback - Particles")]
    [SerializeField] private BiomeParticleEntry[] biomeParticles = Array.Empty<BiomeParticleEntry>();
    [SerializeField] private Vector3 particleOffset = new(0f, 0.2f, 0f);
    [SerializeField, Min(1)] private int maxConcurrentPlacementParticles = 4;
    [SerializeField, Min(0)] private int maxPooledPlacementParticles = 6;

    private readonly Dictionary<TileBiome, ParticleSystem> _particlesByBiome = new();
    private readonly Dictionary<TileBiome, Stack<ParticleSystem>> _placementParticlePool = new();
    private readonly Dictionary<int, Stack<GameObject>> _availabilityPoolByPrefabId = new();
    private readonly Dictionary<GameObject, int> _availabilityInstancePrefabId = new();
    private Transform _particleParent;
    private int _activePlacementParticles;

    private void Awake()
    {
        RebuildParticleLookup();
        EnsureParticleParent();
    }

    /// <summary>Obcina nieaktywne instancje availability ponad limit per prefab.</summary>
    public void TrimAvailabilityPools(int maxPerPrefab)
    {
        maxPerPrefab = Mathf.Max(1, maxPerPrefab);
        foreach (var kv in _availabilityPoolByPrefabId)
        {
            var stack = kv.Value;
            while (stack.Count > maxPerPrefab)
            {
                var go = stack.Pop();
                _availabilityInstancePrefabId.Remove(go);
                Destroy(go);
            }
        }
    }

    /// <summary>Usuwa nadmiarowe partikle z puli (nie te aktualnie grające).</summary>
    public void TrimPlacementParticlePool()
    {
        foreach (var kv in _placementParticlePool)
        {
            var stack = kv.Value;
            while (stack.Count > maxPooledPlacementParticles)
            {
                var ps = stack.Pop();
                if (ps != null)
                    Destroy(ps.gameObject);
            }
        }
    }

    public GameObject PlaceOccupant(Tile tile, Quaternion rotation)
        => PlaceOccupant(tile, rotation, null);

    /// <summary>
    /// Promuje gotowego ghosta na postawiony kafel — zero Instantiate, zero Populate.
    /// Resetuje stan ghost-specyficzny (skala, sortingOrder, kolaidery) i reparentuje pod occupantsParent.
    /// Fallback na PlaceOccupant jeśli prebuiltInstance == null.
    /// </summary>
    public GameObject PlaceOccupantFromPrebuilt(
        Tile tile, Quaternion rotation, TileDraw tileDraw,
        GameObject prebuiltInstance, GameObject ghostRoot)
    {
        if (prebuiltInstance == null) return PlaceOccupant(tile, rotation, tileDraw);
        if (tile == null) return null;

        var r = runtimeStore.Get(tile);
        if (r.occupied) return null;

        RemoveAvailability(tile);

        prebuiltInstance.transform.SetParent(occupantsParent, worldPositionStays: false);
        prebuiltInstance.transform.position  = tile.worldPos + occupantOffset;
        prebuiltInstance.transform.rotation  = rotation;
        Vector3 prefabScale = tileDraw?.prefab != null
            ? tileDraw.prefab.transform.localScale
            : Vector3.one;
        prebuiltInstance.transform.localScale = prefabScale;

        if (ghostRoot != null) Destroy(ghostRoot);

        foreach (var col in prebuiltInstance.GetComponentsInChildren<Collider>(true))
            col.enabled = true;
        foreach (var rend in prebuiltInstance.GetComponentsInChildren<Renderer>(true))
            rend.sortingOrder = 0;

        var tileObj = prebuiltInstance.GetComponent<TileObject>()
                   ?? prebuiltInstance.AddComponent<TileObject>();
        if (tile.grid != null) tileObj.AssignTile(tile.grid, tile);

        var biomeRuntime = prebuiltInstance.GetComponentInChildren<TileBiomeRuntime>();
        var biome = tileDraw?.biome ?? TileBiome.None;

        runtimeStore.MarkOccupied(tile, prebuiltInstance, tileDraw?.prefab, tileDraw, biomeRuntime);
        SpawnBiomeParticles(tile, biome);
        TileEvents.RaiseTilePlaced(tile, biome);
        return prebuiltInstance;
    }

    public GameObject PlaceOccupant(Tile tile, Quaternion rotation, TileDraw tileDraw)
    {
        if (tile == null) return null;
        var r = runtimeStore.Get(tile);
        if (r.occupied) return null;

        var biome = tileDraw?.biome ?? TileBiome.None;
        var prefab = tileDraw?.prefab
            ?? (deck != null ? deck.GetPrefabFor(biome) : null)
            ?? occupantPrefab;
        if (prefab == null) return null;

        RemoveAvailability(tile);
        var pos = tile.worldPos + occupantOffset;
        var go = Instantiate(prefab, pos, rotation, occupantsParent);
        go.SetActive(true);
        go.transform.localScale = prefab.transform.localScale;

        var tileObj = go.GetComponent<TileObject>();
        if (tileObj == null)
            tileObj = go.AddComponent<TileObject>();
        if (tile.grid != null)
            tileObj.AssignTile(tile.grid, tile);

        TileBiomeRuntime biomeRuntime = null;
        if (biome != TileBiome.None)
        {
            biomeRuntime = go.GetComponent<TileBiomeRuntime>();
            if (biomeRuntime == null) biomeRuntime = go.AddComponent<TileBiomeRuntime>();

            float radius = tile.grid != null ? tile.grid.HexRadius : 1f;
            biomeRuntime.Initialize(biome, radius, tileDraw?.biomeVariantId);

            if (biomePopulator != null)
            {
                int seed = unchecked((tile.q * 73856093) ^ (tile.r * 19349663));
                biomePopulator.Populate(biomeRuntime, seed);
            }
        }

        runtimeStore.MarkOccupied(tile, go, prefab, tileDraw, biomeRuntime);
        SpawnBiomeParticles(tile, biome);
        TileEvents.RaiseTilePlaced(tile, biome);
        return go;
    }

    public GameObject PlaceAvailability(Tile tile, float alpha, string tag)
    {
        if (tile == null) return null;
        var r = runtimeStore.Get(tile);
        var tpl = availabilityPrefab != null ? availabilityPrefab : r.templatePrefab;
        if (tpl == null) return null;

        RemoveAvailability(tile);
        var go = AcquireAvailability(tile, tpl, alpha, tag);
        r.availabilityInstance = go;
        r.available = true;
        return go;
    }

    public GameObject PlaceAvailabilitySelected(Tile tile, float alpha, string tag)
    {
        if (tile == null) return null;
        var r = runtimeStore.Get(tile);
        var tpl = availabilitySelectedTilePrefab != null ? availabilitySelectedTilePrefab : r.templatePrefab;
        if (tpl == null) return null;

        RemoveAvailability(tile);
        var go = AcquireAvailability(tile, tpl, alpha, tag);
        r.availabilityInstance = go;
        r.available = true;
        return go;
    }

    public void RemoveAvailability(Tile tile)
    {
        var r = runtimeStore.Get(tile);
        if (r?.availabilityInstance == null) return;

        ReleaseAvailability(r.availabilityInstance);
        r.availabilityInstance = null;
        r.available = false;
    }

    public void RemoveOccupant(Tile tile)
    {
        var r = runtimeStore.Get(tile);
        if (r?.occupantInstance == null) return;
        Destroy(r.occupantInstance);
        r.occupantInstance = null;
        runtimeStore.Free(tile);
    }

    /// <summary>Usuwa wszystkie postawione kafle i ghosty dostępności (restart sesji).</summary>
    public void ClearBoard()
    {
        ClearChildren(occupantsParent);
        ClearChildren(availabilityParent);
        _availabilityPoolByPrefabId.Clear();
        _availabilityInstancePrefabId.Clear();
        ClearPlacementParticles();
        runtimeStore?.ClearAll();
    }

    private void ClearPlacementParticles()
    {
        _activePlacementParticles = 0;
        StopAllCoroutines();
        foreach (var kv in _placementParticlePool)
        {
            while (kv.Value.Count > 0)
            {
                var ps = kv.Value.Pop();
                if (ps != null)
                    Destroy(ps.gameObject);
            }
        }
        _placementParticlePool.Clear();
        if (_particleParent != null)
            ClearChildren(_particleParent);
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            if (Application.isPlaying)
                DestroyImmediate(child);
            else
                DestroyImmediate(child);
        }
    }

    // -------------------------------------------------------------------------
    // Availability pool
    // -------------------------------------------------------------------------

    private GameObject AcquireAvailability(Tile tile, GameObject prefab, float alpha, string tag)
    {
        int prefabId = prefab.GetInstanceID();
        GameObject instance = null;

        if (_availabilityPoolByPrefabId.TryGetValue(prefabId, out Stack<GameObject> stack) && stack.Count > 0)
        {
            while (stack.Count > 0 && instance == null)
                instance = stack.Pop();
        }

        if (instance == null)
        {
            instance = Instantiate(prefab, availabilityParent);
            _availabilityInstancePrefabId[instance] = prefabId;
        }

        instance.transform.SetParent(availabilityParent, false);
        instance.transform.position = tile.worldPos + availabilityOffset;
        instance.transform.rotation = Quaternion.identity;
        instance.SetActive(true);
        ConfigureAvailabilityInstance(instance, alpha, tag);
        return instance;
    }

    private void ReleaseAvailability(GameObject instance)
    {
        if (instance == null) return;

        if (!_availabilityInstancePrefabId.TryGetValue(instance, out int prefabId))
        {
            Destroy(instance);
            return;
        }

        instance.SetActive(false);
        instance.transform.SetParent(availabilityParent, false);

        if (!_availabilityPoolByPrefabId.TryGetValue(prefabId, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>();
            _availabilityPoolByPrefabId[prefabId] = stack;
        }

        if (stack.Count >= maxPooledPerPrefab)
        {
            _availabilityInstancePrefabId.Remove(instance);
            Destroy(instance);
            return;
        }

        stack.Push(instance);
    }

    // Lazy — MaterialPropertyBlock cannot be created in a static field initializer (Unity restriction).
    private static MaterialPropertyBlock _availabilityMpb;
    private static MaterialPropertyBlock GetMpb() => _availabilityMpb ??= new MaterialPropertyBlock();
    private static readonly int _colorPropId     = Shader.PropertyToID("_Color");
    private static readonly int _baseColorPropId = Shader.PropertyToID("_BaseColor");

    private static Color ReadMaterialTint(Material mat)
    {
        if (mat == null) return Color.white;
        if (mat.HasProperty(_baseColorPropId))
            return mat.GetColor(_baseColorPropId);
        if (mat.HasProperty(_colorPropId))
            return mat.GetColor(_colorPropId);
        return Color.white;
    }

    private static void ConfigureAvailabilityInstance(GameObject instance, float alpha, string tag)
    {
        if (instance == null) return;
        if (!instance.name.Contains("(Available)"))
            instance.name = instance.name.Replace("(Clone)", "").Trim() + " (Available)";
        if (!string.IsNullOrEmpty(tag)) instance.tag = tag;

        var mpb = GetMpb();
        foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
        {
            var mat = r.sharedMaterial;
            r.GetPropertyBlock(mpb);
            var baseColor = ReadMaterialTint(mat);
            baseColor.a = alpha;
            if (mat != null && mat.HasProperty(_colorPropId))
                mpb.SetColor(_colorPropId, baseColor);
            if (mat != null && mat.HasProperty(_baseColorPropId))
                mpb.SetColor(_baseColorPropId, baseColor);
            r.SetPropertyBlock(mpb);
        }
        foreach (var col in instance.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
    }

    private void RebuildParticleLookup()
    {
        _particlesByBiome.Clear();
        if (biomeParticles == null)
            return;

        for (int i = 0; i < biomeParticles.Length; i++)
        {
            var entry = biomeParticles[i];
            if (entry.biome == TileBiome.None || entry.prefab == null)
                continue;
            _particlesByBiome[entry.biome] = entry.prefab;
        }
    }

    private void SpawnBiomeParticles(Tile tile, TileBiome biome)
    {
        if (tile == null || biome == TileBiome.None)
            return;
        if (!_particlesByBiome.TryGetValue(biome, out ParticleSystem prefab) || prefab == null)
            return;
        if (_activePlacementParticles >= maxConcurrentPlacementParticles)
            return;

        EnsureParticleParent();
        Vector3 spawnPos = tile.worldPos + particleOffset;
        var ps = AcquirePlacementParticle(biome, prefab, spawnPos);
        if (ps == null)
            return;

        ps.gameObject.SetActive(true);
        ps.Play(true);
        _activePlacementParticles++;

        float lifetime = ps.main.duration + ps.main.startLifetime.constantMax + 0.5f;
        StartCoroutine(ReleasePlacementParticleAfter(ps, biome, lifetime));
    }

    private void EnsureParticleParent()
    {
        if (_particleParent != null)
            return;
        var go = new GameObject("PlacementParticles");
        go.transform.SetParent(transform, false);
        _particleParent = go.transform;
    }

    private ParticleSystem AcquirePlacementParticle(TileBiome biome, ParticleSystem prefab, Vector3 position)
    {
        if (_placementParticlePool.TryGetValue(biome, out var stack))
        {
            while (stack.Count > 0)
            {
                var pooled = stack.Pop();
                if (pooled == null)
                    continue;
                pooled.transform.SetParent(_particleParent, false);
                pooled.transform.position = position;
                return pooled;
            }
        }

        var ps = Instantiate(prefab, position, Quaternion.identity, _particleParent);
        return ps;
    }

    private IEnumerator ReleasePlacementParticleAfter(ParticleSystem ps, TileBiome biome, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (ps == null)
        {
            _activePlacementParticles = Mathf.Max(0, _activePlacementParticles - 1);
            yield break;
        }

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.gameObject.SetActive(false);
        _activePlacementParticles = Mathf.Max(0, _activePlacementParticles - 1);

        if (!_placementParticlePool.TryGetValue(biome, out var stack))
        {
            stack = new Stack<ParticleSystem>();
            _placementParticlePool[biome] = stack;
        }

        if (stack.Count >= maxPooledPlacementParticles)
            Destroy(ps.gameObject);
        else
            stack.Push(ps);
    }
}

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
    [Header("Placement Feedback - Particles")]
    [SerializeField] private BiomeParticleEntry[] biomeParticles = Array.Empty<BiomeParticleEntry>();
    [SerializeField] private Vector3 particleOffset = new(0f, 0.2f, 0f);

    private readonly System.Collections.Generic.Dictionary<TileBiome, ParticleSystem> _particlesByBiome
        = new System.Collections.Generic.Dictionary<TileBiome, ParticleSystem>();

    private void Awake()
    {
        RebuildParticleLookup();
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

        // Reparent instancję z ghost root do hierarchii postawionych kafli.
        prebuiltInstance.transform.SetParent(occupantsParent, worldPositionStays: false);
        prebuiltInstance.transform.position  = tile.worldPos + occupantOffset;
        prebuiltInstance.transform.rotation  = rotation;
        prebuiltInstance.transform.localScale = Vector3.one; // ghost miał 1.35 przez root — reset

        // Zniszcz wrapper ghost roota (jest już pusty po reparencie).
        if (ghostRoot != null) Destroy(ghostRoot);

        // Przywróć stan pomijany w ghost build (kolaidery były wyłączone, sorting był podwyższony).
        foreach (var col in prebuiltInstance.GetComponentsInChildren<Collider>(true))
            col.enabled = true;
        foreach (var rend in prebuiltInstance.GetComponentsInChildren<Renderer>(true))
            rend.sortingOrder = 0;

        // TileObject — wymagany do śledzenia kafla.
        var tileObj = prebuiltInstance.GetComponent<TileObject>()
                   ?? prebuiltInstance.AddComponent<TileObject>();
        if (tile.grid != null) tileObj.AssignTile(tile.grid, tile);

        // TileBiomeRuntime jest już gotowy z ghost build (Populate już wywołany).
        var biomeRuntime = prebuiltInstance.GetComponentInChildren<TileBiomeRuntime>();
        var biome = tileDraw?.biome ?? TileBiome.None;

        runtimeStore.MarkOccupied(tile, prebuiltInstance, tileDraw?.prefab, tileDraw, biomeRuntime);
        SpawnBiomeParticles(tile, biome);
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
            biomeRuntime.Initialize(biome, radius);

            if (biomePopulator != null)
            {
                int seed = unchecked((tile.q * 73856093) ^ (tile.r * 19349663));
                biomePopulator.Populate(biomeRuntime, seed);
            }
        }

        runtimeStore.MarkOccupied(tile, go, prefab, tileDraw, biomeRuntime);
        SpawnBiomeParticles(tile, biome);
        return go;
    }

    public GameObject PlaceAvailability(Tile tile, float alpha, string tag)
    {
        if (tile == null) return null;
        var r = runtimeStore.Get(tile);
        var tpl = availabilityPrefab != null ? availabilityPrefab : r.templatePrefab;
        if (tpl == null) return null;

        RemoveAvailability(tile);
        var pos = tile.worldPos + availabilityOffset;
        var go = Instantiate(tpl, pos, Quaternion.identity, availabilityParent);
        ConfigureAvailabilityInstance(go, alpha, tag);
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
        var pos = tile.worldPos + availabilityOffset;
        var go = Instantiate(tpl, pos, Quaternion.identity, availabilityParent);
        ConfigureAvailabilityInstance(go, alpha, tag);
        r.availabilityInstance = go;
        r.available = true;
        return go;
    }

    public void RemoveAvailability(Tile tile)
    {
        var r = runtimeStore.Get(tile);
        if (r?.availabilityInstance == null) return;
        Destroy(r.availabilityInstance);
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

    // Lazy — MaterialPropertyBlock cannot be created in a static field initializer (Unity restriction).
    private static MaterialPropertyBlock _availabilityMpb;
    private static MaterialPropertyBlock GetMpb() => _availabilityMpb ??= new MaterialPropertyBlock();
    private static readonly int _colorPropId     = Shader.PropertyToID("_Color");
    private static readonly int _baseColorPropId = Shader.PropertyToID("_BaseColor");

    private static void ConfigureAvailabilityInstance(GameObject instance, float alpha, string tag)
    {
        if (instance == null) return;
        instance.name = instance.name.Replace("(Clone)", "").Trim() + " (Available)";
        if (!string.IsNullOrEmpty(tag)) instance.tag = tag;

        var mpb = GetMpb();
        foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
        {
            // MaterialPropertyBlock: sets per-instance alpha without creating material instances.
            // Preserves GPU instancing and batching on the shared sharedMaterial.
            r.GetPropertyBlock(mpb);
            var baseColor = r.sharedMaterial != null ? r.sharedMaterial.color : Color.white;
            baseColor.a = alpha;
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(_colorPropId))
                mpb.SetColor(_colorPropId, baseColor);
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(_baseColorPropId))
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

        Vector3 spawnPos = tile.worldPos + particleOffset;
        var ps = Instantiate(prefab, spawnPos, Quaternion.identity);
        ps.Play(true);
        float lifetime = ps.main.duration + ps.main.startLifetime.constantMax + 0.5f;
        Destroy(ps.gameObject, lifetime);
    }
}
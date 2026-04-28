using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>Zwierze, do którego habitatu przypisano kaflę (po klasyfikacji biomu).</summary>
public enum HabitatAnimal
{
    None = 0,
    Deer,
    Beaver,
    Bear
}

public class TileRuntimeStore : MonoBehaviour
{
    public class Runtime
    {
        public bool occupied;
        public bool available;
        public GameObject occupantInstance;
        public GameObject availabilityInstance;
        public GameObject templatePrefab;
        public TileDraw tileDraw;
        public TileBiome biome = TileBiome.None;
        public TileBiomeRuntime biomeRuntime;
        public int habitatID = -1;
        public List<Tile> connectedTiles;
        /// <summary>Po przypisaniu regionu do habitatu zwierzęcia — które zwierzę go zajął.</summary>
        public HabitatAnimal assignedAnimal = HabitatAnimal.None;
    }

    private readonly Dictionary<Tile, Runtime> _map = new();
    private int _occupiedCount;
    private int _nextHabitatId = 1;

    public Runtime Get(Tile t)
    {
        if (t == null) return null;
        if (!_map.TryGetValue(t, out var r)) _map[t] = r = new Runtime();
        return r;
    }
    public int OccupiedCount => _occupiedCount;
    public void MarkOccupied(Tile t, GameObject inst, GameObject template = null, TileDraw tileDraw = null, TileBiomeRuntime biomeRuntime = null)
    {
        var r = Get(t);
        if (!r.occupied) _occupiedCount++;
        r.occupied = true;
        r.available = false;
        r.occupantInstance = inst;
        if (template) r.templatePrefab = template;
        if (tileDraw != null)
        {
            r.tileDraw = tileDraw;
            r.biome = tileDraw.biome;
            if (!template && tileDraw.prefab)
                r.templatePrefab = tileDraw.prefab;
        }
        if (biomeRuntime != null) r.biomeRuntime = biomeRuntime;
        TileEvents.RaiseTileStateChanged(t);
    }

    public void Free(Tile t)
    {
        var r = Get(t);
        if (r.occupied && _occupiedCount > 0) _occupiedCount--;
        r.occupied = false;
        r.available = true;
        r.tileDraw = null;
        r.occupantInstance = null;
        r.biomeRuntime = null;
        r.biome = TileBiome.None;
        r.assignedAnimal = HabitatAnimal.None;
        r.habitatID = -1;
        TileEvents.RaiseTileStateChanged(t);
    }

    /// <summary>Zwraca wszystkie zajęte kaflę wraz z runtime (do wykrywania regionów / klasyfikacji).</summary>
    public IEnumerable<(Tile tile, Runtime runtime)> GetOccupiedTilesWithRuntime()
    {
        foreach (var kvp in _map)
            if (kvp.Value.occupied)
                yield return (kvp.Key, kvp.Value);
    }

    /// <summary>Przypisuje ciąg kafli do habitatu danego zwierzęcia (kafle nie będą brane pod uwagę przy innych zwierzętach).</summary>
    public void AssignHabitatToTiles(IReadOnlyList<Tile> tiles, HabitatAnimal animal)
    {
        if (tiles == null || tiles.Count == 0) return;
        int id = _nextHabitatId++;
        foreach (var t in tiles)
        {
            var r = Get(t);
            r.assignedAnimal = animal;
            r.habitatID = id;
        }
        TileEvents.RaiseHabitatAssigned(animal, tiles);
    }
    
    public IEnumerable<Runtime> GetOccupiedTiles()
    {
        foreach (var kvp in _map)
            if (kvp.Value.occupied)
                yield return kvp.Value;
    }

    /// <summary>Zwraca grupy kafli przypisanych do habitatów (do rysowania obrysu).</summary>
    public List<(int habitatId, HabitatAnimal animal, List<Tile> tiles)> GetHabitatGroups()
    {
        var byId = new Dictionary<int, (HabitatAnimal animal, List<Tile> tiles)>();
        foreach (var (tile, r) in GetOccupiedTilesWithRuntime())
        {
            if (r.assignedAnimal == HabitatAnimal.None || r.habitatID < 0) continue;
            if (!byId.TryGetValue(r.habitatID, out var group))
                byId[r.habitatID] = (r.assignedAnimal, new List<Tile> { tile });
            else
                group.tiles.Add(tile);
        }
        var result = new List<(int, HabitatAnimal, List<Tile>)>();
        foreach (var kvp in byId)
            result.Add((kvp.Key, kvp.Value.animal, kvp.Value.tiles));
        return result;
    }
}
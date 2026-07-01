using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

public class TileRuntimeStore : MonoBehaviour
{
    public const int MaxHabitatsPerTile = 1;

    public class HabitatRecord
    {
        public int Id;
        public HabitatAnimal Animal;
        public Tile PrimaryCoreTile;
        public List<Tile> Tiles = new List<Tile>();
    }

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
        public int habitatId = -1;

        public bool CanAcceptNewHabitat() => habitatId < 0;
    }

    private readonly Dictionary<Tile, Runtime> _map = new();
    private readonly Dictionary<int, HabitatRecord> _habitats = new();

    /// <summary>habitatId → groupLeaderId (najniższe ID w grupie sąsiadujących same-animal habitatów).</summary>
    private readonly Dictionary<int, int> _habitatGroupLeader = new();

    private static readonly HabitatRegionScratch _coreScratch = new();
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
        if (r == null) return;

        if (r.habitatId >= 0)
            RemoveTileFromHabitatInternal(r.habitatId, t);

        if (r.occupied && _occupiedCount > 0) _occupiedCount--;
        r.occupied = false;
        r.available = true;
        r.tileDraw = null;
        r.occupantInstance = null;
        r.biomeRuntime = null;
        r.biome = TileBiome.None;
        r.habitatId = -1;
        TileEvents.RaiseTileStateChanged(t);
    }

    public bool TryGetHabitatAnimal(int habitatId, out HabitatAnimal animal)
    {
        if (_habitats.TryGetValue(habitatId, out var rec))
        {
            animal = rec.Animal;
            return true;
        }
        animal = HabitatAnimal.None;
        return false;
    }

    /// <summary>Światowa pozycja kotwicy habitatu (core tile) — fallback gdy brak spawnu zwierzęcia.</summary>
    public bool TryGetHabitatWorldAnchor(int habitatId, out Vector3 worldPos)
    {
        worldPos = default;
        if (!_habitats.TryGetValue(habitatId, out var rec))
            return false;

        var tile = rec.PrimaryCoreTile;
        if (tile == null)
        {
            for (int i = 0; i < rec.Tiles.Count; i++)
            {
                if (rec.Tiles[i] != null)
                {
                    tile = rec.Tiles[i];
                    break;
                }
            }
        }

        if (tile == null)
            return false;

        worldPos = tile.worldPos;
        return true;
    }

    /// <summary>
    /// Registers a new habitat if every tile has fewer than two habitats and all tiles are occupied.
    /// </summary>
    private bool _registeringHabitat;

    public bool TryRegisterHabitat(
        HabitatAnimal animal,
        IReadOnlyList<Tile> tiles,
        out int habitatId,
        HabitatRulesProfile rules = null)
    {
        habitatId = -1;
        if (_registeringHabitat) return false;   // re-entrancy guard
        if (animal == HabitatAnimal.None || tiles == null || tiles.Count == 0)
            return false;

        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            if (t == null) return false;
            var rt = Get(t);
            if (!rt.occupied || !rt.CanAcceptNewHabitat())
                return false;
        }

        Tile primaryCore = null;
        HabitatCoreValidation.TryGetPrimaryCoreTile(tiles, animal, rules, _coreScratch, out primaryCore, this);

        int id = _nextHabitatId++;
        var record = new HabitatRecord { Id = id, Animal = animal, PrimaryCoreTile = primaryCore };
        foreach (var t in tiles)
        {
            if (t == null) continue;
            record.Tiles.Add(t);
            Get(t).habitatId = id;
        }
        _habitats[id] = record;
        _habitatGroupLeader[id] = id;   // initially own leader

        _registeringHabitat = true;
        try
        {
            int points = HabitatRequirements.ComputeAwardedPoints(animal, tiles.Count);
            float scoreValue = HabitatRequirements.ComputeScore(animal, tiles.Count);

            TileEvents.RaiseHabitatAssigned(new HabitatAssignmentData(
                id, animal, tiles, points, scoreValue, tiles.Count, primaryCore));

            TryGroupAdjacentHabitats(id, rules);
        }
        finally
        {
            _registeringHabitat = false;
        }
        return true;
    }

    /// <summary>
    /// Grupuje nowy habitat z sąsiednimi habitatami tego samego zwierzęcia.
    /// Rekordy habitatów NIE są usuwane — każdy sub-habitat zachowuje swoje kafle i swoje zwierzę.
    /// Zmienia się tylko lider grupy wizualnej (najniższe ID w grupie).
    /// </summary>
    private bool TryGroupAdjacentHabitats(int seedHabitatId, HabitatRulesProfile rules)
    {
        if (!_habitats.TryGetValue(seedHabitatId, out var seedRecord))
            return false;

        // BFS po sąsiednich habitatach tego samego zwierzęcia
        var directNeighborIds = CollectTouchingSameAnimalHabitats(seedHabitatId, seedRecord.Animal);
        if (directNeighborIds.Count <= 1)
            return false;   // tylko sam seed — brak sąsiednich

        // Rozszerz o wszystkich aktualnych członków grup, do których należą sąsiedzi
        var allMemberIds = new HashSet<int>();
        foreach (int hid in directNeighborIds)
        {
            int leader = GetGroupLeader(hid);
            // Dodaj wszystkich z tej samej grupy
            foreach (var kv in _habitatGroupLeader)
                if (kv.Value == leader) allMemberIds.Add(kv.Key);
            allMemberIds.Add(hid);
        }

        // Nowy lider = najniższe ID w grupie
        int newLeader = seedHabitatId;
        foreach (int mid in allMemberIds)
            if (mid < newLeader) newLeader = mid;

        // Ustaw lidera dla wszystkich członków
        foreach (int mid in allMemberIds)
            _habitatGroupLeader[mid] = newLeader;

        // Lista ID nie będących liderem (dla GameUI – korekta licznika)
        var nonLeaderIds = new List<int>();
        var allGroupTiles = new List<Tile>();
        foreach (int mid in allMemberIds)
        {
            if (mid != newLeader) nonLeaderIds.Add(mid);
            if (_habitats.TryGetValue(mid, out var rec))
                foreach (var t in rec.Tiles)
                    if (t != null && !allGroupTiles.Contains(t)) allGroupTiles.Add(t);
        }

        HabitatCoreValidation.TryGetPrimaryCoreTile(
            allGroupTiles, seedRecord.Animal, rules, _coreScratch, out var primaryCore, this);

        int basePoints = HabitatRequirements.ComputeAwardedPoints(seedRecord.Animal, allGroupTiles.Count);
        int connectionBonus = HabitatRequirements.ComputeMergeConnectionBonus(
            seedRecord.Animal, allMemberIds.Count);

        TileEvents.RaiseHabitatMerged(new HabitatMergeData(
            newLeader,
            seedRecord.Animal,
            allGroupTiles,
            nonLeaderIds,
            allMemberIds.Count,
            basePoints,
            connectionBonus,
            primaryCore));

        Debug.Log(
            $"[HabitatGroup] {seedRecord.Animal}: zgrupowano {allMemberIds.Count} sub-habitatów " +
            $"({allGroupTiles.Count} kafli łącznie) → +{basePoints} pkt + {connectionBonus} bonus.");

        return true;
    }

    public int GetGroupLeader(int habitatId)
        => _habitatGroupLeader.TryGetValue(habitatId, out int l) ? l : habitatId;

    /// <summary>
    /// Zwraca listę grup wizualnych — jeden wpis per połączony region (łączy kafle sub-habitatów).
    /// Używane przez HabitatOutlineVisualizer zamiast GetHabitatGroups().
    /// </summary>
    public List<(int groupId, HabitatAnimal animal, List<Tile> tiles)> GetVisualHabitatGroups()
    {
        var groupData = new Dictionary<int, (HabitatAnimal animal, List<Tile> tiles)>();
        foreach (var kv in _habitats)
        {
            int leader = GetGroupLeader(kv.Key);
            if (!groupData.TryGetValue(leader, out var entry))
            {
                entry = (kv.Value.Animal, new List<Tile>());
                groupData[leader] = entry;
            }
            foreach (var t in kv.Value.Tiles)
                if (t != null && !entry.tiles.Contains(t))
                    entry.tiles.Add(t);
        }
        var result = new List<(int, HabitatAnimal, List<Tile>)>(groupData.Count);
        foreach (var kv in groupData)
            result.Add((kv.Key, kv.Value.animal, kv.Value.tiles));
        return result;
    }

    private HashSet<int> CollectTouchingSameAnimalHabitats(int seedHabitatId, HabitatAnimal animal)
    {
        var result = new HashSet<int>();
        var queue = new Queue<int>();
        result.Add(seedHabitatId);
        queue.Enqueue(seedHabitatId);

        while (queue.Count > 0)
        {
            int hid = queue.Dequeue();
            if (!_habitats.TryGetValue(hid, out var rec))
                continue;

            for (int i = 0; i < rec.Tiles.Count; i++)
            {
                var tile = rec.Tiles[i];
                if (tile == null)
                    continue;

                foreach (Tile neighbor in HabitatRegionEnumerator.GetNeighborsSafe(tile))
                {
                    if (neighbor == null)
                        continue;

                    var nr = Get(neighbor);
                    if (nr == null || !nr.occupied || nr.habitatId < 0 || nr.habitatId == hid)
                        continue;
                    if (!_habitats.TryGetValue(nr.habitatId, out var other) || other.Animal != animal)
                        continue;
                    if (result.Add(other.Id))
                        queue.Enqueue(other.Id);
                }
            }
        }

        return result;
    }

    private void RemoveTileFromHabitatInternal(int habitatId, Tile t)
    {
        if (!_habitats.TryGetValue(habitatId, out var rec))
            return;
        rec.Tiles.Remove(t);
        var rt = Get(t);
        if (rt != null && rt.habitatId == habitatId) rt.habitatId = -1;
        if (rec.Tiles.Count == 0)
            _habitats.Remove(habitatId);
    }

    public IEnumerable<(Tile tile, Runtime runtime)> GetOccupiedTilesWithRuntime()
    {
        foreach (var kvp in _map)
            if (kvp.Value.occupied)
                yield return (kvp.Key, kvp.Value);
    }

    public IEnumerable<Runtime> GetOccupiedTiles()
    {
        foreach (var kvp in _map)
            if (kvp.Value.occupied)
                yield return kvp.Value;
    }

    /// <summary>Groups for outlines / debug — one entry per habitat id.</summary>
    public List<(int habitatId, HabitatAnimal animal, List<Tile> tiles)> GetHabitatGroups()
    {
        var result = new List<(int, HabitatAnimal, List<Tile>)>();
        foreach (var kvp in _habitats)
        {
            var rec = kvp.Value;
            result.Add((rec.Id, rec.Animal, new List<Tile>(rec.Tiles)));
        }
        return result;
    }

    public IReadOnlyDictionary<int, HabitatRecord> GetAllHabitats() => _habitats;

    /// <summary>Czyści stan sesji (np. przed restartem). Instancje na scenie niszczy <see cref="TilePlacementService.ClearBoard"/>.</summary>
    public void ClearAll()
    {
        _map.Clear();
        _habitats.Clear();
        _habitatGroupLeader.Clear();
        _occupiedCount = 0;
        _nextHabitatId = 1;
        _registeringHabitat = false;
    }
}

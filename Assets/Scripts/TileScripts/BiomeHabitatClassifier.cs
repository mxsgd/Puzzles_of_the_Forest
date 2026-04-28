using System;
using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Klasyfikuje spójne regiony zajętych kafli do habitatów zwierząt.
/// Po każdym położeniu kafla sprawdza sąsiadujące kafle; jeśli region spełnia wymagania
/// dokładnie jednego zwierzęcia, przypisuje go do tego habitatu (kafle nie są już dostępne dla innych).
/// </summary>
public class BiomeHabitatClassifier : MonoBehaviour
{
    [SerializeField] private TileGrid grid;
    [SerializeField] private TileRuntimeStore runtimeStore;

    [Header("Ograniczenie habitatu")]
    [SerializeField, Min(1)] private int maxTilesPerHabitat = 5;

    [Header("Debug")]
    [SerializeField] private bool logClassification = true;
    [SerializeField] private bool verboseDebug = true;

    private HashSet<Tile> _visited;
    private HashSet<Tile> _unassignedOccupied;

    private void Awake()
    {
        if (!grid) grid = FindAnyObjectByType<TileGrid>();
        if (!runtimeStore) runtimeStore = FindAnyObjectByType<TileRuntimeStore>();
        _visited = new HashSet<Tile>();
        _unassignedOccupied = new HashSet<Tile>();
    }

    private void OnEnable()
    {
        TileEvents.TileStateChanged += OnTileStateChanged;
    }

    private void OnDisable()
    {
        TileEvents.TileStateChanged -= OnTileStateChanged;
    }

    private void OnTileStateChanged(Tile tile)
    {
        if (verboseDebug) Debug.Log("[BiomeHabitat] TileStateChanged — uruchamiam RunClassification");
        try
        {
            RunClassification(tile);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BiomeHabitat] Błąd w RunClassification: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>Od postawionego kafla buduje kulę o promieniu 5 w grafie, potem szuka w niej spójnej kombinacji 1..5 kafli pasującej do habitatu.</summary>
    public void RunClassification(Tile placedTile = null)
    {
        if (grid == null || runtimeStore == null)
        {
            if (verboseDebug) Debug.LogWarning("[BiomeHabitat] Brak grid lub runtimeStore — pomijam.");
            return;
        }

        if (_unassignedOccupied == null) return;

        _unassignedOccupied.Clear();
        try
        {
            foreach (var (tile, runtime) in runtimeStore.GetOccupiedTilesWithRuntime())
            {
                if (tile == null || runtime == null) continue;
                if (runtime.assignedAnimal == HabitatAnimal.None)
                    _unassignedOccupied.Add(tile);
            }
        }
        catch (InvalidOperationException e)
        {
            Debug.LogWarning($"[BiomeHabitat] Kolekcja zmodyfikowana podczas iteracji — pomijam tę klatkę. ({e.Message})");
            return;
        }

        if (verboseDebug) Debug.Log($"[BiomeHabitat] Zajęte nieprzypisane kafle: {_unassignedOccupied.Count}");
        if (_unassignedOccupied.Count == 0) return;

        if (placedTile != null && _unassignedOccupied.Contains(placedTile))
        {
            var ball = CollectBall(placedTile, maxDistance: maxTilesPerHabitat);
            if (ball.Count > 0 && TryFindHabitatInBall(ball, out var region, out var animal))
            {
                runtimeStore.AssignHabitatToTiles(region, animal);
                if (logClassification)
                    Debug.Log($"[BiomeHabitat] Zakwalifikowano region ({region.Count} kafli) do habitatu: {animal} (kula: {ball.Count} kafli)");
                return;
            }
        }

        _visited = _visited ?? new HashSet<Tile>();
        _visited.Clear();
        var starts = new List<Tile>(_unassignedOccupied);
        foreach (var start in starts)
        {
            if (start == null || _visited.Contains(start)) continue;
            var region = CollectRegionFallback(start);
            if (region.Count == 0) continue;
            var animal = TryClassifyRegion(region, out var field, out var forest, out var bushes, out var rocks, out var water);
            if (animal != HabitatAnimal.None)
            {
                runtimeStore.AssignHabitatToTiles(region, animal);
                if (logClassification)
                    Debug.Log($"[BiomeHabitat] Zakwalifikowano region ({region.Count} kafli) do habitatu: {animal}");
                return;
            }
            if (verboseDebug)
            {
                var msg = $"[BiomeHabitat] Region {region.Count} kafli (pole:{field} las:{forest} krzaki:{bushes} skały:{rocks} woda:{water}) — brak pasującego zwierzęcia.";
                msg += " " + GetWhyNoMatch(field, forest, bushes, rocks, water);
                Debug.Log(msg);
            }
        }
    }

    /// <summary>Kula w grafie: wszystkie nieprzypisane zajęte kafle w odległości ≤ maxDistance od center (liczba krawędzi).</summary>
    private HashSet<Tile> CollectBall(Tile center, int maxDistance)
    {
        var ball = new HashSet<Tile>();
        var queue = new Queue<(Tile t, int dist)>();
        queue.Enqueue((center, 0));
        ball.Add(center);
        while (queue.Count > 0)
        {
            var (t, dist) = queue.Dequeue();
            if (dist >= maxDistance) continue;
            IEnumerable<Tile> neighbors = null;
            try { neighbors = t.GetNeighbors(); } catch { continue; }
            if (neighbors == null) continue;
            foreach (var n in neighbors)
            {
                if (n == null || ball.Contains(n)) continue;
                if (!_unassignedOccupied.Contains(n)) continue;
                var r = runtimeStore.Get(n);
                if (!r.occupied || r.assignedAnimal != HabitatAnimal.None) continue;
                ball.Add(n);
                queue.Enqueue((n, dist + 1));
            }
        }
        return ball;
    }

    /// <summary>W kuli szukamy spójnej kombinacji 1..5 kafli pasującej do któregoś zwierzęcia. Dla każdego ziarna w kuli BFS do 5 kafli, bez duplikatów.</summary>
    private bool TryFindHabitatInBall(HashSet<Tile> ball, out List<Tile> bestRegion, out HabitatAnimal bestAnimal)
    {
        bestRegion = null;
        bestAnimal = HabitatAnimal.None;
        var seenKeys = new HashSet<string>();
        foreach (var seed in ball)
        {
            var region = CollectRegionWithinBall(seed, ball, maxSize: maxTilesPerHabitat);
            if (region.Count == 0) continue;
            var key = RegionKey(region);
            if (seenKeys.Contains(key)) continue;
            seenKeys.Add(key);
            var animal = TryClassifyRegion(region, out _, out _, out _, out _, out _);
            if (animal != HabitatAnimal.None)
            {
                bestRegion = region;
                bestAnimal = animal;
                return true;
            }
        }
        return false;
    }

    private static string RegionKey(List<Tile> region)
    {
        var list = new List<int>();
        foreach (var t in region)
            if (t != null) list.Add(t.i * 10000 + t.j);
        list.Sort();
        return string.Join(",", list);
    }

    /// <summary>BFS od seed tylko po kafelkach z ball, max maxSize. Zwraca spójny region.</summary>
    private List<Tile> CollectRegionWithinBall(Tile seed, HashSet<Tile> ball, int maxSize)
    {
        var region = new List<Tile>();
        var queue = new Queue<Tile>();
        var visited = new HashSet<Tile>();
        queue.Enqueue(seed);
        visited.Add(seed);
        while (queue.Count > 0 && region.Count < maxSize)
        {
            var t = queue.Dequeue();
            region.Add(t);
            if (region.Count >= maxSize) break;
            IEnumerable<Tile> neighbors = null;
            try { neighbors = t.GetNeighbors(); } catch { continue; }
            if (neighbors == null) continue;
            foreach (var n in neighbors)
            {
                if (n == null || visited.Contains(n) || !ball.Contains(n)) continue;
                visited.Add(n);
                queue.Enqueue(n);
            }
        }
        return region;
    }

    /// <summary>Fallback: jeden region do max 5 kafli od startu.</summary>
    private List<Tile> CollectRegionFallback(Tile start)
    {
        var region = new List<Tile>();
        var queue = new Queue<Tile>();
        if (start == null) return region;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            if (t == null) continue;
            if (region.Count < maxTilesPerHabitat)
            {
                region.Add(t);
                _visited.Add(t);
            }
            else continue;
            IEnumerable<Tile> neighbors = null;
            try { neighbors = t.GetNeighbors(); } catch { continue; }
            if (neighbors == null) continue;
            foreach (var neighbor in neighbors)
            {
                if (neighbor == null || _visited.Contains(neighbor)) continue;
                if (!_unassignedOccupied.Contains(neighbor)) continue;
                var r = runtimeStore.Get(neighbor);
                if (!r.occupied || r.assignedAnimal != HabitatAnimal.None) continue;
                queue.Enqueue(neighbor);
            }
        }
        return region;
    }

    /// <summary>Liczy biomy kafli w regionie i zwraca pierwsze pasujące zwierzę (Deer → Beaver → Bear), lub None.</summary>
    private HabitatAnimal TryClassifyRegion(IReadOnlyList<Tile> region, out int field, out int forest, out int bushes, out int rocks, out int water)
    {
        field = 0; forest = 0; bushes = 0; rocks = 0; water = 0;
        foreach (var t in region)
        {
            var r = runtimeStore.Get(t);
            switch (r.biome)
            {
                case TileBiome.Meadow:      field++;  break;
                case TileBiome.Forested:    forest++; break;
                case TileBiome.Bushy:       bushes++; break;
                case TileBiome.Mountainous: rocks++;  break;
                case TileBiome.Water:       water++;  break;
            }
        }

        // Deer: 2x plain, forest, bushes, water
        if (field >= 2 && forest >= 1 && bushes >= 1 && water >= 1)
            return HabitatAnimal.Deer;

        // Beaver: 3x forest, 2x water
        if (forest >= 3 && water >= 2)
            return HabitatAnimal.Beaver;

        // Bear: plain, forest, 2x rocks, water
        if (field >= 1 && forest >= 1 && rocks >= 2 && water >= 1)
            return HabitatAnimal.Bear;

        return HabitatAnimal.None;
    }

    /// <summary>Zwraca krótką podpowiedź, czego brakuje do którego zwierzęcia (dla verbose logów).</summary>
    private static string GetWhyNoMatch(int field, int forest, int bushes, int rocks, int water)
    {
        var missing = new List<string>();
        if (field < 2 || forest < 1 || bushes < 1 || water < 1)
            missing.Add($"Deer: potrzeba min. pole:{Mathf.Max(0, 2 - field)} las:{Mathf.Max(0, 1 - forest)} krzaki:{Mathf.Max(0, 1 - bushes)} woda:{Mathf.Max(0, 1 - water)}");
        if (forest < 3 || water < 2)
            missing.Add($"Beaver: potrzeba min. las:{Mathf.Max(0, 3 - forest)} woda:{Mathf.Max(0, 2 - water)}");
        if (field < 1 || forest < 1 || rocks < 2 || water < 1)
            missing.Add($"Bear: potrzeba min. pole:{Mathf.Max(0, 1 - field)} las:{Mathf.Max(0, 1 - forest)} skały:{Mathf.Max(0, 2 - rocks)} woda:{Mathf.Max(0, 1 - water)}");
        return missing.Count > 0 ? "Brakuje: " + string.Join(" | ", missing) : "";
    }
}

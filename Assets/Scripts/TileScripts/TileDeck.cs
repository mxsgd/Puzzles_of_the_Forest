using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class TilePrefabGroup
{
    [Tooltip("Biom kafla. Determinuje co populator wstawi w 12 trójkątach.")]
    public TileBiome biome = TileBiome.None;

    [Tooltip("Opcjonalny prefab kafla TYLKO dla tego biomu. " +
             "Jeśli null — używa się BaseTilePrefab z TileDeck. " +
             "Ustaw np. dla Wodnego (osobny shader) albo dla Górzystego (inna podstawa).")]
    public GameObject baseTilePrefabOverride;

    [Min(1), Tooltip("Waga w talii — ile razy ten biom trafia do puli przed tasowaniem.")]
    public int weight = 1;

    [Tooltip("Ikona dla UI podglądu talii.")]
    public Sprite icon;

    [Tooltip("Etykieta UI. Gdy puste — nazwa biomu (Lesisty/Łąkowy/...).")]
    public string displayName;
}

[Serializable]
public class TileDraw
{
    public TileBiome biome;
    public GameObject prefab;
    public Sprite icon;
    public string displayName;

    public TileDraw(TileBiome biome, GameObject prefab, Sprite icon, string displayName)
    {
        this.biome = biome;
        this.prefab = prefab;
        this.icon = icon;
        this.displayName = displayName;
    }
}

public class TileDeck : MonoBehaviour
{
    [Header("Ustawienia talii")]
    [SerializeField, Min(1)] private int deckSize = 30;
    [SerializeField] private bool rebuildOnStart = true;

    [Header("Domyślny prefab kafla")]
    [SerializeField, Tooltip("Bazowy heks używany dla biomów bez własnego override.")]
    private GameObject baseTilePrefab;

    [Header("Pula kafli")]
    [SerializeField] private List<TilePrefabGroup> tileGroups = new();

    [Header("Debug — podgląd puli (read-only)")]
    [SerializeField] private List<TileDraw> tilePool = new();

    [Header("Nagroda za utworzenie habitatu")]
    [SerializeField] private bool addTilesOnHabitatCreated = true;
    [SerializeField, Min(0)] private int tilesAddedPerHabitat = 3;

    private readonly Queue<TileDraw> _deck = new();

    public event Action<IReadOnlyList<TileDraw>> DeckChanged;
    public event Action DeckEmptied;

    public TileDraw Current => _deck.Count > 0 ? _deck.Peek() : null;
    public bool IsEmpty => _deck.Count == 0;
    public GameObject BaseTilePrefab => baseTilePrefab;

    /// <summary>Zwraca prefab kafla używany dla danego biomu (override grupy lub default).</summary>
    public GameObject GetPrefabFor(TileBiome biome)
    {
        foreach (var g in tileGroups)
            if (g != null && g.biome == biome && g.baseTilePrefabOverride != null)
                return g.baseTilePrefabOverride;
        return baseTilePrefab;
    }

    private void Awake()
    {
        if (rebuildOnStart) RebuildDeck();
    }

    private void OnEnable()
    {
        if (addTilesOnHabitatCreated)
            TileEvents.HabitatAssigned += OnHabitatAssigned;
    }

    private void OnDisable()
    {
        TileEvents.HabitatAssigned -= OnHabitatAssigned;
    }

    private void OnHabitatAssigned(HabitatAssignmentData _)
    {
        if (tilesAddedPerHabitat > 0)
            EnqueueRandomTilesFromPool(tilesAddedPerHabitat);
    }

    /// <summary>
    /// Dokłada losowe kafle z aktualnej puli biomów na koniec kolejki talii (jak nagroda).
    /// </summary>
    public void EnqueueRandomTilesFromPool(int count)
    {
        if (count <= 0)
            return;

        var pool = BuildPool();
        if (pool.Count == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            _deck.Enqueue(new TileDraw(pick.biome, pick.prefab, pick.icon, pick.displayName));
        }

        NotifyDeckChanged();
    }

    public void RebuildDeck()
    {
        _deck.Clear();
        var pool = BuildPool();
        if (pool.Count == 0) { NotifyDeckChanged(); return; }

        var buffer = new List<TileDraw>(pool.Count);
        while (_deck.Count < deckSize)
        {
            buffer.Clear();
            buffer.AddRange(pool);
            Shuffle(buffer);
            foreach (var draw in buffer)
            {
                _deck.Enqueue(draw);
                if (_deck.Count >= deckSize) break;
            }
        }
        NotifyDeckChanged();
    }

    public TileDraw DrawTile()
    {
        if (_deck.Count == 0) { DeckEmptied?.Invoke(); return null; }

        var draw = _deck.Dequeue();
        NotifyDeckChanged();
        if (_deck.Count == 0) DeckEmptied?.Invoke();
        return draw;
    }

    public IReadOnlyList<TileDraw> GetQueuedTiles() => _deck.ToList();

    /// <summary>
    /// Wymienia pierwszą kartę na talii (Current) na losową z aktualnej puli biomów.
    /// Zwraca true gdy udało się wykonać reroll. Talia zachowuje rozmiar.
    /// </summary>
    public bool RerollCurrent()
    {
        if (_deck.Count == 0) return false;

        var pool = BuildPool();
        if (pool.Count == 0) return false;

        var rest = new List<TileDraw>(_deck.Count - 1);
        _deck.Dequeue();
        while (_deck.Count > 0) rest.Add(_deck.Dequeue());

        var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
        _deck.Enqueue(new TileDraw(pick.biome, pick.prefab, pick.icon, pick.displayName));
        foreach (var item in rest) _deck.Enqueue(item);

        NotifyDeckChanged();
        return true;
    }

    private List<TileDraw> BuildPool()
    {
        var pool = new List<TileDraw>();
        foreach (var group in tileGroups)
        {
            if (group == null || group.biome == TileBiome.None) continue;

            var prefab = group.baseTilePrefabOverride != null
                ? group.baseTilePrefabOverride
                : baseTilePrefab;
            if (prefab == null) continue;

            var label = string.IsNullOrWhiteSpace(group.displayName)
                ? TileBiomeRules.GetDisplayName(group.biome)
                : group.displayName;

            int weight = Mathf.Max(1, group.weight);
            for (int w = 0; w < weight; w++)
                pool.Add(new TileDraw(group.biome, prefab, group.icon, label));
        }
        return pool;
    }

    private void NotifyDeckChanged() => DeckChanged?.Invoke(GetQueuedTiles());

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    [ContextMenu("Refresh Pool Preview")]
    private void RefreshPoolPreview() => tilePool = BuildPool();
}

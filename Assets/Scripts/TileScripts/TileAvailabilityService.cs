using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

public class TileAvailabilityService : MonoBehaviour
{
    [SerializeField] private TileGrid grid;
    [SerializeField] private TileRuntimeStore runtime;

    private readonly HashSet<Tile> _available = new();

    private void OnEnable()
    {
        TileEvents.TileStateChanged += OnTileStateChanged;
        RebuildCache();
    }

    private void OnDisable()
    {
        TileEvents.TileStateChanged -= OnTileStateChanged;
        _available.Clear();
    }

    // -------------------------------------------------------------------------
    // Publiczne API
    // -------------------------------------------------------------------------

    public IEnumerable<Tile> GetAvailable() => _available;

    // -------------------------------------------------------------------------
    // Obsługa zdarzeń — przyrostowa aktualizacja
    // -------------------------------------------------------------------------

    private void OnTileStateChanged(Tile tile)
    {
        if (tile == null || grid == null || runtime == null)
            return;

        var rt = runtime.Get(tile);
        if (rt == null) return;

        if (rt.occupied)
            HandleTilePlaced(tile, rt);
        else
            HandleTileFreed(tile);
    }

    /// <summary>
    /// Kafel właśnie zajęty:
    /// — nie jest już dostępny do postawienia,
    /// — jego wolni sąsiedzi stają się dostępni.
    /// </summary>
    private void HandleTilePlaced(Tile tile, TileRuntimeStore.Runtime rt)
    {
        SetAvailable(tile, false);

        foreach (Tile n in grid.GetNeighbors(tile))
        {
            if (n == null) continue;
            var rn = runtime.Get(n);
            if (rn == null || rn.occupied) continue;

            SetAvailable(n, true);
            if (rn.templatePrefab == null)
                rn.templatePrefab = rt.templatePrefab;
        }
    }

    /// <summary>
    /// Kafel właśnie zwolniony:
    /// — może być dostępny jeśli ma zajętego sąsiada,
    /// — sąsiedzi bez żadnego innego zajętego sąsiada tracą dostępność.
    /// </summary>
    private void HandleTileFreed(Tile tile)
    {
        bool tileHasOccupiedNeighbor = false;
        foreach (Tile n in grid.GetNeighbors(tile))
        {
            if (n == null) continue;
            if (runtime.Get(n)?.occupied == true)
            {
                tileHasOccupiedNeighbor = true;
                break;
            }
        }
        SetAvailable(tile, tileHasOccupiedNeighbor);

        foreach (Tile n in grid.GetNeighbors(tile))
        {
            if (n == null) continue;
            var rn = runtime.Get(n);
            if (rn == null || rn.occupied) continue;

            bool neighborHasOtherOccupied = false;
            foreach (Tile nn in grid.GetNeighbors(n))
            {
                if (nn == null || ReferenceEquals(nn, tile)) continue;
                if (runtime.Get(nn)?.occupied == true)
                {
                    neighborHasOtherOccupied = true;
                    break;
                }
            }

            if (!neighborHasOtherOccupied)
                SetAvailable(n, false);
        }
    }

    // -------------------------------------------------------------------------
    // Pełny rebuild (raz na start / ContextMenu)
    // -------------------------------------------------------------------------

    [ContextMenu("Rebuild Availability Cache")]
    public void RebuildCache()
    {
        _available.Clear();
        if (grid?.tiles == null || runtime == null)
            return;

        foreach (Tile t in grid.tiles)
        {
            var r = runtime.Get(t);
            if (r != null) r.available = false;
        }

        foreach (Tile t in grid.tiles)
        {
            var rt = runtime.Get(t);
            if (rt == null || !rt.occupied) continue;

            foreach (Tile n in grid.GetNeighbors(t))
            {
                if (n == null) continue;
                var rn = runtime.Get(n);
                if (rn == null || rn.occupied) continue;

                SetAvailable(n, true);
                if (rn.templatePrefab == null)
                    rn.templatePrefab = rt.templatePrefab;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void SetAvailable(Tile tile, bool available)
    {
        var r = runtime.Get(tile);
        if (r != null) r.available = available;

        if (available)
            _available.Add(tile);
        else
            _available.Remove(tile);
    }
}

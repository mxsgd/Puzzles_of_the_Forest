using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TileGrid tileGrid;
    [SerializeField] private TilePlacementService placement;
    [SerializeField] private TileRuntimeStore runtime;
    [SerializeField] private  TileDeck tileDeck;

    [Header("Starting Tile")]
    [SerializeField] private bool placeStartingTileOnStart;

    private void Awake()
    {
        if (!tileGrid)   tileGrid   = FindAnyObjectByType<TileGrid>();
        if (!placement)  placement  = FindAnyObjectByType<TilePlacementService>();
        if (!runtime)    runtime    = FindAnyObjectByType<TileRuntimeStore>();
        if (!tileDeck)    tileDeck    = FindAnyObjectByType<TileDeck>();

        if (FindAnyObjectByType<GameFlowController>() == null)
            gameObject.AddComponent<GameFlowController>();
    }

    private void Start()
    {
        if (placeStartingTileOnStart)
            StartCoroutine(PlaceStartingTileWhenReady());
    }

    private IEnumerator PlaceStartingTileWhenReady()
    {
        // Poczekaj aż TileGrid, TileDeck i reszta systemów się zainicjalizują.
        yield return null;
        yield return null;

        if (tileDeck != null && (tileDeck.IsEmpty || tileDeck.Current == null))
            tileDeck.RebuildDeck();

        PlaceStartingTile();

        var availability = FindAnyObjectByType<TileAvailabilityService>();
        availability?.RebuildCache();
    }

    /// <summary>Stawia kafel startowy na środku siatki (nie zużywa karty z talii).</summary>
    public bool PlaceStartingTile()
    {
        if (!tileGrid)   tileGrid   = FindAnyObjectByType<TileGrid>();
        if (!placement)  placement  = FindAnyObjectByType<TilePlacementService>();
        if (!runtime)    runtime    = FindAnyObjectByType<TileRuntimeStore>();
        if (!tileDeck)   tileDeck   = FindAnyObjectByType<TileDeck>();

        if (!tileGrid || !placement || !runtime)
        {
            Debug.LogWarning("[GameManager] PlaceStartingTile: brak TileGrid / Placement / RuntimeStore.");
            return false;
        }

        var centerTile = tileGrid.GetCenterTile();
        if (centerTile == null)
        {
            tileGrid.BuildGrid();
            centerTile = tileGrid.GetCenterTile();
        }
        if (centerTile == null)
        {
            Debug.LogWarning("[GameManager] PlaceStartingTile: brak kafelka środkowego (siatka nie zbudowana?).");
            return false;
        }

        var rt = runtime.Get(centerTile);
        if (rt != null && rt.occupied)
            return true;

        var rotation = tileGrid.transform.rotation;

        // Kafel startowy nie zużywa karty z talii — gracz ma pełne SessionTileCount kafli.
        TileDraw draw = tileDeck != null ? tileDeck.Current : null;
        if (draw == null && tileDeck != null)
        {
            tileDeck.RebuildDeck();
            draw = tileDeck.Current;
        }

        var instance = placement.PlaceOccupant(centerTile, rotation, draw);
        if (instance == null)
        {
            Debug.LogWarning("[GameManager] PlaceStartingTile: PlaceOccupant zwrócił null (prefab / draw?).");
            return false;
        }

        return true;
    }
}
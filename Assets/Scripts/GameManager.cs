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

        if (FindAnyObjectByType<TileNeighborMatchScorer>() == null)
            gameObject.AddComponent<TileNeighborMatchScorer>();

        if (FindAnyObjectByType<SameBiomeConnectionAnimator>() == null)
            gameObject.AddComponent<SameBiomeConnectionAnimator>();

        if (FindAnyObjectByType<ActivePerksHudView>() == null)
            gameObject.AddComponent<ActivePerksHudView>();

        if (FindAnyObjectByType<BackgroundQuad>() == null)
            gameObject.AddComponent<BackgroundQuad>();

        if (FindAnyObjectByType<WindLeavesVfx>() == null)
            gameObject.AddComponent<WindLeavesVfx>();

        EnsureMusicVolumeApplier();
    }

    /// <summary>Binds the scene's "Soundtrack" AudioSource to the Music volume slider.</summary>
    private static void EnsureMusicVolumeApplier()
    {
        var soundtrack = GameObject.Find("Soundtrack");
        if (soundtrack == null) return;

        var source = soundtrack.GetComponent<AudioSource>();
        if (source == null) return;

        if (soundtrack.GetComponent<MusicVolumeApplier>() == null)
            soundtrack.AddComponent<MusicVolumeApplier>();
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
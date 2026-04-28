using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TileGrid tileGrid;
    [SerializeField] private TilePlacementService placement;
    [SerializeField] private TileRuntimeStore runtime;
    [SerializeField] private  TileDeck tileDeck;

    [Header("Starting Tile")]
    [SerializeField] private bool placeStartingTileOnStart = true;

    private void Awake()
    {
        if (!tileGrid)   tileGrid   = FindAnyObjectByType<TileGrid>();
        if (!placement)  placement  = FindAnyObjectByType<TilePlacementService>();
        if (!runtime)    runtime    = FindAnyObjectByType<TileRuntimeStore>();
        if (!tileDeck)    tileDeck    = FindAnyObjectByType<TileDeck>();
    }

    private void Start()
    {
        if (placeStartingTileOnStart)
            PlaceStartingTile();
    }

    public void PlaceStartingTile()
    {
        if (!tileGrid || !placement || !runtime)
            return;

        var centerTile = tileGrid.GetCenterTile();
        if (centerTile == null)
            return;

        var rt = runtime.Get(centerTile);
        if (rt != null && rt.occupied)
            return;

        var rotation = tileGrid.transform.rotation;

        var instance = placement.PlaceOccupant(centerTile, rotation, tileDeck.DrawTile());
        if (instance == null)
            return;
    }
}
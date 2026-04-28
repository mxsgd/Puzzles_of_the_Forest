using UnityEngine;

public class TileObject : MonoBehaviour
{
    [SerializeField] private TileGrid grid;
    [SerializeField, TextArea(2,4)] private string debugInfo; // Podgląd w Inspectorze
    
    private TileGrid.Tile tile;

    public TileGrid Grid => grid;
    public TileGrid.Tile Tile => tile;

    public void AssignTile(TileGrid parentGrid, TileGrid.Tile assignedTile)
    {
        grid = parentGrid;
        tile = assignedTile;
        
        #if UNITY_EDITOR
        // Debug info dla Inspectora
        debugInfo = tile != null 
            ? $"Tile [{tile.i},{tile.j}] | Axial [{tile.q},{tile.r}]\nPos: {tile.worldPos}"
            : "No tile assigned";
        #endif
    }

    // Pomocnicza metoda dla animacji/VFX
    public bool IsAssigned => tile != null && grid != null;
}
using System;
using System.Collections.Generic;
using Tile = TileGrid.Tile;

public static class TileEvents
{
    public static event Action<Tile> TileStateChanged;

    /// <summary>Emisja, gdy region kafli zostaje zakwalifikowany jako nowy habitat zwierzęcia.</summary>
    public static event Action<HabitatAnimal, IReadOnlyList<Tile>> HabitatAssigned;

    public static void RaiseTileStateChanged(Tile tile)
        => TileStateChanged?.Invoke(tile);

    public static void RaiseHabitatAssigned(HabitatAnimal animal, IReadOnlyList<Tile> tiles)
        => HabitatAssigned?.Invoke(animal, tiles);
}

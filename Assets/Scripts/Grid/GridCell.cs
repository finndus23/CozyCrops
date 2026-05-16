public class GridCell
{
    public int X { get; }
    public int Z { get; }
    public TileType Type { get; set; }
    public bool IsOccupied => Type != TileType.Grass;

    // Farming-Zustand
    public bool IsLocked { get; set; }
    public bool IsTilled { get; set; }
    public PlantInstance Plant { get; private set; }
    public bool HasPlant => Plant != null;
    public FarmTileVisual TileVisual { get; set; }

    public GridCell(int x, int z)
    {
        X = x;
        Z = z;
        Type = TileType.Grass;
    }

    public bool TryPlant(PlantInstance plant)
    {
        if (!IsTilled || HasPlant || Type != TileType.FarmPlot) return false;
        Plant = plant;
        return true;
    }

    public PlantInstance Harvest()
    {
        var harvested = Plant;
        Plant = null;
        IsTilled = false;
        return harvested;
    }
}

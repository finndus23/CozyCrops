public class GridCell
{
    public int X { get; }
    public int Z { get; }
    public TileType Type { get; set; }
    public bool IsOccupied => Type != TileType.Grass;

    // Farming-Zustand
    public bool IsLocked { get; set; }
    public bool IsTilled { get; set; }

    /// <summary>
    /// Kompost-Dünger aufgetragen — Wachstum und Feldarbeit auf dieser Tile sind schneller
    /// (siehe PlantManager.TickGrowth/ToolUseHandler). Gilt nur für EINEN Anbauzyklus:
    /// Harvest() setzt das zurück, genau wie IsTilled.
    /// </summary>
    public bool IsFertilized { get; set; }

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
        IsFertilized = false;
        return harvested;
    }

    /// <summary>
    /// Wird nur vom Save-/Load-System benutzt, damit private Plant-Daten sauber zur�ckgesetzt werden k�nnen.
    /// </summary>
    public void ApplyLoadedPlant(PlantInstance plant)
    {
        Plant = plant;
        if (plant != null)
            IsTilled = true;
    }

    /// <summary>
    /// Wird nur vom Save-/Load-System benutzt.
    /// </summary>
    public void ClearLoadedPlant()
    {
        Plant = null;
    }
}

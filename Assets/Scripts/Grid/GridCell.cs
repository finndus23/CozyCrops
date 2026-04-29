public class GridCell
{
    public int X { get; }
    public int Z { get; }
    public TileType Type { get; set; }
    public bool IsOccupied => Type != TileType.Grass;

    public GridCell(int x, int z)
    {
        X = x;
        Z = z;
        Type = TileType.Grass;
    }
}

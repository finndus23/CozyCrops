using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [SerializeField] private int width = 20;
    [SerializeField] private int height = 20;
    [SerializeField] private float cellSize = 1f;

    [SerializeField] private GameObject grassTilePrefab;
    [SerializeField] private GameObject farmPlotPrefab;
    [SerializeField] private GameObject pathTilePrefab;

    private GridCell[,] cells;
    private GameObject[,] tileObjects;

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    void Awake()
    {
        Instance = this;
        InitializeGrid();
    }

    void InitializeGrid()
    {
        cells = new GridCell[width, height];
        tileObjects = new GameObject[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                cells[x, z] = new GridCell(x, z);
                SpawnTile(x, z, grassTilePrefab);
            }
        }
    }

    public bool TryPlaceTile(int x, int z, TileType type)
    {
        if (!IsInBounds(x, z)) return false;
        if (type == TileType.Grass) return false;
        if (cells[x, z].Type == type) return false;

        cells[x, z].Type = type;
        ReplaceTile(x, z, type switch
        {
            TileType.FarmPlot => farmPlotPrefab,
            TileType.Path     => pathTilePrefab,
            _                 => grassTilePrefab
        });
        return true;
    }

    public bool TryRemoveTile(int x, int z)
    {
        if (!IsInBounds(x, z)) return false;
        if (cells[x, z].Type == TileType.Grass) return false;

        cells[x, z].Type = TileType.Grass;
        ReplaceTile(x, z, grassTilePrefab);
        return true;
    }

    public void ApplyToSelection(TileType type)
    {
        foreach (var cell in SelectionManager.Instance.SelectedCells)
        {
            if (type == TileType.Grass)
                TryRemoveTile(cell.x, cell.y);
            else
                TryPlaceTile(cell.x, cell.y, type);
        }
        SelectionManager.Instance.ClearSelection();
    }

    public GridCell GetCell(int x, int z) => IsInBounds(x, z) ? cells[x, z] : null;

    public bool WorldToGrid(Vector3 worldPos, out int x, out int z)
    {
        x = Mathf.FloorToInt(worldPos.x / cellSize);
        z = Mathf.FloorToInt(worldPos.z / cellSize);
        return IsInBounds(x, z);
    }

    public Vector3 GridToWorld(int x, int z)
    {
        return new Vector3(x * cellSize + cellSize * 0.5f, 0f, z * cellSize + cellSize * 0.5f);
    }

    public bool IsInBounds(int x, int z) => x >= 0 && x < width && z >= 0 && z < height;

    private void SpawnTile(int x, int z, GameObject prefab)
    {
        if (prefab == null) return;
        tileObjects[x, z] = Instantiate(prefab, GridToWorld(x, z), Quaternion.identity, transform);
    }

    private void ReplaceTile(int x, int z, GameObject prefab)
    {
        if (tileObjects[x, z] != null)
            Destroy(tileObjects[x, z]);
        SpawnTile(x, z, prefab);
    }
}

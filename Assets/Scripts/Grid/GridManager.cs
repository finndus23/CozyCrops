using System;
using System.Collections.Generic;
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
    [SerializeField] private GameObject borderTilePrefab;

    private GridCell[,] cells;
    private GameObject[,] tileObjects;

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    void Awake()
    {
        Instance = this;

        // Wenn bereits Kinder vorhanden sind → vorgebautes Grid laden
        if (transform.childCount > 0)
            LoadPrebuiltTiles();
        else
            InitializeGrid();
    }

    // ──────────────────────────────────────────────
    // Editor-only: Grid im Scene-View vorbauen
    // ──────────────────────────────────────────────

    [ContextMenu("Grid generieren")]
    private void GenerateGrid()
    {
        // Vorhandene Tiles bereinigen (DestroyImmediate für Editor-Zeit)
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        cells = new GridCell[width, height];
        tileObjects = new GameObject[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                cells[x, z] = new GridCell(x, z);
                SpawnTile(x, z, grassTilePrefab, TileType.Grass);
            }
        }

        SpawnBorderRing();

#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[GridManager] Grid generiert: {width}x{height} Tiles + Border als Scene-Objekte gespeichert.");
#endif
    }

    // ──────────────────────────────────────────────
    // Runtime: vorgebaute Tiles laden
    // ──────────────────────────────────────────────

    private void LoadPrebuiltTiles()
    {
        cells = new GridCell[width, height];
        tileObjects = new GameObject[width, height];

        // Leere GridCells anlegen
        for (int x = 0; x < width; x++)
            for (int z = 0; z < height; z++)
                cells[x, z] = new GridCell(x, z);

        // Vorhandene Scene-Kinder auf Grid-Koordinaten mappen
        foreach (Transform child in transform)
        {
            if (!WorldToGrid(child.position, out int x, out int z)) continue;

            tileObjects[x, z] = child.gameObject;
            cells[x, z].TileVisual = child.GetComponent<FarmTileVisual>();

            var marker = child.GetComponent<TileMarker>();
            if (marker != null)
                cells[x, z].Type = marker.tileType;
        }

        Debug.Log($"[GridManager] {transform.childCount} vorgebaute Tiles geladen.");
    }

    // ──────────────────────────────────────────────
    // Runtime: Grid zur Laufzeit neu generieren (Fallback)
    // ──────────────────────────────────────────────

    private void InitializeGrid()
    {
        cells = new GridCell[width, height];
        tileObjects = new GameObject[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                cells[x, z] = new GridCell(x, z);
                SpawnTile(x, z, grassTilePrefab, TileType.Grass);
            }
        }

        SpawnBorderRing();
    }

    // ──────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────

    public bool TryPlaceTile(int x, int z, TileType type)
    {
        if (!IsInBounds(x, z)) return false;
        if (cells[x, z].IsLocked) return false;
        if (cells[x, z].HasPlant) return false;
        if (type == TileType.Grass) return false;
        if (cells[x, z].Type == type) return false;

        cells[x, z].Type = type;
        cells[x, z].IsTilled = false;
        cells[x, z].ClearLoadedPlant();

        ReplaceTile(x, z, GetPrefabForType(type), type);
        return true;
    }

    public bool TryRemoveTile(int x, int z)
    {
        if (!IsInBounds(x, z)) return false;
        if (cells[x, z].IsLocked) return false;
        if (cells[x, z].HasPlant) return false;
        if (cells[x, z].Type == TileType.Grass) return false;

        cells[x, z].Type = TileType.Grass;
        cells[x, z].IsTilled = false;
        cells[x, z].ClearLoadedPlant();

        ReplaceTile(x, z, grassTilePrefab, TileType.Grass);
        return true;
    }

    public bool TryApplyTile(int x, int z, TileType type)
    {
        bool changed = type == TileType.Grass
            ? TryRemoveTile(x, z)
            : TryPlaceTile(x, z, type);

        if (changed && FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();

        return changed;
    }

    public void ApplyToSelection(TileType type)
    {
        bool changed = false;

        foreach (var cell in SelectionManager.Instance.SelectedCells)
        {
            bool success;

            if (type == TileType.Grass)
                success = TryRemoveTile(cell.x, cell.y);
            else
                success = TryPlaceTile(cell.x, cell.y, type);

            if (success)
                changed = true;
        }

        SelectionManager.Instance.ClearSelection();

        if (changed && FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();
    }

    /// <summary>
    /// Wird vom Save-/Load-System benutzt. Setzt alle gespeicherten Tiles, Pflanzen und Visuals zurück.
    /// </summary>
    public void ApplySaveTiles(List<TileSaveData> savedTiles)
    {
        if (savedTiles == null)
        {
            Debug.LogWarning("[GridManager] Keine Tile-Daten im Save vorhanden.");
            return;
        }

        PlantManager.Instance?.ClearAllPlantVisuals();

        int appliedCount = 0;
        int skippedCount = 0;
        int loadedPlantCount = 0;

        foreach (TileSaveData tileData in savedTiles)
        {
            if (tileData == null)
            {
                skippedCount++;
                continue;
            }

            if (!IsInBounds(tileData.x, tileData.z))
            {
                skippedCount++;
                continue;
            }

            ApplySingleSavedTile(tileData);
            appliedCount++;

            if (tileData.hasPlant)
                loadedPlantCount++;
        }

        // Nach dem Wiederherstellen aller Cell-Daten baut der PlantManager seine interne Liste neu auf.
        PlantManager.Instance?.RebuildLoadedPlantsFromGrid();

        Debug.Log($"[GridManager] Save angewendet. Applied={appliedCount}, Skipped={skippedCount}, PlantsInSave={loadedPlantCount}");
    }

    public GridCell GetCell(int x, int z) => IsInBounds(x, z) ? cells[x, z] : null;
    public GameObject GetTileObject(int x, int z) => IsInBounds(x, z) ? tileObjects[x, z] : null;

    public bool WorldToGrid(Vector3 worldPos, out int x, out int z)
    {
        Vector3 local = worldPos - transform.position;
        x = Mathf.FloorToInt(local.x / cellSize);
        z = Mathf.FloorToInt(local.z / cellSize);
        return IsInBounds(x, z);
    }

    public Vector3 GridToWorld(int x, int z)
    {
        return transform.position + new Vector3(x * cellSize + cellSize * 0.5f, 0f, z * cellSize + cellSize * 0.5f);
    }

    public bool IsInBounds(int x, int z) => x >= 0 && x < width && z >= 0 && z < height;

    // ──────────────────────────────────────────────
    // Save-/Load-Hilfen
    // ──────────────────────────────────────────────

    private void ApplySingleSavedTile(TileSaveData tileData)
    {
        GridCell cell = cells[tileData.x, tileData.z];

        TileType loadedType = ParseTileType(tileData.tileType);
        TileType previousType = cell.Type;

        cell.Type = loadedType;
        cell.IsLocked = tileData.isLocked;
        cell.IsTilled = loadedType == TileType.FarmPlot && tileData.isTilled;
        cell.ClearLoadedPlant();

        if (tileObjects[tileData.x, tileData.z] == null || previousType != loadedType)
            ReplaceTile(tileData.x, tileData.z, GetPrefabForType(loadedType), loadedType);
        else
            EnsureMarker(tileObjects[tileData.x, tileData.z], loadedType);

        if (loadedType == TileType.FarmPlot && tileData.hasPlant)
            ApplyLoadedPlant(cell, tileData);

        RefreshFarmTileVisual(cell);
    }

    private void ApplyLoadedPlant(GridCell cell, TileSaveData tileData)
    {
        if (PlantDatabase.Instance == null)
        {
            Debug.LogWarning("[GridManager] Kein PlantDatabase in der Scene gefunden. Pflanze konnte nicht geladen werden.");
            return;
        }

        PlantType plantType = PlantDatabase.Instance.GetById(tileData.plantId);
        if (plantType == null)
        {
            Debug.LogWarning($"[GridManager] PlantType mit SaveId '{tileData.plantId}' nicht gefunden.");
            return;
        }

        PlantInstance loadedPlant = new PlantInstance(
            plantType,
            tileData.plantStageIndex,
            tileData.plantGrowthTimer,
            tileData.plantWateringsThisStage);

        cell.ApplyLoadedPlant(loadedPlant);
    }

    private void RefreshFarmTileVisual(GridCell cell)
    {
        if (cell == null) return;
        if (cell.Type != TileType.FarmPlot) return;
        if (cell.TileVisual == null) return;

        if (!cell.IsTilled && !cell.HasPlant)
        {
            cell.TileVisual.SetState(FarmTileState.Dry);
            return;
        }

        if (cell.HasPlant && cell.Plant != null && !cell.Plant.IsFullyGrown &&
            cell.Plant.WateringsThisStage >= cell.Plant.Type.wateringsPerStage)
        {
            cell.TileVisual.SetState(FarmTileState.Watered);
            return;
        }

        cell.TileVisual.SetState(FarmTileState.Tilled);
    }

    private TileType ParseTileType(string value)
    {
        if (Enum.TryParse(value, out TileType parsed))
            return parsed;

        return TileType.Grass;
    }

    private GameObject GetPrefabForType(TileType type)
    {
        return type switch
        {
            TileType.FarmPlot => farmPlotPrefab,
            TileType.Path => pathTilePrefab,
            _ => grassTilePrefab
        };
    }

    // ──────────────────────────────────────────────
    // Interne Hilfsmethoden
    // ──────────────────────────────────────────────

    private void SpawnTile(int x, int z, GameObject prefab, TileType type)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[GridManager] Kein Prefab für TileType {type} bei {x},{z} gesetzt.");
            return;
        }

        var go = Instantiate(prefab, GridToWorld(x, z), Quaternion.identity, transform);
        tileObjects[x, z] = go;
        cells[x, z].Type = type;
        cells[x, z].TileVisual = go.GetComponent<FarmTileVisual>();

        EnsureMarker(go, type);
    }

    private void ReplaceTile(int x, int z, GameObject prefab, TileType type)
    {
        if (tileObjects[x, z] != null)
        {
            // Sofort ausblenden, weil Destroy() erst am Frame-Ende wirklich löscht.
            // Sonst kann beim F6-Test kurz das alte Default-Tile über dem geladenen Tile liegen.
            tileObjects[x, z].SetActive(false);
            Destroy(tileObjects[x, z]);
            tileObjects[x, z] = null;
        }

        cells[x, z].TileVisual = null;
        SpawnTile(x, z, prefab, type);

        // Feel-Good-Polish: Tile poppt beim Wechseln (Farmland/Path/Gras) rein statt hart zu erscheinen.
        var newTile = tileObjects[x, z];
        if (newTile != null && !newTile.TryGetComponent<PopInFx>(out _))
            newTile.AddComponent<PopInFx>();
    }

    private void EnsureMarker(GameObject go, TileType type)
    {
        if (go == null) return;

        var marker = go.GetComponent<TileMarker>() ?? go.AddComponent<TileMarker>();
        marker.tileType = type;
    }

    private void SpawnBorderRing()
    {
        if (borderTilePrefab == null) return;

        // Oben und unten (inkl. Ecken)
        for (int x = -1; x <= width; x++)
        {
            SpawnBorderTile(x, -1);
            SpawnBorderTile(x, height);
        }

        // Links und rechts (ohne Ecken)
        for (int z = 0; z < height; z++)
        {
            SpawnBorderTile(-1, z);
            SpawnBorderTile(width, z);
        }
    }

    private void SpawnBorderTile(int x, int z)
    {
        Instantiate(borderTilePrefab, GridToWorld(x, z), Quaternion.identity, transform);
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kümmert sich um den kompletten Pflanzen-Gameloop:
/// Erde lockern → Samen pflanzen → Gießen → Wachsen → Ernten
/// </summary>
public class PlantManager : MonoBehaviour
{
    public static PlantManager Instance { get; private set; }

    // Alle aktiven Pflanzen mit ihrer Zelle
    private readonly Dictionary<GridCell, GameObject> plantVisuals = new();
    private readonly List<GridCell> activePlants = new();

    void Awake() => Instance = this;

    void Update()
    {
        TickGrowth();
    }

    // --- Aktionen ---

    /// <summary>Hacke: Erde lockern auf einer FarmPlot-Zelle.</summary>
    public bool TryTill(int x, int z)
    {
        var cell = GridManager.Instance.GetCell(x, z);
        if (cell == null || cell.IsLocked) return false;
        if (cell.Type != TileType.FarmPlot) return false;
        if (cell.IsTilled || cell.HasPlant) return false;

        cell.IsTilled = true;
        cell.TileVisual?.SetState(FarmTileState.Tilled);

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();

        return true;
    }

    /// <summary>Samen: Pflanze setzen auf gelockerter Erde.</summary>
    public bool TryPlant(int x, int z, PlantType type)
    {
        if (type == null) return false;

        var cell = GridManager.Instance.GetCell(x, z);
        if (cell == null || cell.IsLocked) return false;

        if (!PlayerInventory.Instance.TryUseSeed(type)) return false;

        var instance = new PlantInstance(type);
        if (!cell.TryPlant(instance))
        {
            // Samen zurückgeben wenn pflanzen fehlschlug
            PlayerInventory.Instance.AddSeed(type);
            return false;
        }

        if (!activePlants.Contains(cell))
            activePlants.Add(cell);

        SpawnVisual(cell);

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();

        return true;
    }

    /// <summary>Gießkanne: Pflanze auf einer Zelle gießen.</summary>
    public bool TryWater(int x, int z)
    {
        var cell = GridManager.Instance.GetCell(x, z);
        if (cell == null || cell.IsLocked || !cell.HasPlant) return false;

        cell.Plant.Water();
        if (cell.Plant.WateringsThisStage >= cell.Plant.Type.wateringsPerStage)
            cell.TileVisual?.SetState(FarmTileState.Watered);

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();

        return true;
    }

    /// <summary>Sichel: Reife Pflanze ernten.</summary>
    public bool TryHarvest(int x, int z)
    {
        var cell = GridManager.Instance.GetCell(x, z);
        if (cell == null || cell.IsLocked || !cell.HasPlant) return false;
        if (!cell.Plant.IsFullyGrown) return false;

        var harvested = cell.Harvest();
        PlayerInventory.Instance.AddCrop(harvested.Type);

        cell.TileVisual?.SetState(FarmTileState.Dry);
        RemoveVisual(cell);
        activePlants.Remove(cell);

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();

        return true;
    }

    // --- Save-/Load-Hilfen ---

    /// <summary>
    /// Löscht alle Pflanzen-GameObjects, aber verändert nicht die GridCell-Daten.
    /// Wird beim Laden benutzt, bevor die gespeicherten Pflanzen neu aufgebaut werden.
    /// </summary>
    public void ClearAllPlantVisuals()
    {
        foreach (var kvp in plantVisuals)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }

        plantVisuals.Clear();
        activePlants.Clear();
    }

    /// <summary>
    /// Baut activePlants und Pflanzen-Visuals aus den aktuell geladenen GridCell-Daten neu auf.
    /// </summary>
    public void RebuildLoadedPlantsFromGrid()
    {
        ClearAllPlantVisuals();

        GridManager grid = GridManager.Instance;
        if (grid == null) return;

        for (int x = 0; x < grid.Width; x++)
        {
            for (int z = 0; z < grid.Height; z++)
            {
                GridCell cell = grid.GetCell(x, z);
                if (cell == null || !cell.HasPlant) continue;

                if (!activePlants.Contains(cell))
                    activePlants.Add(cell);

                SpawnVisual(cell);
            }
        }

        Debug.Log($"[PlantManager] {activePlants.Count} geladene Pflanze(n) registriert.");
    }

    // --- Wachstum ---

    private void TickGrowth()
    {
        // Rückwärts iterieren, damit spätere Änderungen an der Liste nicht so leicht Probleme machen.
        for (int i = activePlants.Count - 1; i >= 0; i--)
        {
            var cell = activePlants[i];
            if (cell == null || !cell.HasPlant)
            {
                activePlants.RemoveAt(i);
                continue;
            }

            bool stageChanged = cell.Plant.Tick(Time.deltaTime);
            if (stageChanged)
            {
                UpdateVisual(cell);
                // Wässerung hat sich zurückgesetzt — Tile wieder auf Tilled
                if (!cell.Plant.IsFullyGrown)
                    cell.TileVisual?.SetState(FarmTileState.Tilled);

                if (FarmSaveManager.Instance != null)
                    FarmSaveManager.Instance.RequestSave();
            }
        }
    }

    // --- Visuals ---

    private void SpawnVisual(GridCell cell)
    {
        if (cell == null || !cell.HasPlant) return;

        RemoveVisual(cell);

        var prefab = cell.Plant.GetCurrentPrefab();
        if (prefab == null) return;

        var pos = GridManager.Instance.GridToWorld(cell.X, cell.Z);
        var go = Instantiate(prefab, pos, Quaternion.identity);
        plantVisuals[cell] = go;
    }

    private void UpdateVisual(GridCell cell)
    {
        RemoveVisual(cell);
        SpawnVisual(cell);
    }

    private void RemoveVisual(GridCell cell)
    {
        if (cell == null) return;

        if (plantVisuals.TryGetValue(cell, out var go))
        {
            if (go != null)
                Destroy(go);

            plantVisuals.Remove(cell);
        }
    }
}

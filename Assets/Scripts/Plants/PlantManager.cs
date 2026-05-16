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

        activePlants.Add(cell);
        SpawnVisual(cell);
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
        return true;
    }

    // --- Wachstum ---

    private void TickGrowth()
    {
        foreach (var cell in activePlants)
        {
            if (!cell.HasPlant) continue;

            bool stageChanged = cell.Plant.Tick(Time.deltaTime);
            if (stageChanged)
            {
                UpdateVisual(cell);
                // Wässerung hat sich zurückgesetzt — Tile wieder auf Tilled
                if (!cell.Plant.IsFullyGrown)
                    cell.TileVisual?.SetState(FarmTileState.Tilled);
            }
        }
    }

    // --- Visuals ---

    private void SpawnVisual(GridCell cell)
    {
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
        if (plantVisuals.TryGetValue(cell, out var go))
        {
            Destroy(go);
            plantVisuals.Remove(cell);
        }
    }
}

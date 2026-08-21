using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kümmert sich um den kompletten Pflanzen-Gameloop:
/// Erde lockern → Samen pflanzen → Gießen → Wachsen → Ernten
/// </summary>
public class PlantManager : MonoBehaviour
{
    public static PlantManager Instance { get; private set; }

    // --- Statische Events für Mission-System ---
    public static event Action OnFieldTilled;
    public static event Action<PlantType> OnSeedPlanted;
    public static event Action<PlantType> OnPlantWatered;
    public static event Action<PlantType> OnCropHarvested;

    // --- Wachstums-Events (gefeuert von TickGrowth) ---
    /// <summary>Eine Pflanze hat eine Wachstumsstufe erreicht, ist aber noch nicht erntereif.</summary>
    public static event Action<PlantType> OnPlantGrew;
    /// <summary>Eine Pflanze ist vollständig gewachsen und erntereif.</summary>
    public static event Action<PlantType> OnPlantFullyGrown;

    // Alle aktiven Pflanzen mit ihrer Zelle
    private readonly Dictionary<GridCell, GameObject> plantVisuals = new();
    private readonly List<GridCell> activePlants = new();

    /// <summary>
    /// Alle Zellen mit einer wachsenden Pflanze. Einstiegspunkt für alles was pro Pflanze
    /// etwas anzeigen will (PlantStatusOverlay) — ohne dass jemand das Grid absuchen muss.
    /// </summary>
    public IReadOnlyList<GridCell> ActivePlants => activePlants;

    /// <summary>Das gespawnte Visual dieser Pflanze, oder null.</summary>
    public GameObject GetPlantVisual(GridCell cell) =>
        cell != null && plantVisuals.TryGetValue(cell, out var go) ? go : null;

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

        OnFieldTilled?.Invoke();
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

        OnSeedPlanted?.Invoke(type);
        return true;
    }

    /// <summary>Gießkanne: Pflanze auf einer Zelle gießen.</summary>
    public bool TryWater(int x, int z)
    {
        var cell = GridManager.Instance.GetCell(x, z);
        if (cell == null || cell.IsLocked || !cell.HasPlant) return false;

        // Ohne diese Prüfung ließ sich Gießen endlos spammen: Water() selbst ist zwar
        // gedeckelt, TryWater meldete aber trotzdem Erfolg — das löste jedes Mal ein
        // OnPlantWatered aus (Missions-Fortschritt gratis) und einen Save.
        if (!cell.Plant.NeedsWatering) return false;

        var wateredType = cell.Plant.Type;

        // Gießkraft kommt aus dem Werkzeug-Level: ab dem entsprechenden Meilenstein zählt
        // ein Einsatz mehrfach, damit Pflanzen mit hohem Gießbedarf nicht dauerhaft zwei
        // Durchgänge über dasselbe Feld verlangen.
        int power = ToolRegistry.Instance != null
            ? ToolRegistry.Instance.GetWateringPower(ToolType.WateringCan)
            : 1;

        cell.Plant.Water(power);

        if (cell.Plant.WateringsThisStage >= cell.Plant.Type.wateringsPerStage)
            cell.TileVisual?.SetState(FarmTileState.Watered);

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();

        OnPlantWatered?.Invoke(wateredType);
        return true;
    }

    /// <summary>Sichel: Reife Pflanze ernten. yieldBonus = zusätzliche Crops durch Upgrades.</summary>
    public bool TryHarvest(int x, int z, int yieldBonus = 0)
    {
        var cell = GridManager.Instance.GetCell(x, z);
        if (cell == null || cell.IsLocked || !cell.HasPlant) return false;
        if (!cell.Plant.IsFullyGrown) return false;

        var harvested = cell.Harvest();
        PlayerInventory.Instance.AddCrop(harvested.Type, 1 + yieldBonus);

        cell.TileVisual?.SetState(FarmTileState.Dry);
        PlayHarvestVisual(cell);
        activePlants.Remove(cell);

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();

        OnCropHarvested?.Invoke(harvested.Type);
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

        for (int x = grid.MinX; x < grid.MaxXExclusive; x++)
        {
            for (int z = grid.MinZ; z < grid.MaxZExclusive; z++)
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

                if (cell.Plant.IsFullyGrown)
                {
                    OnPlantFullyGrown?.Invoke(cell.Plant.Type);
                }
                else
                {
                    // Wässerung hat sich zurückgesetzt — Tile wieder auf Tilled
                    cell.TileVisual?.SetState(FarmTileState.Tilled);
                    OnPlantGrew?.Invoke(cell.Plant.Type);
                }

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
        var rotation = Quaternion.Euler(0f, cell.Plant.Type.modelYRotation, 0f);
        var go = Instantiate(prefab, pos, rotation);

        // Feel-Good-Polish: Pop-In-Animation beim Spawnen/Stage-Wechsel (siehe PlantGrowthFx.Start()).
        if (!go.TryGetComponent<PlantGrowthFx>(out _))
            go.AddComponent<PlantGrowthFx>();

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

    /// <summary>
    /// Entfernt das Pflanzen-Visual bei der Ernte: die Crop fliegt in die Scheune
    /// (siehe PlantGrowthFx.PlayHarvestFlyTo()). Ist keine Scheune in der Szene,
    /// schrumpft sie an Ort und Stelle weg.
    /// </summary>
    private void PlayHarvestVisual(GridCell cell)
    {
        if (cell == null) return;

        if (plantVisuals.TryGetValue(cell, out var go) && go != null)
        {
            if (go.TryGetComponent<PlantGrowthFx>(out var fx))
            {
                if (BarnInteraction.Instance != null)
                    fx.PlayHarvestFlyTo(BarnInteraction.Instance.CollectPoint);
                else
                    fx.PlayHarvestAndDestroy();
            }
            else
            {
                Destroy(go); // Fallback, falls das Visual doch mal ohne Fx-Component unterwegs ist
            }

            plantVisuals.Remove(cell);
        }
    }
}

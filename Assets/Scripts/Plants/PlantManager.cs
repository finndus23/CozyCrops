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

    /// <summary>Sichel auf Gras hat einen Not-Samen gefunden — für Sound/Feedback. Position ist die Welt-Position der Tile.</summary>
    public static event Action<PlantType, Vector3> OnStarterSeedFound;

    /// <summary>Dünger-RNG hat bei der Ernte zugeschlagen (siehe RollFertilizedHarvestBonus) —
    /// für visuelles Feedback. Position ist die Welt-Position der geernteten Tile.</summary>
    public static event Action<PlantType, Vector3> OnFertilizedHarvestBonus;

    /// <summary>Sichel-RNG hat bei der Ernte zugeschlagen (siehe RollScytheYieldBonus) — für
    /// visuelles Feedback. Unabhängig vom Dünger-Bonus, kann bei derselben Ernte zusätzlich
    /// auslösen. Position ist die Welt-Position der geernteten Tile.</summary>
    public static event Action<PlantType, Vector3> OnScytheYieldBonus;

    [Header("Anti-Softlock: Not-Samen im Gras")]
    [Tooltip("Grundchance pro Sichel-Schlag auf einer Gras-Zelle (nicht Ackerland), einen " +
             "Not-Samen zu finden — auch wenn der Spieler nicht klamm ist. Bewusst niedrig: " +
             "das ist ein netter Nebeneffekt beim Rumlaufen, keine zweite Einnahmequelle.")]
    [Range(0f, 1f)]
    [SerializeField] private float grassSeedChance = 0.08f;

    [Tooltip("Startchance, sobald der Spieler wirklich feststeckt: 0 Gold UND 0 Samen jeder " +
             "Art. Deutlich höher als die Grundchance, damit die Rettung nicht selbst am " +
             "Zufall hängenbleibt.")]
    [Range(0f, 1f)]
    [SerializeField] private float rescueSeedChance = 0.3f;

    [Tooltip("Wie stark die Chance pro erfolglosem Versuch steigt, solange der Spieler " +
             "feststeckt (0.3 → 0.55 → 0.8 → 1.0 bei 0.25). Garantiert eine Rettung nach " +
             "wenigen Schlägen statt theoretisch endlos Pech haben zu können.")]
    [Range(0f, 1f)]
    [SerializeField] private float rescueSeedChanceRamp = 0.25f;

    [Tooltip("Save-ID des Samens, der im Gras gefunden wird (siehe PlantType.saveId) — " +
             "sinnvollerweise der günstigste, zum Wiedereinstieg in die Wirtschaft.")]
    [SerializeField] private string starterSeedId = "carrot";

    [Header("Dünger")]
    [Tooltip("Wachstums-Multiplikator auf gedüngten Tiles (GridCell.IsFertilized). 1.5 = " +
             "50% schnelleres Wachstum. Gilt für den kompletten Anbauzyklus bis zur Ernte, " +
             "danach setzt Harvest() den Dünger zurück (siehe GridCell.IsFertilized).")]
    [Range(1f, 2f)]
    [SerializeField] private float fertilizedGrowthMultiplier = 1.5f;

    [Tooltip("Zusätzliche Ernte-Items beim Ernten eines GEDÜNGTEN Feldes, WENN der RNG-Wurf " +
             "trifft (siehe ToolData.GetFertilizedHarvestChance — skaliert mit dem Level des " +
             "Dünger-Werkzeugs, 20% auf Level 0 bis 50% auf Höchstlevel). Kein garantierter " +
             "Bonus mehr, damit sich das Aufwerten lohnt statt jede Ernte planbar zu machen.")]
    [SerializeField] private int fertilizedHarvestBonus = 1;

    [Header("Sichel-Ernte-Bonus")]
    [Tooltip("Zusätzliche Ernte-Items bei JEDER Ernte, WENN der Sichel-RNG-Wurf trifft " +
             "(siehe ToolData.GetYieldBonusChance — skaliert mit dem Sichel-Level, 0% auf " +
             "Level 0 bis 30% auf Höchstlevel). Ersetzt den alten garantierten " +
             "Meilenstein-Bonus, unabhängig vom Dünger-Bonus oben — beide können bei " +
             "derselben Ernte zusammen auslösen.")]
    [SerializeField] private int scytheYieldBonusAmount = 1;

    // Zählt erfolglose Versuche NUR während der Spieler feststeckt. Reset sobald er es
    // nicht mehr tut oder ein Fund gelingt — sonst würde die Chance sich über die ganze
    // Spielzeit aufsummieren und irgendwann jeden Grasschlag garantiert auslösen.
    private int consecutiveMissesWhileStuck;

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

    /// <summary>
    /// Dünger: macht Wachstum (TickGrowth) und Feldarbeit (ToolUseHandler) auf dieser Tile
    /// schneller, bis zur nächsten Ernte (siehe GridCell.IsFertilized).
    /// </summary>
    public bool TryFertilize(int x, int z)
    {
        var cell = GridManager.Instance.GetCell(x, z);
        if (cell == null || cell.IsLocked) return false;
        if (cell.Type != TileType.FarmPlot || cell.IsFertilized) return false;

        if (!PlayerInventory.Instance.TrySpendFertilizer(1)) return false;

        cell.IsFertilized = true;

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

        // Vor Harvest() abfragen — die setzt IsFertilized als Teil des Zurücksetzens
        // bereits auf false.
        bool wasFertilized = cell.IsFertilized;

        var harvested = cell.Harvest();

        int totalYield = 1 + yieldBonus;
        Vector3 worldPos = GridManager.Instance.GridToWorld(x, z);

        if (wasFertilized && RollFertilizedHarvestBonus())
        {
            totalYield += fertilizedHarvestBonus;
            OnFertilizedHarvestBonus?.Invoke(harvested.Type, worldPos);
        }

        // Unabhängig vom Dünger-Bonus oben — beide RNG-Würfe laufen getrennt und können bei
        // derselben Ernte zusammen zuschlagen.
        if (RollScytheYieldBonus())
        {
            totalYield += scytheYieldBonusAmount;
            OnScytheYieldBonus?.Invoke(harvested.Type, worldPos);
        }

        PlayerInventory.Instance.AddCrop(harvested.Type, totalYield);

        cell.TileVisual?.SetState(FarmTileState.Dry);
        PlayHarvestVisual(cell);
        activePlants.Remove(cell);

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();

        OnCropHarvested?.Invoke(harvested.Type);
        return true;
    }

    /// <summary>
    /// Würfelt, ob die Ernte eines gedüngten Feldes den Extra-Crop abwirft. Die Chance
    /// kommt vom LEVEL DES DÜNGER-WERKZEUGS (nicht der Sichel) — Aufwerten des Fertilize-
    /// Tools macht den Dünger selbst potenter, nicht nur die Fläche/Warteschlange größer.
    /// </summary>
    private static bool RollFertilizedHarvestBonus()
    {
        var data = ToolRegistry.Instance?.GetData(ToolType.Fertilize);
        if (data == null) return false;

        int level = ToolRegistry.Instance.GetLevel(ToolType.Fertilize);
        float chance = data.GetFertilizedHarvestChance(level);
        return UnityEngine.Random.value < chance;
    }

    /// <summary>
    /// Würfelt den generellen Sichel-Ernte-Bonus — läuft bei JEDER Ernte, nicht nur
    /// gedüngten Feldern. Chance kommt vom SICHEL-Level (0% Level 0 → 30% Level 30),
    /// ersetzt den alten garantierten Meilenstein-Ertragsbonus.
    /// </summary>
    private static bool RollScytheYieldBonus()
    {
        var data = ToolRegistry.Instance?.GetData(ToolType.Scythe);
        if (data == null) return false;

        int level = ToolRegistry.Instance.GetLevel(ToolType.Scythe);
        float chance = data.GetYieldBonusChance(level);
        return UnityEngine.Random.value < chance;
    }

    /// <summary>
    /// Sichel auf Gras statt auf einer reifen Pflanze: kein Ertrag, aber eine Chance auf
    /// einen Not-Samen. Gedacht als Rettungsanker gegen den Softlock "kein Geld, keine
    /// Samen, keine Einnahmequelle mehr" — nicht als zweite Einkommensquelle, deshalb die
    /// niedrige Grundchance und der harte Cut bei <see cref="rescueSeedChance"/> nur für
    /// den echten Notfall.
    ///
    /// Gibt <c>true</c> zurück sobald gemäht wurde, unabhängig vom Fund — der Tile-Impact
    /// ist das Feedback, dass der Schlag etwas getan hat, nicht dass er einen Samen gab.
    /// </summary>
    public bool TryGatherGrass(int x, int z)
    {
        var cell = GridManager.Instance.GetCell(x, z);
        if (cell == null || cell.IsLocked) return false;
        if (cell.Type != TileType.Grass || cell.HasPlant) return false;

        RollStarterSeed(x, z);
        return true;
    }

    private void RollStarterSeed(int x, int z)
    {
        if (PlantDatabase.Instance == null || PlayerInventory.Instance == null) return;

        PlantType starter = PlantDatabase.Instance.GetById(starterSeedId);
        if (starter == null) return;

        bool stuck = IsSoftlocked(starter);
        float chance = stuck
            ? Mathf.Min(1f, rescueSeedChance + consecutiveMissesWhileStuck * rescueSeedChanceRamp)
            : grassSeedChance;

        if (UnityEngine.Random.value > chance)
        {
            if (stuck) consecutiveMissesWhileStuck++;
            return;
        }

        consecutiveMissesWhileStuck = 0;

        PlayerInventory.Instance.AddSeed(starter, 1);
        OnStarterSeedFound?.Invoke(starter, GridManager.Instance.GridToWorld(x, z));
    }

    /// <summary>
    /// Kein Geld für auch nur den günstigsten Samen UND keiner mehr im Inventar —
    /// dann kommt der Spieler ohne Hilfe von außen nicht mehr an neue Saat.
    /// </summary>
    private bool IsSoftlocked(PlantType starter)
    {
        if (PlayerInventory.Instance.Money >= starter.seedPrice) return false;

        foreach (var kvp in PlayerInventory.Instance.GetAllSeeds())
            if (kvp.Value > 0) return false;

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

            // Sorten-eigenes Tempo (PlantType.growthSpeedMultiplier) UND Dünger-Bonus
            // wirken multiplikativ zusammen — eine gedüngte Karotte ist also nicht nur
            // schneller als eine ungedüngte Karotte, sondern auch schneller als ein
            // gedüngter Blumenkohl.
            float dt = Time.deltaTime * Mathf.Max(0.05f, cell.Plant.Type.growthSpeedMultiplier);
            if (cell.IsFertilized) dt *= fertilizedGrowthMultiplier;

            bool stageChanged = cell.Plant.Tick(dt);
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

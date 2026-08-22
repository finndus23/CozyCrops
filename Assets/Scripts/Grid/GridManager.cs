using System;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    /// <summary>
    /// Tiles wurden umgewandelt. Liefert die neue Art, den Mittelpunkt der Fläche und die
    /// Anzahl geänderter Felder.
    ///
    /// Feuert <b>einmal pro Aktion</b>, nicht pro Tile — bei einer Auswahl über zwanzig
    /// Felder käme sonst zwanzigmal derselbe Klang im selben Frame. Aus demselben Grund
    /// bleibt <see cref="ApplySaveTiles"/> bewusst still: beim Laden eines Spielstands
    /// entstehen hunderte Tiles auf einmal.
    /// </summary>
    public static event Action<TileType, Vector3, int> OnTilesAppliedStatic;

    [SerializeField] private int width = 20;
    [SerializeField] private int height = 20;
    [SerializeField] private float cellSize = 1f;

    [Header("Farm-Erweiterung")]
    [Tooltip("Tiefe der fünf Erweiterungsfelder im 3x2-Layout.")]
    [Min(0)]
    [SerializeField] private int expansionSize = 13;

    [Header("Persistenter Dekorationsrand")]
    [Tooltip("Reine Kulissenfläche außerhalb der Farm. Sie besitzt keine interaktiven Grid-Zellen.")]
    [Min(0)]
    [SerializeField] private int decorationBorderSize = 180;

    [SerializeField] private GameObject grassTilePrefab;
    [SerializeField] private GameObject farmPlotPrefab;
    [SerializeField] private GameObject pathTilePrefab;
    [SerializeField] private GameObject borderTilePrefab;

    [Header("Grass-Tile Kleindeko")]
    [Tooltip("Aktiviert kleine Laufzeit-Dekoration auf den bearbeitbaren Farm-Grass-Tiles.")]
    [SerializeField] private bool enableGrassTileDecoration = false;
    [Tooltip("Kleine Blumen und Graeser, die nur auf Grass-Tiles leben.")]
    [SerializeField] private GameObject[] grassPlantDecorationPrefabs;
    [Tooltip("Deterministischer Seed: Dasselbe Grid-Tile erhaelt nach dem Laden dieselbe Dekoration.")]
    [SerializeField] private int grassDecorationSeed = 7319;
    [Min(0f)]
    [SerializeField] private float grassDecorationSurfaceOffset = 0.055f;
    [Min(0.01f)]
    [SerializeField] private float grassPlantDecorationMinScale = 0.55f;
    [Min(0.01f)]
    [SerializeField] private float grassPlantDecorationMaxScale = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] private float grassTileDecorationChance = 0.12f;
    [Range(0f, 1f)]
    [SerializeField] private float secondGrassDecorationChance = 0.04f;
    [Tooltip("Welt-X/Z-Rechtecke, in denen keine Grass-Tile-Deko erscheinen darf (Gebaeude, Zufahrt usw.).")]
    [SerializeField] private Rect[] grassDecorationBlockedAreas;

    [Header("Feel-Good")]
    [Tooltip("Radius in Tiles, in dem Nachbarn beim Umwandeln mithüpfen. 0 = aus.")]
    [SerializeField] private int tileRippleRadius = 2;
    [Tooltip("Zusätzliche Verzögerung pro Tile Abstand — erzeugt die auslaufende Welle.")]
    [SerializeField] private float tileRippleDelayPerTile = 0.035f;

    private GridCell[,] cells;
    private GameObject[,] tileObjects;

    /// <summary>
    /// Unterdrückt Tile-Animationen. Muss beim Aufbau des Grids und beim Laden eines
    /// Spielstands gesetzt sein — sonst fährt beim Spielstart das komplette Feld hoch.
    /// </summary>
    private bool suppressTileFx;

    // Das 3x2-Layout ist passend zur isometrischen Kamera gedreht: Auf der +X-Seite
    // steht das Farmhaus, dort gibt es bewusst KEINE Erweiterung. Stattdessen wächst
    // die Farm nach -X sowie in beide Z-Richtungen.
    public int Width => MaxXExclusive - MinX;
    public int Height => MaxZExclusive - MinZ;
    public int BaseWidth => width;
    public int BaseHeight => height;
    public int ExpansionSize => expansionSize;
    public int DecorationBorderSize => decorationBorderSize;

    /// <summary>Äußere Grenzen der vollständig ausgebauten Farm – ohne Dekorationsrand.</summary>
    public int FarmMinX => -expansionSize;
    public int FarmMinZ => -expansionSize;
    public int FarmMaxXExclusive => width;
    public int FarmMaxZExclusive => height + expansionSize;

    /// <summary>Das logische Grid endet an der vollständig ausgebauten Farm.</summary>
    public int MinX => FarmMinX;
    public int MinZ => FarmMinZ;
    public int MaxXExclusive => FarmMaxXExclusive;
    public int MaxZExclusive => FarmMaxZExclusive;

    /// <summary>Äußere Grenzen der nicht interaktiven Landschaftskulisse.</summary>
    public int EnvironmentMinX => FarmMinX - decorationBorderSize;
    public int EnvironmentMinZ => FarmMinZ - decorationBorderSize;
    public int EnvironmentMaxXExclusive => FarmMaxXExclusive + decorationBorderSize;
    public int EnvironmentMaxZExclusive => FarmMaxZExclusive + decorationBorderSize;
    public float CellSize => cellSize;

    void Awake()
    {
        Instance = this;

        // Wenn bereits Kinder vorhanden sind → vorgebautes Grid laden
        if (transform.childCount > 0)
            LoadPrebuiltTiles();
        else
            InitializeGrid();

        SpawnPersistentDecorationGround();
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

        AllocateGridArrays();

        for (int x = MinX; x < MaxXExclusive; x++)
        {
            for (int z = MinZ; z < MaxZExclusive; z++)
            {
                if (!IsInBounds(x, z)) continue;
                SetCell(x, z, new GridCell(x, z));
                SpawnTile(x, z, grassTilePrefab, TileType.Grass);
            }
        }

        if (expansionSize == 0)
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
        AllocateGridArrays();

        // Leere GridCells anlegen
        for (int x = MinX; x < MaxXExclusive; x++)
            for (int z = MinZ; z < MaxZExclusive; z++)
            {
                if (!IsInBounds(x, z)) continue;
                SetCell(x, z, new GridCell(x, z));
            }

        // Vorhandene Scene-Kinder auf Grid-Koordinaten mappen
        foreach (Transform child in transform)
        {
            if (!WorldToGrid(child.position, out int x, out int z)) continue;

            var marker = child.GetComponent<TileMarker>();
            if (marker == null) continue; // Border-/Hilfsobjekte sind keine nutzbaren Tiles.

            SetTileObject(x, z, child.gameObject);
            GridCell cell = GetCell(x, z);
            cell.TileVisual = child.GetComponent<FarmTileVisual>();
            cell.Type = marker.tileType;
            AddGrassTileDecoration(child.gameObject, x, z, cell.Type);
        }

        // Die bestehende Scene enthält nur die ursprüngliche Farm. Die fünf Felder
        // des äußeren 3x2-Layouts werden deshalb als echte Gras-Tiles ergänzt.
        for (int x = MinX; x < MaxXExclusive; x++)
        {
            for (int z = MinZ; z < MaxZExclusive; z++)
            {
                if (!IsInBounds(x, z)) continue;
                if (GetTileObject(x, z) == null)
                {
                    SpawnTile(x, z, grassTilePrefab, TileType.Grass);
                }
            }
        }

        Debug.Log($"[GridManager] {transform.childCount} vorgebaute Tiles geladen.");
    }

    // ──────────────────────────────────────────────
    // Runtime: Grid zur Laufzeit neu generieren (Fallback)
    // ──────────────────────────────────────────────

    private void InitializeGrid()
    {
        AllocateGridArrays();

        for (int x = MinX; x < MaxXExclusive; x++)
        {
            for (int z = MinZ; z < MaxZExclusive; z++)
            {
                if (!IsInBounds(x, z)) continue;
                SetCell(x, z, new GridCell(x, z));
                SpawnTile(x, z, grassTilePrefab, TileType.Grass);
            }
        }

        if (expansionSize == 0)
            SpawnBorderRing();
    }

    // ──────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────

    public bool TryPlaceTile(int x, int z, TileType type)
    {
        if (!IsPlayerEditable(x, z)) return false;
        GridCell cell = GetCell(x, z);
        if (cell.IsLocked) return false;
        if (cell.HasPlant) return false;
        if (type == TileType.Grass) return false;
        if (cell.Type == type) return false;

        cell.Type = type;
        cell.IsTilled = false;
        cell.IsFertilized = false;
        cell.ClearLoadedPlant();

        ReplaceTile(x, z, GetPrefabForType(type), type);
        return true;
    }

    public bool TryRemoveTile(int x, int z)
    {
        if (!IsPlayerEditable(x, z)) return false;
        GridCell cell = GetCell(x, z);
        if (cell.IsLocked) return false;
        if (cell.HasPlant) return false;
        if (cell.Type == TileType.Grass) return false;

        cell.Type = TileType.Grass;
        cell.IsTilled = false;
        cell.IsFertilized = false;
        cell.ClearLoadedPlant();

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

        if (changed)
            OnTilesAppliedStatic?.Invoke(type, GridToWorld(x, z), 1);

        return changed;
    }

    public void ApplyToSelection(TileType type)
    {
        bool changed = false;

        // Mittelpunkt der tatsächlich geänderten Felder aufsummieren. Über die gesamte
        // Auswahl zu mitteln wäre falsch: gesperrte oder bepflanzte Zellen ändern sich
        // nicht, und der Ton käme dann aus einer Ecke, in der nichts passiert ist.
        var sum = Vector3.zero;
        int count = 0;

        foreach (var cell in SelectionManager.Instance.SelectedCells)
        {
            bool success;

            if (type == TileType.Grass)
                success = TryRemoveTile(cell.x, cell.y);
            else
                success = TryPlaceTile(cell.x, cell.y, type);

            if (!success) continue;

            changed = true;
            sum += GridToWorld(cell.x, cell.y);
            count++;
        }

        SelectionManager.Instance.ClearSelection();

        if (changed && FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();

        if (count > 0)
            OnTilesAppliedStatic?.Invoke(type, sum / count, count);
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

        // Ein Save kann hunderte Tiles neu setzen — ohne das würde beim Laden
        // das ganze Feld gleichzeitig hochfahren und rippeln.
        suppressTileFx = true;

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

        suppressTileFx = false;

        // Nach dem Wiederherstellen aller Cell-Daten baut der PlantManager seine interne Liste neu auf.
        PlantManager.Instance?.RebuildLoadedPlantsFromGrid();

        Debug.Log($"[GridManager] Save angewendet. Applied={appliedCount}, Skipped={skippedCount}, PlantsInSave={loadedPlantCount}");
    }

    public GridCell GetCell(int x, int z) => IsInBounds(x, z) ? cells[ToArrayX(x), ToArrayZ(z)] : null;
    public GameObject GetTileObject(int x, int z) => IsInBounds(x, z) ? tileObjects[ToArrayX(x), ToArrayZ(z)] : null;

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

    public bool IsInBounds(int x, int z)
    {
        return x >= MinX && x < MaxXExclusive && z >= MinZ && z < MaxZExclusive;
    }

    /// <summary>
    /// True für Koordinaten der sichtbaren Kulisse außerhalb des logischen Farm-Grids.
    /// </summary>
    public bool IsDecorationArea(int x, int z)
    {
        return IsInEnvironmentBounds(x, z) && !IsInBounds(x, z);
    }

    public bool IsInEnvironmentBounds(int x, int z) =>
        x >= EnvironmentMinX && x < EnvironmentMaxXExclusive &&
        z >= EnvironmentMinZ && z < EnvironmentMaxZExclusive;

    /// <summary>
    /// Nur Startzone und kaufbare Erweiterungen sind durch Spielerwerkzeuge bearbeitbar.
    /// Die äußere Landschaft besitzt absichtlich gar keine Grid-Zellen und kann daher
    /// ausschließlich im Unity-Editor dekoriert werden.
    /// </summary>
    public bool IsPlayerEditable(int x, int z) =>
        IsInBounds(x, z);

    // ──────────────────────────────────────────────
    // Save-/Load-Hilfen
    // ──────────────────────────────────────────────

    private void ApplySingleSavedTile(TileSaveData tileData)
    {
        GridCell cell = GetCell(tileData.x, tileData.z);

        TileType loadedType = ParseTileType(tileData.tileType);
        TileType previousType = cell.Type;

        cell.Type = loadedType;
        // Der Sperrzustand wird aus den gespeicherten Zonen abgeleitet. Alte Saves
        // enthalten noch Locks der früheren Innenzonen und dürfen die neue Startfarm
        // deshalb nicht erneut blockieren.
        cell.IsTilled = loadedType == TileType.FarmPlot && tileData.isTilled;
        cell.IsFertilized = loadedType == TileType.FarmPlot && tileData.isFertilized;
        cell.ClearLoadedPlant();

        if (GetTileObject(tileData.x, tileData.z) == null || previousType != loadedType)
            ReplaceTile(tileData.x, tileData.z, GetPrefabForType(loadedType), loadedType);
        else
            EnsureMarker(GetTileObject(tileData.x, tileData.z), loadedType);

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

    private void AllocateGridArrays()
    {
        cells = new GridCell[Width, Height];
        tileObjects = new GameObject[Width, Height];
    }

    private int ToArrayX(int x) => x - MinX;
    private int ToArrayZ(int z) => z - MinZ;

    private void SetCell(int x, int z, GridCell cell) =>
        cells[ToArrayX(x), ToArrayZ(z)] = cell;

    private void SetTileObject(int x, int z, GameObject tileObject) =>
        tileObjects[ToArrayX(x), ToArrayZ(z)] = tileObject;

    private void SpawnTile(int x, int z, GameObject prefab, TileType type)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[GridManager] Kein Prefab für TileType {type} bei {x},{z} gesetzt.");
            return;
        }

        var go = Instantiate(prefab, GridToWorld(x, z), Quaternion.identity, transform);
        SetTileObject(x, z, go);
        GridCell cell = GetCell(x, z);
        cell.Type = type;
        cell.TileVisual = go.GetComponent<FarmTileVisual>();

        EnsureMarker(go, type);
        AddGrassTileDecoration(go, x, z, type);
    }

    /// <summary>
    /// Erzeugt auf einem Teil der Grass-Tiles ein, selten zwei, kleine Blumen oder
    /// Graeser. Beim Ersetzen des Tiles verschwinden sie automatisch mit dem alten
    /// GameObject; wird wieder Gras gebaut, erzeugt SpawnTile sie erneut.
    /// </summary>
    private void AddGrassTileDecoration(GameObject tileObject, int x, int z, TileType type)
    {
        const string rootName = "Grass Tile Misc Decoration";

        if (!enableGrassTileDecoration || !Application.isPlaying || tileObject == null || type != TileType.Grass)
            return;
        if (grassPlantDecorationPrefabs == null || grassPlantDecorationPrefabs.Length == 0)
            return;
        if (IsGrassDecorationBlocked(GridToWorld(x, z)))
            return;
        if (tileObject.transform.Find(rootName) != null)
            return;

        var random = new System.Random(GetGrassDecorationSeed(x, z));
        if (random.NextDouble() >= grassTileDecorationChance)
            return;

        int decorationCount = 1;
        if (random.NextDouble() < secondGrassDecorationChance) decorationCount++;

        var root = new GameObject(rootName);
        root.transform.SetParent(tileObject.transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;

        // Vorgebaute Gras-Tiles sind in Y sehr flach skaliert. Der inverse Faktor sorgt
        // dafuer, dass Blumen und Graeser trotzdem ihre echte, gleichmaessige Form behalten.
        Vector3 tileWorldScale = tileObject.transform.lossyScale;
        root.transform.localScale = new Vector3(
            SafeInverseScale(tileWorldScale.x),
            SafeInverseScale(tileWorldScale.y),
            SafeInverseScale(tileWorldScale.z));

        float baseAngle = NextFloat(random, 0f, Mathf.PI * 2f);
        for (int i = 0; i < decorationCount; i++)
        {
            GameObject prefab = PickGrassDecorationPrefab(
                grassPlantDecorationPrefabs,
                random);
            if (prefab == null) continue;

            float angle = baseAngle + Mathf.PI * 2f * i / decorationCount +
                          NextFloat(random, -0.3f, 0.3f);
            float radius = NextFloat(random, cellSize * 0.1f, cellSize * 0.34f);
            float scale = NextFloat(random,
                Mathf.Min(grassPlantDecorationMinScale, grassPlantDecorationMaxScale),
                Mathf.Max(grassPlantDecorationMinScale, grassPlantDecorationMaxScale));

            GameObject decoration = Instantiate(prefab, root.transform);
            decoration.name = $"Grass Misc {i + 1}";
            decoration.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * radius,
                grassDecorationSurfaceOffset,
                Mathf.Sin(angle) * radius);
            decoration.transform.localRotation = Quaternion.Euler(
                0f, NextFloat(random, 0f, 360f), 0f);
            decoration.transform.localScale = prefab.transform.localScale * scale;

            // Kleindeko darf weder Tile-Auswahl noch Werkzeuge oder Physik blockieren.
            foreach (Collider decorationCollider in decoration.GetComponentsInChildren<Collider>(true))
                decorationCollider.enabled = false;

            // Tausende sehr kleine Schatten waeren teuer und visuell kaum wahrnehmbar.
            foreach (Renderer decorationRenderer in decoration.GetComponentsInChildren<Renderer>(true))
                decorationRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private bool IsGrassDecorationBlocked(Vector3 worldPosition)
    {
        if (grassDecorationBlockedAreas == null)
            return false;

        Vector2 worldXZ = new Vector2(worldPosition.x, worldPosition.z);
        foreach (Rect blockedArea in grassDecorationBlockedAreas)
        {
            if (blockedArea.Contains(worldXZ))
                return true;
        }

        return false;
    }

    private static GameObject PickGrassDecorationPrefab(
        GameObject[] prefabs,
        System.Random random)
    {
        if (prefabs == null || prefabs.Length == 0)
            return null;

        for (int attempt = 0; attempt < prefabs.Length; attempt++)
        {
            GameObject prefab = prefabs[random.Next(prefabs.Length)];
            if (prefab != null) return prefab;
        }

        return null;
    }

    private int GetGrassDecorationSeed(int x, int z)
    {
        unchecked
        {
            int hash = grassDecorationSeed;
            hash = hash * 397 ^ x;
            hash = hash * 397 ^ z;
            return hash;
        }
    }

    private static float NextFloat(System.Random random, float min, float max) =>
        min + (max - min) * (float)random.NextDouble();

    private static float SafeInverseScale(float value) =>
        Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;

    private void ReplaceTile(int x, int z, GameObject prefab, TileType type)
    {
        GameObject oldTile = GetTileObject(x, z);
        if (oldTile != null)
        {
            // Sofort ausblenden, weil Destroy() erst am Frame-Ende wirklich löscht.
            // Sonst kann beim F6-Test kurz das alte Default-Tile über dem geladenen Tile liegen.
            oldTile.SetActive(false);
            Destroy(oldTile);
            SetTileObject(x, z, null);
        }

        GetCell(x, z).TileVisual = null;

        SpawnTile(x, z, prefab, type);

        var newTile = GetTileObject(x, z);
        if (newTile == null) return;

        // Beim Laden eines Spielstands laufen hier hunderte Tiles durch — die dürfen
        // nicht alle gleichzeitig hochfahren.
        if (suppressTileFx)
            return;

        TileConvertFx.Ensure(newTile)?.PlayConvert(0f);
        RippleNeighbours(x, z);
    }

    /// <summary>
    /// Lässt die umliegenden Tiles zeitversetzt kurz mithüpfen.
    /// Die Verzögerung wächst mit der Distanz — dadurch läuft eine Welle nach außen
    /// statt dass alles gleichzeitig zuckt.
    /// </summary>
    private void RippleNeighbours(int x, int z)
    {
        if (tileRippleRadius <= 0) return;

        for (int dx = -tileRippleRadius; dx <= tileRippleRadius; dx++)
        {
            for (int dz = -tileRippleRadius; dz <= tileRippleRadius; dz++)
            {
                if (dx == 0 && dz == 0) continue;

                int nx = x + dx;
                int nz = z + dz;
                if (!IsInBounds(nx, nz)) continue;

                // Kreisförmig statt quadratisch — eine quadratische Welle sieht
                // auf einem Grid sofort nach Raster aus.
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance > tileRippleRadius) continue;

                var neighbour = GetTileObject(nx, nz);
                if (neighbour == null) continue;

                TileConvertFx.Ensure(neighbour)?.PlayNudge(distance * tileRippleDelayPerTile);
            }
        }
    }

    /// <summary>
    /// Treffer-Feedback wenn ein Tool auf einer Tile fertig wird (Hacken, Gießen, Ernten).
    /// Wird vom ToolUseHandler aufgerufen.
    /// </summary>
    public void PlayTileImpact(int x, int z)
    {
        if (suppressTileFx || !IsInBounds(x, z)) return;

        var tile = GetTileObject(x, z);
        if (tile == null) return;

        TileConvertFx.Ensure(tile)?.PlayNudge(0f);
    }

    private void EnsureMarker(GameObject go, TileType type)
    {
        if (go == null) return;

        var marker = go.GetComponent<TileMarker>() ?? go.AddComponent<TileMarker>();
        marker.tileType = type;
    }

    /// <summary>
    /// Ein gemeinsamer Gras-Untergrund für die sehr große, nicht interaktive Kulisse.
    /// Dadurch bleibt das logische Grid klein, obwohl der Horizont weit hinausreicht.
    /// </summary>
    private void SpawnPersistentDecorationGround()
    {
        const string groundName = "Persistent Decoration Ground";
        Transform existing = transform.Find(groundName);
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = groundName;
        ground.transform.SetParent(transform, false);

        float worldWidth = (EnvironmentMaxXExclusive - EnvironmentMinX) * cellSize;
        float worldHeight = (EnvironmentMaxZExclusive - EnvironmentMinZ) * cellSize;
        ground.transform.localPosition = new Vector3(
            (EnvironmentMinX + EnvironmentMaxXExclusive) * cellSize * 0.5f,
            -0.08f,
            (EnvironmentMinZ + EnvironmentMaxZExclusive) * cellSize * 0.5f);
        ground.transform.localScale = new Vector3(worldWidth, 0.05f, worldHeight);

        Collider groundCollider = ground.GetComponent<Collider>();
        if (groundCollider != null)
            groundCollider.enabled = false;

        Renderer groundRenderer = ground.GetComponent<Renderer>();
        Renderer grassRenderer = grassTilePrefab != null
            ? grassTilePrefab.GetComponentInChildren<Renderer>()
            : null;

        if (groundRenderer != null)
        {
            groundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            groundRenderer.receiveShadows = true;
            if (grassRenderer != null)
                groundRenderer.sharedMaterial = grassRenderer.sharedMaterial;
        }

        Debug.Log($"[GridManager] Dekorationsgrund: {worldWidth}x{worldHeight} Welt-Einheiten; " +
                  $"interaktives Grid bleibt {Width}x{Height} Tiles.");
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

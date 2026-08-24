using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eine kaufbare Erweiterung in exakten Grid-Koordinaten. Dieselben Rechtecke werden
/// fuer Kaufanzeige, Tile-Sperren, Zaun und Editor-Vorschau verwendet.
/// </summary>
public readonly struct FarmExpansionArea
{
    public string Id { get; }
    public string DisplayName { get; }
    public int CostIndex { get; }
    public RectInt Tiles { get; }

    public FarmExpansionArea(string id, string displayName, int costIndex, RectInt tiles)
    {
        Id = id;
        DisplayName = displayName;
        CostIndex = costIndex;
        Tiles = tiles;
    }
}

/// <summary>
/// Findet beim Start alle GridZones in der Scene, setzt IsLocked auf betroffenen Tiles
/// und stellt die Unlock-API bereit.
///
/// Muss nach GridManager initialisiert werden → Script Execution Order beachten
/// oder einfach in Start() statt Awake() arbeiten.
/// </summary>
public class ZoneManager : MonoBehaviour
{
    public static ZoneManager Instance { get; private set; }

    [Header("Richtungs-Erweiterungen")]
    [Tooltip("Ersetzt die alten Innenzonen durch fünf Felder im 3x2-Layout um die Startzone.")]
    [SerializeField] private bool useDirectionalExpansions = true;
    [Tooltip("Kosten fuer oben links, oben rechts, unten links, unten Mitte und unten rechts.")]
    [SerializeField] private int[] directionalUnlockCosts =
        { 250, 450, 600, 800, 600 };

    [Header("Dynamischer Farmzaun")]
    [Tooltip("Name des bestehenden Zaun-Parents in der Farm-Scene. Dessen erstes Kind dient als Vorlage.")]
    [SerializeField] private string existingFenceRootName = "Zäune";
    [Tooltip("Laenge eines Zaun-Prefabs in Welt-Units.")]
    [Min(0.1f)]
    [SerializeField] private float fenceSegmentLength = 3f;
    [Tooltip("Kleine optische Korrektur passend zum vorhandenen Zaunmodell.")]
    [SerializeField] private Vector3 fenceVisualOffset = new Vector3(0.23f, 0f, 0.07f);

    [Header("Kaufanzeige der Erweiterungen")]
    [Tooltip("Hoehe des World-Space-Kaufbuttons ueber dem Boden.")]
    [Min(0.1f)]
    [SerializeField] private float purchaseButtonHeight = 1.4f;
    [Tooltip("Welt-Skalierung des Kaufbuttons.")]
    [Min(0.001f)]
    [SerializeField] private float purchaseButtonWorldScale = 0.012f;
    [Tooltip("Sehr dezente Flaechenfarbe, die nur beim Hover ueber den Kaufbutton sichtbar ist.")]
    [SerializeField] private Color expansionHoverFillColor = new Color(1f, 0.72f, 0.18f, 0.1f);
    [Tooltip("Randfarbe der Flaechenvorschau beim Hover.")]
    [SerializeField] private Color expansionHoverOutlineColor = new Color(1f, 0.72f, 0.18f, 0.9f);

    [Header("Baeume auf gesperrten Erweiterungen")]
    [Tooltip("Dekoriert noch nicht gekaufte Erweiterungen mit Baeumen. Beim Kauf werden sie mit der Zone entfernt.")]
    [SerializeField] private bool enableLockedExpansionTrees = true;
    [SerializeField] private GameObject[] lockedExpansionTreePrefabs;
    [Tooltip("Entspricht der Baumdichte der statischen Aussendekoration pro Quadrat- bzw. Tile-Einheit.")]
    [Min(0f)]
    [SerializeField] private float lockedExpansionTreeDensity = 0.0041f;
    [SerializeField] private int lockedExpansionTreeSeed = 29411;
    [Min(0.01f)]
    [SerializeField] private float lockedExpansionTreeMinScale = 0.55f;
    [Min(0.01f)]
    [SerializeField] private float lockedExpansionTreeMaxScale = 1.6f;
    [Min(0f)]
    [SerializeField] private float lockedExpansionTreeEdgePadding = 0.8f;
    [Min(0f)]
    [SerializeField] private float purchaseButtonTreeClearRadius = 1.6f;

    [Header("Kaufwelle")]
    [Tooltip("Verzoegerung pro Tile-Distanz vom Kaufbutton. Entspricht der Bau-Ripple-Geschwindigkeit.")]
    [Min(0f)]
    [SerializeField] private float expansionWaveDelayPerTile = 0.035f;

    private GridZone[] zones;
    private readonly Dictionary<GridZone, RectInt> zoneTileRects = new();
    private readonly Dictionary<GridZone, Transform> lockedTreeRoots = new();
    private readonly HashSet<GridZone> pendingPurchaseWaves = new();
    private GameObject fenceTemplate;
    private Vector3 fenceTemplateScale = Vector3.one;
    private Transform runtimeFenceRoot;

    void Awake()
    {
        Instance = this;

        if (useDirectionalExpansions)
            CreateDirectionalExpansionZones();
    }

    void Start()
    {
        zones = FindObjectsByType<GridZone>(FindObjectsSortMode.None);

        // An JEDE Zone hängen statt nur in TryUnlockZone zu entsperren.
        // Vorher lag das Entsperren der Tiles allein in TryUnlockZone(): wer eine Zone
        // über zone.Unlock() öffnete — der Missions-Reward und das Load-System tun genau
        // das — bekam zwar die Blocker weg, die Tiles blieben aber für immer gesperrt.
        // Über das Event ist der Pfad egal.
        foreach (var zone in zones)
        {
            if (zone == null) continue;
            var captured = zone;
            captured.OnUnlocked += () =>
            {
                UnlockZoneTiles(captured);
                RefreshFarmFence();

                if (pendingPurchaseWaves.Remove(captured))
                    PlayExpansionPurchaseWave(captured);
                else
                    lockedTreeRoots.Remove(captured);
            };
        }

        LockAllZoneTiles();
        SetupFenceTemplate();
        RefreshFarmFence();
        Debug.Log($"[ZoneManager] {zones.Length} Zone(n) gefunden und Tiles gesperrt.");
    }

    private void CreateDirectionalExpansionZones()
    {
        GridManager grid = GridManager.Instance;
        if (grid == null || grid.ExpansionSize <= 0)
        {
            Debug.LogWarning("[ZoneManager] Richtungs-Erweiterungen brauchen einen GridManager mit Expansion Size > 0.");
            return;
        }

        // Die vier alten Zonen lagen innerhalb des vorhandenen Zauns. Ihre Blocker bleiben
        // als Scene-Daten erhalten, werden fuer das neue System aber komplett deaktiviert.
        foreach (GridZone legacyZone in FindObjectsByType<GridZone>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (legacyZone != null)
                legacyZone.gameObject.SetActive(false);
        }

        var root = new GameObject("Directional Farm Expansions").transform;
        root.SetParent(transform, false);

        // Eigene IDs sind wichtig: zone_1..zone_4 gehörten zum alten System INNERHALB
        // des Startzauns. Würden wir sie wiederverwenden, würde ein alter Spielstand beim
        // Laden ungewollt neue Außenflächen freischalten.
        // 3x2-Layout aus der Skizze, in Weltkoordinaten passend zur isometrischen Kamera
        // gedreht. +X zeigt optisch zum Farmhaus und bleibt deshalb ohne Erweiterung.
        // Auf dem Bildschirm ergibt sich:
        // [ Oben links ] [ STARTZONE ] [ Oben rechts ]
        // [ Unten links] [ Unten Mitte] [ Unten rechts]
        // Keine Collider überlappen sich; jedes sichtbare Feld ist ein eigener Kauf.
        foreach (FarmExpansionArea area in CreateDirectionalLayout(grid))
            CreateExpansionZone(root, area, CostAt(area.CostIndex));
    }

    /// <summary>
    /// Gemeinsame, einzige Quelle fuer die Geometrie der fuenf Erweiterungen.
    /// Der Editor darf diese Methode ebenfalls verwenden, damit seine Vorschau nicht
    /// noch einmal separat gepflegte und dadurch abweichende Rechtecke zeichnet.
    /// </summary>
    public static FarmExpansionArea[] CreateDirectionalLayout(GridManager grid)
    {
        if (grid == null || grid.ExpansionSize <= 0)
            return System.Array.Empty<FarmExpansionArea>();

        int e = grid.ExpansionSize;
        int minX = grid.BaseMinX;
        int minZ = grid.BaseMinZ;
        int maxZ = grid.BaseMaxZExclusive;
        int w = grid.BaseWidth;
        int h = grid.BaseHeight;
        return new[]
        {
            new FarmExpansionArea("farm_expansion_left", "Top Left", 0,
                new RectInt(minX, maxZ, w, e)),
            new FarmExpansionArea("farm_expansion_right", "Top Right", 1,
                new RectInt(minX, minZ - e, w, e)),
            new FarmExpansionArea("farm_expansion_bottom_left", "Bottom Left", 2,
                new RectInt(minX - e, maxZ, e, e)),
            new FarmExpansionArea("farm_expansion_bottom", "Bottom Middle", 3,
                new RectInt(minX - e, minZ, e, h)),
            new FarmExpansionArea("farm_expansion_bottom_right", "Bottom Right", 4,
                new RectInt(minX - e, minZ - e, e, e))
        };
    }

    private void CreateExpansionZone(Transform parent, FarmExpansionArea area, int cost)
    {
        GridManager grid = GridManager.Instance;
        float cellSize = grid.CellSize;
        RectInt rect = area.Tiles;

        var go = new GameObject($"Farm Expansion {area.DisplayName}");
        go.transform.SetParent(parent, true);
        go.AddComponent<BoxCollider>();

        GridZone zone = go.AddComponent<GridZone>();
        Vector3 center = grid.transform.position + new Vector3(
            (rect.xMin + rect.width * 0.5f) * cellSize,
            0.1f,
            (rect.yMin + rect.height * 0.5f) * cellSize);
        Vector3 size = new Vector3(rect.width * cellSize, 1f, rect.height * cellSize);

        zone.Configure(area.Id, cost, center, size);
        zoneTileRects[zone] = rect;
        FarmExpansionPurchaseView.Create(
            zone,
            area.DisplayName,
            size,
            purchaseButtonHeight,
            purchaseButtonWorldScale,
            expansionHoverFillColor,
            expansionHoverOutlineColor);
        SpawnLockedExpansionTrees(
            zone,
            area.Id,
            rect.xMin,
            rect.yMin,
            rect.width,
            rect.height);
    }

    private void SpawnLockedExpansionTrees(
        GridZone zone,
        string zoneId,
        int minX,
        int minZ,
        int tileWidth,
        int tileHeight)
    {
        if (!enableLockedExpansionTrees || zone == null)
            return;
        if (lockedExpansionTreePrefabs == null || lockedExpansionTreePrefabs.Length == 0)
            return;
        if (lockedExpansionTreeDensity <= 0f || tileWidth <= 0 || tileHeight <= 0)
            return;

        int treeCount = Mathf.Max(1,
            Mathf.RoundToInt(tileWidth * tileHeight * lockedExpansionTreeDensity));
        var random = new System.Random(GetExpansionTreeSeed(zoneId, minX, minZ));
        var root = new GameObject("Locked Expansion Trees").transform;
        root.SetParent(zone.transform, false);
        lockedTreeRoots[zone] = root;

        GridManager grid = GridManager.Instance;
        float cellSize = grid.CellSize;
        float padding = Mathf.Min(
            lockedExpansionTreeEdgePadding,
            Mathf.Max(0f, Mathf.Min(tileWidth, tileHeight) * cellSize * 0.5f - 0.05f));
        float minWorldX = grid.transform.position.x + minX * cellSize + padding;
        float maxWorldX = grid.transform.position.x + (minX + tileWidth) * cellSize - padding;
        float minWorldZ = grid.transform.position.z + minZ * cellSize + padding;
        float maxWorldZ = grid.transform.position.z + (minZ + tileHeight) * cellSize - padding;
        Vector2 zoneCenter = new(zone.transform.position.x, zone.transform.position.z);

        for (int i = 0; i < treeCount; i++)
        {
            GameObject prefab = PickDecorationPrefab(lockedExpansionTreePrefabs, random);
            if (prefab == null) continue;

            Vector2 positionXZ = zoneCenter;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                positionXZ = new Vector2(
                    NextFloat(random, minWorldX, maxWorldX),
                    NextFloat(random, minWorldZ, maxWorldZ));
                if (Vector2.Distance(positionXZ, zoneCenter) >= purchaseButtonTreeClearRadius)
                    break;
            }

            float scale = NextFloat(random,
                Mathf.Min(lockedExpansionTreeMinScale, lockedExpansionTreeMaxScale),
                Mathf.Max(lockedExpansionTreeMinScale, lockedExpansionTreeMaxScale));
            GameObject tree = Instantiate(
                prefab,
                new Vector3(positionXZ.x, grid.transform.position.y, positionXZ.y),
                Quaternion.Euler(0f, NextFloat(random, 0f, 360f), 0f),
                root);
            tree.name = $"Locked Tree {i + 1}";
            tree.transform.localScale = prefab.transform.localScale * scale;

            // Baeume sind reine Kaufzonen-Dekoration und duerfen weder Klicks noch
            // Spielerbewegung oder die Tile-Auswahl blockieren.
            foreach (Collider treeCollider in tree.GetComponentsInChildren<Collider>(true))
                treeCollider.enabled = false;
        }
    }

    private int GetExpansionTreeSeed(string zoneId, int minX, int minZ)
    {
        unchecked
        {
            int hash = lockedExpansionTreeSeed;
            hash = hash * 397 ^ minX;
            hash = hash * 397 ^ minZ;
            if (!string.IsNullOrEmpty(zoneId))
            {
                foreach (char character in zoneId)
                    hash = hash * 31 + character;
            }

            return hash;
        }
    }

    private static GameObject PickDecorationPrefab(GameObject[] prefabs, System.Random random)
    {
        for (int attempt = 0; attempt < prefabs.Length; attempt++)
        {
            GameObject prefab = prefabs[random.Next(prefabs.Length)];
            if (prefab != null) return prefab;
        }

        return null;
    }

    private static float NextFloat(System.Random random, float min, float max) =>
        min + (max - min) * (float)random.NextDouble();

    /// <summary>
    /// Wird nach erfolgreicher Bezahlung, aber vor GridZone.Unlock aufgerufen. Die
    /// Baumgruppe wird aus der Zone geloest, damit der normale Unlock Button und Preview
    /// sofort entfernen kann, waehrend die Baeume auf die ankommende Tile-Welle warten.
    /// </summary>
    public void PrepareZonePurchase(GridZone zone)
    {
        if (zone == null)
            return;

        pendingPurchaseWaves.Add(zone);
        if (lockedTreeRoots.TryGetValue(zone, out Transform treeRoot) && treeRoot != null)
            treeRoot.SetParent(transform, true);
    }

    private void PlayExpansionPurchaseWave(GridZone zone)
    {
        if (zone == null || !zoneTileRects.TryGetValue(zone, out RectInt rect))
            return;

        GridManager grid = GridManager.Instance;
        if (grid == null)
            return;

        Vector2 waveOrigin = new(zone.transform.position.x, zone.transform.position.z);
        for (int x = rect.xMin; x < rect.xMax; x++)
        {
            for (int z = rect.yMin; z < rect.yMax; z++)
            {
                GameObject tile = grid.GetTileObject(x, z);
                if (tile == null) continue;

                Vector3 tilePosition = grid.GridToWorld(x, z);
                float distanceInTiles = Vector2.Distance(
                    waveOrigin,
                    new Vector2(tilePosition.x, tilePosition.z)) / grid.CellSize;
                TileConvertFx.Ensure(tile)?.PlayNudge(distanceInTiles * expansionWaveDelayPerTile);
            }
        }

        if (!lockedTreeRoots.TryGetValue(zone, out Transform treeRoot) || treeRoot == null)
            return;

        lockedTreeRoots.Remove(zone);
        StartCoroutine(HideTreesWhenWaveArrives(treeRoot, waveOrigin, grid.CellSize));
    }

    private IEnumerator HideTreesWhenWaveArrives(
        Transform treeRoot,
        Vector2 waveOrigin,
        float cellSize)
    {
        var timedTrees = new List<TimedTree>(treeRoot.childCount);
        for (int i = 0; i < treeRoot.childCount; i++)
        {
            GameObject tree = treeRoot.GetChild(i).gameObject;
            Vector3 position = tree.transform.position;
            float distanceInTiles = Vector2.Distance(
                waveOrigin,
                new Vector2(position.x, position.z)) / cellSize;
            timedTrees.Add(new TimedTree(
                distanceInTiles * expansionWaveDelayPerTile,
                tree));
        }

        timedTrees.Sort((a, b) => a.Delay.CompareTo(b.Delay));

        float elapsed = 0f;
        foreach (TimedTree timedTree in timedTrees)
        {
            float wait = timedTree.Delay - elapsed;
            if (wait > 0f)
                yield return new WaitForSeconds(wait);

            elapsed = timedTree.Delay;
            if (timedTree.Tree == null) continue;

            timedTree.Tree.SetActive(false);
            Destroy(timedTree.Tree);
        }

        if (treeRoot != null)
            Destroy(treeRoot.gameObject);
    }

    private readonly struct TimedTree
    {
        public float Delay { get; }
        public GameObject Tree { get; }

        public TimedTree(float delay, GameObject tree)
        {
            Delay = delay;
            Tree = tree;
        }
    }

    private int CostAt(int index)
    {
        if (directionalUnlockCosts == null || index < 0 || index >= directionalUnlockCosts.Length)
            return 500 + index * 250;

        return Mathf.Max(0, directionalUnlockCosts[index]);
    }

    private void SetupFenceTemplate()
    {
        Transform existingRoot = null;
        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate.name == existingFenceRootName)
            {
                existingRoot = candidate;
                break;
            }
        }

        if (existingRoot == null || existingRoot.childCount == 0)
        {
            Debug.LogWarning($"[ZoneManager] Zaun-Parent '{existingFenceRootName}' oder Zaunvorlage fehlt.");
            return;
        }

        fenceTemplate = existingRoot.GetChild(0).gameObject;
        fenceTemplateScale = fenceTemplate.transform.lossyScale;

        // Die Scene enthält neben dem alten Außenzaun auch viele einzelne Zaunstücke
        // der vier ehemaligen Innenzonen. Alle Instanzen des verwendeten Zaunmodells
        // explizit abschalten, damit wirklich nur der Runtime-Zaun sichtbar bleibt.
        int hiddenFenceObjects = 0;
        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (!candidate.name.StartsWith("Env_WoodFence_02")) continue;
            candidate.gameObject.SetActive(false);
            hiddenFenceObjects++;
        }

        runtimeFenceRoot = new GameObject("Runtime Farm Fence").transform;
        runtimeFenceRoot.SetParent(existingRoot.parent, true);
        runtimeFenceRoot.position = Vector3.zero;
        runtimeFenceRoot.rotation = Quaternion.identity;
        runtimeFenceRoot.localScale = Vector3.one;

        existingRoot.gameObject.SetActive(false);
        Debug.Log($"[ZoneManager] {hiddenFenceObjects} alte Zaunobjekte deaktiviert; Runtime-Zaun übernimmt die Besitzgrenze.");
    }

    private void RefreshFarmFence()
    {
        if (!useDirectionalExpansions || fenceTemplate == null || runtimeFenceRoot == null)
            return;

        for (int i = runtimeFenceRoot.childCount - 1; i >= 0; i--)
        {
            GameObject oldPiece = runtimeFenceRoot.GetChild(i).gameObject;
            oldPiece.SetActive(false);
            Destroy(oldPiece);
        }

        GridManager grid = GridManager.Instance;
        var horizontalEdges = new Dictionary<int, List<int>>();
        var verticalEdges = new Dictionary<int, List<int>>();

        // Der Zaun wird ausschließlich aus der geometrisch besessenen Fläche gebaut:
        // Startrechteck + Rechtecke der freigeschalteten Zonen. GridCell.IsLocked ist
        // absichtlich keine Quelle mehr, weil Lade-/Missionsreihenfolgen dort kurzzeitig
        // widersprüchliche Zustände erzeugen können und der Zaun dann gezackt wird.
        var ownedTiles = new HashSet<Vector2Int>();
        AddOwnedRectangle(ownedTiles, grid.StartAreaTiles);

        int unlockedZoneCount = 0;
        foreach (GridZone zone in zones)
        {
            if (zone == null || !zone.IsUnlocked) continue;
            if (!zoneTileRects.TryGetValue(zone, out RectInt rect)) continue;

            AddOwnedRectangle(ownedTiles, rect);
            unlockedZoneCount++;
        }

        foreach (Vector2Int tile in ownedTiles)
        {
            int x = tile.x;
            int z = tile.y;

            if (!ownedTiles.Contains(new Vector2Int(x, z - 1))) AddEdge(horizontalEdges, z, x);
            if (!ownedTiles.Contains(new Vector2Int(x, z + 1))) AddEdge(horizontalEdges, z + 1, x);
            if (!ownedTiles.Contains(new Vector2Int(x - 1, z))) AddEdge(verticalEdges, x, z);
            if (!ownedTiles.Contains(new Vector2Int(x + 1, z))) AddEdge(verticalEdges, x + 1, z);
        }

        SpawnFenceRuns(horizontalEdges, true);
        SpawnFenceRuns(verticalEdges, false);
        Debug.Log($"[ZoneManager] Farmzaun neu gebaut: {ownedTiles.Count} Besitz-Tiles, " +
                  $"{unlockedZoneCount}/{zones.Length} Erweiterungen offen.");
    }

    private static void AddOwnedRectangle(HashSet<Vector2Int> ownedTiles, RectInt rect)
    {
        for (int x = rect.xMin; x < rect.xMax; x++)
            for (int z = rect.yMin; z < rect.yMax; z++)
                ownedTiles.Add(new Vector2Int(x, z));
    }

    private static void AddEdge(Dictionary<int, List<int>> edges, int boundary, int start)
    {
        if (!edges.TryGetValue(boundary, out List<int> starts))
        {
            starts = new List<int>();
            edges.Add(boundary, starts);
        }

        starts.Add(start);
    }

    private void SpawnFenceRuns(Dictionary<int, List<int>> edges, bool horizontal)
    {
        foreach (var pair in edges)
        {
            List<int> starts = pair.Value;
            starts.Sort();

            int runStart = starts[0];
            int previous = runStart;

            for (int i = 1; i <= starts.Count; i++)
            {
                bool continues = i < starts.Count && starts[i] == previous + 1;
                if (continues)
                {
                    previous = starts[i];
                    continue;
                }

                SpawnFenceRun(pair.Key, runStart, previous - runStart + 1, horizontal);

                if (i < starts.Count)
                {
                    runStart = starts[i];
                    previous = runStart;
                }
            }
        }
    }

    private void SpawnFenceRun(int boundary, int runStart, int runLength, bool horizontal)
    {
        GridManager grid = GridManager.Instance;
        int segmentCells = Mathf.Max(1, Mathf.RoundToInt(fenceSegmentLength / grid.CellSize));

        for (int offset = 0; offset < runLength; offset += segmentCells)
        {
            int cellsInPiece = Mathf.Min(segmentCells, runLength - offset);
            int pieceStart = runStart + offset;

            float pieceLength = cellsInPiece * grid.CellSize;
            float alongCenter = pieceStart * grid.CellSize + pieceLength * 0.5f;

            Vector3 position = grid.transform.position + (horizontal
                ? new Vector3(alongCenter, 0f, boundary * grid.CellSize)
                : new Vector3(boundary * grid.CellSize, 0f, alongCenter));
            position += fenceVisualOffset;

            Quaternion rotation = Quaternion.Euler(0f, horizontal ? 0f : -90f, 0f);
            GameObject piece = Instantiate(fenceTemplate, position, rotation, runtimeFenceRoot);
            piece.name = horizontal ? "Farm Fence Horizontal" : "Farm Fence Vertical";
            piece.transform.localScale = Vector3.Scale(
                fenceTemplateScale,
                new Vector3(pieceLength / fenceSegmentLength, 1f, 1f));
            piece.SetActive(true);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Versucht eine Zone freizuschalten (zieht Kosten ab, entsperrt Tiles).
    /// Gibt false zurück wenn kein Gold oder bereits freigeschaltet.
    /// </summary>
    public bool TryUnlockZone(GridZone zone)
    {
        if (zone == null) return false;
        return zone.TryUnlock(); // Tiles laufen über OnUnlocked
    }

    /// <summary>Zone per SaveId/Name suchen — für Missions-Belohnungen und -Ziele.</summary>
    public GridZone FindZone(string zoneId)
    {
        if (string.IsNullOrWhiteSpace(zoneId) || zones == null) return null;

        foreach (var zone in zones)
        {
            if (zone == null) continue;
            if (zone.SaveId == zoneId || zone.gameObject.name == zoneId) return zone;
        }

        return null;
    }

    // ── Interne Logik ─────────────────────────────────────────────────────────

    private void LockAllZoneTiles()
    {
        GridManager grid = GridManager.Instance;

        for (int x = grid.MinX; x < grid.MaxXExclusive; x++)
        {
            for (int z = grid.MinZ; z < grid.MaxZExclusive; z++)
            {
                var cell = grid.GetCell(x, z);
                if (cell == null) continue;

                cell.IsLocked = IsCoveredByLockedZone(x, z, grid);
            }
        }
    }

    private bool IsCoveredByLockedZone(int x, int z, GridManager grid)
    {
        var tile = new Vector2Int(x, z);
        foreach (GridZone zone in zones)
        {
            if (zone == null || zone.IsUnlocked)
                continue;

            if (zoneTileRects.TryGetValue(zone, out RectInt rect))
            {
                if (rect.Contains(tile))
                    return true;
                continue;
            }

            // Fallback fuer das alte, frei im Editor platzierte Zonen-System.
            if (zone.ContainsTile(grid.GridToWorld(x, z)))
                return true;
        }

        return false;
    }

    // Nach jedem Kauf ALLE Zellen aus den kanonischen Rechtecken neu ableiten. Das heilt
    // auch alte Spielstaende, in denen einzelne Rand-Tiles faelschlich gesperrt blieben.
    private void UnlockZoneTiles(GridZone _) => LockAllZoneTiles();
}

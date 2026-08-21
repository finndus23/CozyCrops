using System.Collections.Generic;
using UnityEngine;

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
    private GridZone[] zones;
    private readonly Dictionary<GridZone, RectInt> zoneTileRects = new();
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

        int e = grid.ExpansionSize;
        int w = grid.BaseWidth;
        int h = grid.BaseHeight;

        // Eigene IDs sind wichtig: zone_1..zone_4 gehörten zum alten System INNERHALB
        // des Startzauns. Würden wir sie wiederverwenden, würde ein alter Spielstand beim
        // Laden ungewollt neue Außenflächen freischalten.
        // 3x2-Layout aus der Skizze, in Weltkoordinaten passend zur isometrischen Kamera
        // gedreht. +X zeigt optisch zum Farmhaus und bleibt deshalb ohne Erweiterung.
        // Auf dem Bildschirm ergibt sich:
        // [ Oben links ] [ STARTZONE ] [ Oben rechts ]
        // [ Unten links] [ Unten Mitte] [ Unten rechts]
        // Keine Collider überlappen sich; jedes sichtbare Feld ist ein eigener Kauf.
        CreateExpansionZone(root, "farm_expansion_left", "Top Left", CostAt(0),
            0, h, w, e);
        CreateExpansionZone(root, "farm_expansion_right", "Top Right", CostAt(1),
            0, -e, w, e);
        CreateExpansionZone(root, "farm_expansion_bottom_left", "Bottom Left", CostAt(2),
            -e, h, e, e);
        CreateExpansionZone(root, "farm_expansion_bottom", "Bottom Middle", CostAt(3),
            -e, 0, e, h);
        CreateExpansionZone(root, "farm_expansion_bottom_right", "Bottom Right", CostAt(4),
            -e, -e, e, e);
    }

    private void CreateExpansionZone(Transform parent, string id, string displayName, int cost,
        int minX, int minZ, int tileWidth, int tileHeight)
    {
        GridManager grid = GridManager.Instance;
        float cellSize = grid.CellSize;

        var go = new GameObject($"Farm Expansion {displayName}");
        go.transform.SetParent(parent, true);
        go.AddComponent<BoxCollider>();

        GridZone zone = go.AddComponent<GridZone>();
        Vector3 center = grid.transform.position + new Vector3(
            (minX + tileWidth * 0.5f) * cellSize,
            0.1f,
            (minZ + tileHeight * 0.5f) * cellSize);
        Vector3 size = new Vector3(tileWidth * cellSize, 1f, tileHeight * cellSize);

        zone.Configure(id, cost, center, size);
        zoneTileRects[zone] = new RectInt(minX, minZ, tileWidth, tileHeight);
        FarmExpansionPurchaseView.Create(
            zone,
            displayName,
            size,
            purchaseButtonHeight,
            purchaseButtonWorldScale,
            expansionHoverFillColor,
            expansionHoverOutlineColor);
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
        AddOwnedRectangle(ownedTiles, new RectInt(0, 0, grid.BaseWidth, grid.BaseHeight));

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

                // Die ursprüngliche 30x39-Farm beginnt vollständig entsperrt.
                // Ausschließlich die fünf Kaufzonen dürfen Grid-Zellen sperren; die
                // Landschaftskulisse liegt inzwischen bewusst außerhalb dieses Grids.
                cell.IsLocked = false;

                Vector3 tileCenter = grid.GridToWorld(x, z);

                foreach (var zone in zones)
                {
                    if (!zone.IsUnlocked && zone.ContainsTile(tileCenter))
                    {
                        cell.IsLocked = true;
                        break; // Tile muss nur von einer Zone gesperrt sein
                    }
                }
            }
        }
    }

    private void UnlockZoneTiles(GridZone zone)
    {
        GridManager grid = GridManager.Instance;

        for (int x = grid.MinX; x < grid.MaxXExclusive; x++)
        {
            for (int z = grid.MinZ; z < grid.MaxZExclusive; z++)
            {
                var cell = grid.GetCell(x, z);
                if (cell == null || !cell.IsLocked) continue;

                Vector3 tileCenter = grid.GridToWorld(x, z);
                if (!zone.ContainsTile(tileCenter)) continue;

                // Prüfen ob eine andere (noch gesperrte) Zone dieses Tile beansprucht
                bool stillLocked = false;
                foreach (var other in zones)
                {
                    if (other == zone) continue;
                    if (!other.IsUnlocked && other.ContainsTile(tileCenter))
                    {
                        stillLocked = true;
                        break;
                    }
                }

                if (!stillLocked)
                    cell.IsLocked = false;
            }
        }
    }
}

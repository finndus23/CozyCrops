using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>Farbe eines Werkzeugs für Hover- und Warteschlangen-Overlays.</summary>
[System.Serializable]
public class ToolIndicatorStyle
{
    public ToolType tool;
    public Color color = Color.white;
}

/// <summary>
/// Zeichnet alle Tile-Overlays im Farm-Modus:
/// - Hover-/AoE-Vorschau unter dem Cursor
/// - Status der Tool-Warteschlange (wartend / laufend mit Fortschrittsring)
///
/// Beides liegt bewusst in einer Klasse: es teilt sich Prefab, Pool und Farblogik,
/// und die beiden Ebenen müssen sich absprechen — auf einer Tile die schon in der
/// Warteschlange steht darf nicht zusätzlich das Hover-Overlay liegen, sonst
/// überlagern sich zwei Rahmen.
///
/// Setup:
/// - overlayPrefab → "Tile Indicator"-Prefab (Quad mit CozyCrops/TileIndicator-Shader)
/// - heightOffset  → kleiner Abstand über der Tile-Oberkante (nicht absolut!)
/// </summary>
public class AoEPreview : MonoBehaviour
{
    public static AoEPreview Instance { get; private set; }

    [Header("Referenzen")]
    [SerializeField] private GameObject overlayPrefab;

    [Header("Einstellungen")]
    [Tooltip("Abstand ÜBER der Tile-Oberkante — kein absoluter Welt-Y-Wert.\n" +
             "Klein halten (0.005–0.02): der Shader hat einen Tiefen-Bias, der Indikator " +
             "gewinnt den Tiefentest auch ohne echten Abstand.")]
    [SerializeField] private float heightOffset = 0.01f;
    [Tooltip("Sollte der cellSize des GridManagers entsprechen.")]
    [SerializeField] private float tileSize = 1f;

    [Header("Farben")]
    [Tooltip("Rückfallfarbe für den Hover-Indikator: Baumodus und alles, wofür in " +
             "toolStyles kein Eintrag steht.")]
    [SerializeField] private Color indicatorColor = new(1f, 1f, 1f, 0.9f);

    [Tooltip("Rückfallfarbe für Tiles in der Warteschlange, wenn das Werkzeug keinen " +
             "eigenen Stil hat.")]
    [SerializeField] private Color queuedColor = new(1f, 0.78f, 0.25f, 0.95f);

    [Tooltip("Eine Farbe pro Werkzeug — gilt für Hover UND Warteschlange.\n\n" +
             "Seit die Warteschlangen pro Werkzeug parallel laufen, liegen mehrere Jobs " +
             "gleichzeitig auf dem Feld. Mit einer einzigen Queue-Farbe sieht man zwar DASS " +
             "dort etwas läuft, aber nicht WAS — Hacken, Säen und Gießen sind dann optisch " +
             "identisch. Die Farbe ist die einzige Information, die auf einem 1x1-Tile noch " +
             "lesbar ist.")]
    [SerializeField] private ToolIndicatorStyle[] toolStyles =
    {
        new() { tool = ToolType.Hoe,         color = new Color(0.85f, 0.55f, 0.25f, 1f) },
        new() { tool = ToolType.Seed,        color = new Color(0.45f, 0.85f, 0.35f, 1f) },
        new() { tool = ToolType.WateringCan, color = new Color(0.35f, 0.72f, 1f,    1f) },
        new() { tool = ToolType.Scythe,      color = new Color(1f,    0.83f, 0.30f, 1f) },
        new() { tool = ToolType.Fertilize,   color = new Color(0.55f, 0.35f, 0.75f, 1f) },
    };

    [Tooltip("Alpha-Faktor für den Hover gegenüber der Warteschlange. Der Hover ist nur " +
             "eine Vorschau und soll leiser sein als eine tatsächlich eingeplante Aktion — " +
             "sonst kann man beides bei gleicher Farbe nicht auseinanderhalten.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float hoverAlphaFactor = 0.7f;

    [Tooltip("Standardfarbe der externen Vorschau — Reichweite eines Automatik-Geraets.")]
    [SerializeField] private Color externalPreviewColor = new(0.45f, 0.8f, 1f, 0.55f);

    [Header("Animation")]
    [SerializeField] private float popInDuration = 0.16f;
    [SerializeField] private float popInFromScale = 0.55f;

    private readonly List<GameObject> pool = new();
    private readonly Dictionary<Vector2Int, GameObject> hoverOverlays = new();
    private readonly Dictionary<Vector2Int, GameObject> jobOverlays = new();

    /// <summary>
    /// Dritte Spur neben Hover und Warteschlange: eine von aussen gesetzte Kachelmenge,
    /// die stehen bleibt, bis sie ausdruecklich geloescht wird. Fuer die
    /// Reichweiten-Vorschau der Automatik-Geraete.
    ///
    /// Bewusst eine Kachelmenge und kein LineRenderer-Rechteck wie bei den Farm-Zonen: die
    /// Reichweite beschneidet sich am Gitterrand und an gesperrten Zonen unregelmaessig,
    /// ein Rechteck wuerde dem Spieler etwas Falsches zeigen.
    /// </summary>
    private readonly Dictionary<Vector2Int, GameObject> externalOverlays = new();
    private readonly List<Vector2Int> externalTiles = new();
    private Color externalColor;
    private bool externalActive;

    // Optional eine einzelne hervorgehobene Kachel innerhalb der Menge — die Zielkachel
    // beim Platzieren, gruen oder rot je nach Gueltigkeit.
    private bool hasExternalHighlight;
    private Vector2Int externalHighlightTile;
    private Color externalHighlightColor;

    // Wiederverwendet — im Gegensatz zu renderer.material erzeugt ein
    // MaterialPropertyBlock keine Material-Instanz pro Overlay.
    private MaterialPropertyBlock propertyBlock;
    private static readonly int BaseColorId    = Shader.PropertyToID("_BaseColor");
    private static readonly int FillId         = Shader.PropertyToID("_Fill");
    private static readonly int CornerLengthId = Shader.PropertyToID("_CornerLength");

    // Eckklammern für Hover, geschlossener Rahmen für den Fortschrittsring —
    // ein Ring aus vier losen Ecken wäre als Ladeanzeige nicht lesbar.
    private const float HoverCornerLength = 0.34f;
    private const float RingCornerLength  = 1f;

    private readonly List<Vector2Int> staleKeys = new();
    private readonly List<Vector2Int> desiredJobTiles = new();

    private int lastPreviewX = -999, lastPreviewZ = -999;
    private int lastPreviewAo = -1;
    private ToolType lastPreviewTool = ToolType.None;
    private bool lastPreviewBuildMode;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        propertyBlock = new MaterialPropertyBlock();
    }

    void Update()
    {
        UpdateJobOverlays();
        UpdateExternalOverlays();
        UpdateHoverOverlay();
    }

    // ── Externe Vorschau ──────────────────────────────────────────────────────

    /// <summary>
    /// Zeigt eine feste Kachelmenge an, bis <see cref="ClearExternalPreview"/> kommt.
    /// Mehrfach aufrufbar — die Menge wird ersetzt, nicht ergaenzt.
    /// </summary>
    public void SetExternalPreview(IReadOnlyList<Vector2Int> tiles, Color color)
    {
        externalTiles.Clear();
        if (tiles != null) externalTiles.AddRange(tiles);

        externalColor = color;
        externalActive = externalTiles.Count > 0;
        hasExternalHighlight = false;

        if (!externalActive) HideAllExternal();
    }

    /// <summary>
    /// Wie oben, hebt aber eine Kachel der Menge gesondert hervor — beim Platzieren die
    /// Zielkachel. Die Reichweite bleibt dabei als Flaeche lesbar, waehrend die Zielkachel
    /// zeigt, ob dort ueberhaupt gesetzt werden darf.
    /// </summary>
    public void SetExternalPreview(IReadOnlyList<Vector2Int> tiles, Color color,
                                   Vector2Int highlight, Color highlightColor)
    {
        SetExternalPreview(tiles, color);

        hasExternalHighlight = externalActive;
        externalHighlightTile = highlight;
        externalHighlightColor = highlightColor;
    }

    private Color ResolveExternalColor(Vector2Int tile) =>
        hasExternalHighlight && tile == externalHighlightTile ? externalHighlightColor : externalColor;

    /// <summary>Wie oben, in der im Inspector eingestellten Standardfarbe.</summary>
    public void SetExternalPreview(IReadOnlyList<Vector2Int> tiles) =>
        SetExternalPreview(tiles, externalPreviewColor);

    public void ClearExternalPreview()
    {
        externalTiles.Clear();
        externalActive = false;
        HideAllExternal();
    }

    /// <summary>
    /// Laeuft jeden Frame und baut die Overlays gediffed nach — dadurch heilt die Vorschau
    /// von selbst, wenn eine Kachel zwischenzeitlich von einem Job-Overlay belegt war.
    /// </summary>
    private void UpdateExternalOverlays()
    {
        if (!externalActive)
        {
            if (externalOverlays.Count > 0) HideAllExternal();
            return;
        }

        staleKeys.Clear();
        foreach (var kvp in externalOverlays)
        {
            // Job-Overlays haben Vorrang, sonst liegen zwei Quads deckungsgleich
            // uebereinander und flimmern gegeneinander.
            if (!externalTiles.Contains(kvp.Key) || jobOverlays.ContainsKey(kvp.Key))
                staleKeys.Add(kvp.Key);
        }

        foreach (var key in staleKeys)
        {
            Release(externalOverlays[key]);
            externalOverlays.Remove(key);
        }

        foreach (var tile in externalTiles)
        {
            if (jobOverlays.ContainsKey(tile)) continue;
            if (GridManager.Instance == null || GridManager.Instance.GetCell(tile.x, tile.y) == null) continue;

            var tileColor = ResolveExternalColor(tile);

            if (externalOverlays.TryGetValue(tile, out var existing))
            {
                Apply(existing, tileColor, 1f, HoverCornerLength);
                continue;
            }

            var go = Acquire(tile);
            if (go == null) continue;

            Apply(go, tileColor, 1f, HoverCornerLength);
            PlayPopIn(go);
            externalOverlays[tile] = go;
        }
    }

    private void HideAllExternal()
    {
        if (externalOverlays.Count == 0) return;

        foreach (var kvp in externalOverlays)
            Release(kvp.Value);

        externalOverlays.Clear();
    }

    // ── Hover ─────────────────────────────────────────────────────────────────

    private void UpdateHoverOverlay()
    {
        // Waehrend eine Station platziert oder verschoben wird, gehoert die Kachelanzeige
        // allein der Reichweiten-Vorschau. Der Werkzeug-Hover wuerde sich sonst genau
        // darunterlegen und beide unleserlich machen.
        bool placing = AutomationPlacementController.Instance != null
                       && AutomationPlacementController.Instance.IsPlacing;

        if (placing || !GridInput.IsHoveringGrid)
        {
            HideAllHover();
            InvalidateHoverCache();
            return;
        }

        // Im Baumodus wird immer genau eine Tile gesetzt — dort gibt es keine AoE,
        // egal welches Werkzeug in der Hotbar liegt.
        bool buildMode = BuildModeManager.Instance != null && BuildModeManager.Instance.IsActive;

        ToolType tool = buildMode || Hotbar.Instance == null
                            ? ToolType.None
                            : Hotbar.Instance.ActiveTool;

        int x = GridInput.HoveredX;
        int z = GridInput.HoveredZ;
        int aoSize = !buildMode && tool != ToolType.None && ToolRegistry.Instance != null
                        ? ToolRegistry.Instance.GetAoSize(tool)
                        : 1;

        // Tool gehört mit in den Vergleich: ein Tool-Wechsel ändert die AoE-Größe,
        // auch ohne dass sich der Cursor bewegt.
        bool unchanged = x == lastPreviewX && z == lastPreviewZ
                         && aoSize == lastPreviewAo && tool == lastPreviewTool
                         && buildMode == lastPreviewBuildMode;

        // Bei laufender Warteschlange trotzdem neu auswerten: eine Tile kann gerade
        // freigeworden oder belegt worden sein, ohne dass sich der Cursor bewegt hat.
        bool queueActive = ToolUseHandler.Instance != null
                           && (ToolUseHandler.Instance.RunningJobs.Count > 0
                               || ToolUseHandler.Instance.QueuedJobs.Count > 0);

        if (unchanged && !queueActive) return;

        lastPreviewX         = x;
        lastPreviewZ         = z;
        lastPreviewAo        = aoSize;
        lastPreviewTool      = tool;
        lastPreviewBuildMode = buildMode;

        var tiles = ToolUseHandler.CalculateAoTiles(x, z, aoSize);

        staleKeys.Clear();
        foreach (var kvp in hoverOverlays)
        {
            if (!tiles.Contains(kvp.Key) || jobOverlays.ContainsKey(kvp.Key))
                staleKeys.Add(kvp.Key);
        }
        foreach (var key in staleKeys)
        {
            Release(hoverOverlays[key]);
            hoverOverlays.Remove(key);
        }

        foreach (var tile in tiles)
        {
            // Warteschlangen-Overlay hat Vorrang
            if (jobOverlays.ContainsKey(tile)) continue;
            if (GridManager.Instance == null || GridManager.Instance.GetCell(tile.x, tile.y) == null) continue;

            var hoverColor = ResolveHoverColor(tool);

            if (hoverOverlays.TryGetValue(tile, out var existing))
            {
                Apply(existing, hoverColor, 1f, HoverCornerLength);
                continue;
            }

            var go = Acquire(tile);
            if (go == null) continue;

            Apply(go, hoverColor, 1f, HoverCornerLength);
            PlayPopIn(go);
            hoverOverlays[tile] = go;
        }
    }

    // ── Warteschlange ─────────────────────────────────────────────────────────

    /// <summary>
    /// Ein Overlay pro Tile in der Warteschlange, alle in derselben Farbe.
    /// Wartend und laufend unterscheiden sich über die FORM, nicht über den Farbton:
    /// - wartend: Eckklammern wie beim Hover, nur in der Queue-Farbe → "eingeplant"
    /// - laufend: geschlossener Rahmen der sich im Uhrzeigersinn füllt → "läuft gerade"
    ///
    /// Läuft jeden Frame, weil sich der Fortschritt kontinuierlich ändert — die
    /// Overlays selbst werden aber gediffed, es wird nur umgefärbt statt neu gebaut.
    /// </summary>
    private void UpdateJobOverlays()
    {
        var handler = ToolUseHandler.Instance;
        if (handler == null)
        {
            HideAllJobs();
            return;
        }

        desiredJobTiles.Clear();

        foreach (var job in handler.RunningJobs)
        {
            foreach (var tile in job.Tiles)
            {
                if (desiredJobTiles.Contains(tile)) continue;
                desiredJobTiles.Add(tile);
                ShowJobTile(tile, job.Progress, RingCornerLength, job.Tool);
            }
        }

        foreach (var job in handler.QueuedJobs)
        {
            foreach (var tile in job.Tiles)
            {
                if (desiredJobTiles.Contains(tile)) continue;
                desiredJobTiles.Add(tile);
                ShowJobTile(tile, 1f, HoverCornerLength, job.Tool);
            }
        }

        staleKeys.Clear();
        foreach (var kvp in jobOverlays)
        {
            if (!desiredJobTiles.Contains(kvp.Key))
                staleKeys.Add(kvp.Key);
        }
        foreach (var key in staleKeys)
        {
            Release(jobOverlays[key]);
            jobOverlays.Remove(key);
        }
    }

    private void ShowJobTile(Vector2Int tile, float fill, float cornerLength, ToolType tool)
    {
        var color = ResolveJobColor(tool);

        if (jobOverlays.TryGetValue(tile, out var existing))
        {
            Apply(existing, color, fill, cornerLength);
            return;
        }

        // Eine Tile kann vom Hover zur Warteschlange wechseln — dann das Hover-Overlay
        // freigeben, sonst liegen zwei Rahmen übereinander.
        if (hoverOverlays.TryGetValue(tile, out var hover))
        {
            Release(hover);
            hoverOverlays.Remove(tile);
        }

        var go = Acquire(tile);
        if (go == null) return;

        Apply(go, color, fill, cornerLength);
        PlayPopIn(go);
        jobOverlays[tile] = go;
    }

    // ── Farben ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Farbe des Werkzeugs, oder <paramref name="fallback"/> wenn keine hinterlegt ist.
    /// ToolType.None (Baumodus) hat bewusst nie einen Stil — dort setzt man Tiles,
    /// da wäre eine Werkzeugfarbe schlicht falsch.
    /// </summary>
    private Color ResolveColor(ToolType tool, Color fallback)
    {
        if (tool == ToolType.None || toolStyles == null) return fallback;

        foreach (var style in toolStyles)
        {
            if (style == null || style.tool != tool) continue;

            // Alpha kommt bewusst von der Rückfallfarbe: so stellt man die Deckkraft
            // aller Overlays an EINER Stelle ein, statt sie in vier Farbfeldern
            // nachziehen zu müssen.
            var c = style.color;
            c.a = fallback.a;
            return c;
        }

        return fallback;
    }

    private Color ResolveJobColor(ToolType tool) => ResolveColor(tool, queuedColor);

    private Color ResolveHoverColor(ToolType tool)
    {
        var c = ResolveColor(tool, indicatorColor);
        c.a *= hoverAlphaFactor;
        return c;
    }

    // ── Overlay-Verwaltung ────────────────────────────────────────────────────

    private GameObject Acquire(Vector2Int tile)
    {
        if (overlayPrefab == null || GridManager.Instance == null) return null;

        var go = GetFromPool();
        var worldPos = GridManager.Instance.GridToWorld(tile.x, tile.y);
        go.transform.position = new Vector3(worldPos.x, ResolveSurfaceY(tile), worldPos.z);
        go.SetActive(true);
        return go;
    }

    /// <summary>
    /// Höhe der Tile-Oberkante plus kleiner Abstand.
    ///
    /// Vorher war das ein absoluter Welt-Y-Wert im Inspector — ein Magic Number, der zur
    /// Bodenhöhe passen musste. Die Boden-Tiles liegen auf y=0 mit Scale 0.1, ihre Oberkante
    /// also bei 0.05: bei kleineren Werten steckte das Overlay im Tile und verlor den
    /// Tiefentest (unsichtbar), bei größeren schwebte es sichtbar darüber.
    ///
    /// Jetzt kommt die Höhe aus den echten Renderer-Bounds. Das überlebt Tiles anderer
    /// Höhe (die geplante Höhenvariation!) und funktioniert in jeder Szene ohne Nachstellen.
    /// </summary>
    private float ResolveSurfaceY(Vector2Int tile)
    {
        var tileObj = GridManager.Instance.GetTileObject(tile.x, tile.y);

        if (tileObj != null)
        {
            // Renderer kann auf dem Tile selbst oder auf einem Kind liegen
            var rend = tileObj.GetComponentInChildren<Renderer>();
            if (rend != null)
                return rend.bounds.max.y + heightOffset;
        }

        // Fallback wenn die Tile (noch) kein Objekt hat
        return GridManager.Instance.GridToWorld(tile.x, tile.y).y + heightOffset;
    }

    private void Apply(GameObject go, Color color, float fill, float cornerLength)
    {
        var rend = go.GetComponentInChildren<Renderer>();
        if (rend == null) return;

        rend.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetFloat(FillId, fill);
        propertyBlock.SetFloat(CornerLengthId, cornerLength);
        rend.SetPropertyBlock(propertyBlock);
    }

    private void PlayPopIn(GameObject go)
    {
        var t = go.transform;
        t.DOKill();

        var target = new Vector3(tileSize, tileSize, tileSize);
        t.localScale = target * popInFromScale;
        t.DOScale(target, popInDuration).SetEase(Ease.OutBack);
    }

    private void HideAllHover()
    {
        if (hoverOverlays.Count == 0) return;

        foreach (var kvp in hoverOverlays)
            Release(kvp.Value);

        hoverOverlays.Clear();
    }

    private void HideAllJobs()
    {
        if (jobOverlays.Count == 0) return;

        foreach (var kvp in jobOverlays)
            Release(kvp.Value);

        jobOverlays.Clear();
    }

    private void InvalidateHoverCache()
    {
        lastPreviewX    = -999;
        lastPreviewZ    = -999;
        lastPreviewAo   = -1;
        lastPreviewTool = ToolType.None;
    }

    private void Release(GameObject go)
    {
        if (go == null) return;

        go.transform.DOKill();
        go.SetActive(false);
        pool.Add(go);
    }

    private GameObject GetFromPool()
    {
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            if (pool[i] != null)
            {
                var go = pool[i];
                pool.RemoveAt(i);
                return go;
            }
        }

        var spawned = Instantiate(overlayPrefab, transform);
        spawned.transform.localScale = new Vector3(tileSize, tileSize, tileSize);
        return spawned;
    }
}

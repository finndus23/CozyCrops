using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eine platzierte Automations-Station. Hält ihr eigenes Level (= Reichweite) und bis zu
/// vier eingesetzte <see cref="AutomationModule"/>.
///
/// <b>Warum eine Station statt vier einzelner Geräte:</b> vier getrennt stehende Geräte
/// überdecken sich nur teilweise. Der Schnitt ihrer vier Quadrate misst bei Radius r genau
/// 2r × 2r Kacheln — und das sind exakt die vier Kacheln, auf denen die Geräte selbst
/// stehen. Auf Stufe 0 war die vollständig versorgte Ackerfläche damit null, und die Kette
/// ernten → hacken → säen → gießen schloss sich frühestens, wenn alle vier Geräte Stufe 10
/// hatten. Mit einem gemeinsamen Mittelpunkt ist die angezeigte Reichweite auch die
/// tatsächlich bearbeitete Fläche.
///
/// Die Module arbeiten unabhängig: eigener Cooldown, eigener Cursor, höchstens ein Job.
/// Geteilt wird nur die Kachelliste. Da jedes Modul ein anderes Werkzeug ausführt, ist der
/// Spur-Schlüssel (Werkzeug, Instanz-ID) im ToolUseHandler pro Modul eindeutig, obwohl alle
/// dieselbe Instanz-ID melden.
/// </summary>
public class AutomationDevice : MonoBehaviour, IClickable
{
    [Header("Definition")]
    [SerializeField] private AutomationStationData stationData;

    [Header("Zustand")]
    [Tooltip("Level der Station — bestimmt die Reichweite. Modul-Level stecken in den Modulen.")]
    [SerializeField] private int level;

    [Tooltip("Eingesetzte Module. Pro Typ höchstens eines.")]
    [SerializeField] private List<AutomationModule> modules = new();

    [Header("Fortschrittsring")]
    [Tooltip("Dasselbe Prefab wie bei Pflanzen und Komposter (Plant Status). Optional.")]
    [SerializeField] private GameObject statusPrefab;

    [SerializeField] private float statusHeightOffset = 0.4f;

    [Tooltip("Ringfarbe, während mindestens ein Modul arbeitet.")]
    [SerializeField] private Color workingColor = new(0.45f, 0.8f, 1f, 1f);

    [Tooltip("Ringfarbe, wenn kein Modul etwas tun kann — kein Saatgut, nichts zu ernten.")]
    [SerializeField] private Color idleColor = new(0.6f, 0.6f, 0.6f, 1f);

    [SerializeField, Range(0f, 1f)] private float trackAlpha = 0.6f;
    [SerializeField, Range(0.01f, 0.2f)] private float ringWidth = 0.09f;

    private static readonly int BaseColorId  = Shader.PropertyToID("_BaseColor");
    private static readonly int ProgressId   = Shader.PropertyToID("_Progress");
    private static readonly int SymbolId     = Shader.PropertyToID("_Symbol");
    private static readonly int TrackAlphaId = Shader.PropertyToID("_TrackAlpha");
    private static readonly int RingWidthId  = Shader.PropertyToID("_RingWidth");
    private const float SymbolNone = 0f;

    /// <summary>Wartezeit bis zum nächsten Versuch, wenn gerade keine Kachel Arbeit bietet.</summary>
    private const float RetryInterval = 1f;

    private GameObject statusInstance;
    private MaterialPropertyBlock statusPropertyBlock;
    private Bounds cachedBounds;
    private bool boundsCached;

    private int tileX, tileZ;
    private bool initialized;

    // Reichweiten-Kacheln der Station — von ALLEN Modulen geteilt.
    private readonly List<Vector2Int> targetTiles = new();
    private bool tilesDirty = true;

    private readonly List<Vector2Int> dispatchBuffer = new();

    // ── Öffentlicher Zustand ──────────────────────────────────────────────────

    public AutomationStationData Data => stationData;
    public int Level => level;
    public Vector2Int TilePosition => new(tileX, tileZ);
    public int Radius => stationData != null ? stationData.GetRadius(level) : 0;
    public IReadOnlyList<AutomationModule> Modules => modules;

    /// <summary>Reichweiten-Kacheln — für die Vorschau im Popup und beim Platzieren.</summary>
    public IReadOnlyList<Vector2Int> TargetTiles
    {
        get
        {
            EnsureInitialized();
            if (tilesDirty) RebuildTileCache();
            return targetTiles;
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable()
    {
        GridManager.OnTilesAppliedStatic += HandleTilesApplied;
        GridZone.OnZonePurchasedStatic += HandleZonePurchased;
    }

    void OnDisable()
    {
        GridManager.OnTilesAppliedStatic -= HandleTilesApplied;
        GridZone.OnZonePurchasedStatic -= HandleZonePurchased;
    }

    void Start() => RefreshAttachments();

    void Update()
    {
        if (stationData == null) return;
        if (!EnsureInitialized()) return;

        for (int i = 0; i < modules.Count; i++)
            TickModule(modules[i]);
    }

    void LateUpdate() => UpdateStatusVisual();

    // ── Takt pro Modul ────────────────────────────────────────────────────────

    private void TickModule(AutomationModule module)
    {
        if (module == null || module.data == null || !module.enabled) return;

        // Fertige oder abgebrochene Jobs werden hier im EIGENEN Update losgelassen, niemals
        // aus OnJobFinished heraus. CompleteJob läuft aus ToolUseHandler.Update, und direkt
        // danach iteriert PromoteQueued über 'queued' — ein Enqueue aus dem Event heraus
        // würde die Indizes mitten im Durchlauf verschieben.
        //
        // Der Zustand wird deshalb gepollt statt abonniert: O(1), kann konstruktiv nicht zur
        // falschen Zeit feuern, und es gibt kein Abo, das beim Aus- und Wiedereinschalten
        // verlorengehen könnte.
        if (module.pendingJob != null)
        {
            if (module.pendingJob.State != ToolJobState.Finished
                && module.pendingJob.State != ToolJobState.Cancelled)
                return;

            module.pendingJob = null;
        }

        module.cooldown -= Time.deltaTime;
        if (module.cooldown > 0f) return;

        if (!TryDispatch(module))
        {
            module.idle = true;
            module.cooldown = RetryInterval;
            return;
        }

        module.idle = false;
        module.cooldown = module.data.GetInterval(module.level);
    }

    /// <summary>
    /// Sucht ab dem gemerkten Cursor die nächsten Kacheln mit sinnvoller Arbeit und reiht
    /// dafür EINEN Job ein. Der Weg über die Job-Queue statt direkt über PlantManager.TryX
    /// ist Absicht: nur so greifen die Sperre gegen Doppelbearbeitung, die Fortschrittsringe
    /// auf der Kachel und die geteilte Saatgut-Reservierung.
    /// </summary>
    private bool TryDispatch(AutomationModule module)
    {
        var handler = ToolUseHandler.Instance;
        if (handler == null) return false;

        if (tilesDirty) RebuildTileCache();
        if (targetTiles.Count == 0) return false;

        var tool = module.ExecutesTool;
        if (tool == ToolType.None) return false;

        int wanted = module.data.GetTilesPerTick(module.level);
        dispatchBuffer.Clear();

        int nextCursor = module.scanIndex;
        for (int step = 0; step < targetTiles.Count && dispatchBuffer.Count < wanted; step++)
        {
            int i = (module.scanIndex + step) % targetTiles.Count;
            var tile = targetTiles[i];

            // Gesperrte Zonen sind automatisch abgedeckt: CanApplyTool liefert bei IsLocked
            // false. Ragt die Reichweite in eine ungekaufte Zone, arbeitet die Station dort
            // einfach nicht — und fängt von selbst an, sobald die Zone aufgeht.
            if (!handler.CanApplyTool(tile.x, tile.y, tool, module.seed)) continue;

            // Werkzeugübergreifend: was der Spieler gerade bearbeitet, fasst die Station
            // nicht an. Gilt so, solange allowLayering aus ist (Standard).
            if (handler.IsTileScheduled(tile)) continue;

            dispatchBuffer.Add(tile);
            nextCursor = (i + 1) % targetTiles.Count;
        }

        if (dispatchBuffer.Count == 0) return false;

        // Instanz-ID der Station als Besitzer. Der Spur-Schlüssel im ToolUseHandler ist
        // (Werkzeug, Besitzer) — da jedes Modul ein anderes Werkzeug führt, bekommt jedes
        // seine eigene Spur, obwohl alle dieselbe ID melden.
        var job = handler.TryEnqueueAutomationJob(dispatchBuffer, tool, module.seed,
                                                  GetInstanceID(), module.data.actionDuration);
        if (job == null) return false;

        module.pendingJob = job;
        module.scanIndex = nextCursor;
        return true;
    }

    // ── Module verwalten ──────────────────────────────────────────────────────

    public AutomationModule GetModule(AutomationDeviceType type)
    {
        foreach (var m in modules)
            if (m != null && m.Type == type) return m;
        return null;
    }

    public bool HasModule(AutomationDeviceType type) => GetModule(type) != null;

    /// <summary>
    /// Setzt ein Modul ein. Pro Typ höchstens eines — ein zweites Gieß-Modul brächte nichts
    /// als eine zweite Spur auf denselben Kacheln.
    /// </summary>
    public AutomationModule InstallModule(AutomationDeviceData data, int startLevel = 0)
    {
        if (data == null || HasModule(data.deviceType)) return null;

        var module = new AutomationModule
        {
            data = data,
            level = Mathf.Clamp(startLevel, 0, data.maxLevel),
            enabled = true
        };

        modules.Add(module);
        RefreshAttachments();
        return module;
    }

    public bool RemoveModule(AutomationDeviceType type)
    {
        var module = GetModule(type);
        if (module == null) return false;

        if (module.attachment != null) Destroy(module.attachment);
        modules.Remove(module);
        RefreshAttachments();
        return true;
    }

    /// <summary>
    /// Hängt für jedes Modul sein Anbauteil ans Gehäuse. So wächst die Station sichtbar mit
    /// jedem eingesetzten Modul, statt dass vier getrennte Maschinen nebeneinanderstehen.
    /// </summary>
    private void RefreshAttachments()
    {
        if (stationData == null) return;

        for (int i = 0; i < modules.Count; i++)
        {
            var module = modules[i];
            if (module == null || module.data == null) continue;

            if (module.attachment == null)
            {
                if (module.data.worldPrefab == null) continue;

                module.attachment = Instantiate(module.data.worldPrefab, transform);
            }

            module.attachment.transform.localPosition = stationData.GetSlotOffset(i);
            module.attachment.transform.localRotation = module.data.worldPrefab != null
                ? module.data.worldPrefab.transform.rotation
                : Quaternion.identity;
        }

        boundsCached = false;   // Gehäuse ist gewachsen → Ringposition neu bestimmen
    }

    // ── Initialisierung und Position ──────────────────────────────────────────

    private bool EnsureInitialized()
    {
        if (initialized) return true;

        var grid = GridManager.Instance;
        if (grid == null) return false;
        if (!grid.WorldToGrid(transform.position, out int x, out int z)) return false;

        SetTilePosition(x, z);
        return true;
    }

    public void SetTilePosition(int x, int z, bool snapTransform = true)
    {
        tileX = x;
        tileZ = z;
        initialized = true;
        tilesDirty = true;

        foreach (var module in modules)
            if (module != null) module.scanIndex = 0;

        if (snapTransform && GridManager.Instance != null)
        {
            var world = GridManager.Instance.GridToWorld(x, z);
            transform.position = new Vector3(world.x, transform.position.y, world.z);
        }

        boundsCached = false;
    }

    public void SetData(AutomationStationData newData)
    {
        stationData = newData;
        tilesDirty = true;
        RefreshAttachments();
    }

    public void SetLevel(int newLevel)
    {
        int max = stationData != null ? stationData.maxLevel : newLevel;
        level = Mathf.Clamp(newLevel, 0, max);
        tilesDirty = true;   // Radius kann sich geändert haben
    }

    /// <summary>Hebt die Reichweite um eine Stufe. Gold bucht der Aufrufer ab.</summary>
    public bool TryUpgrade()
    {
        if (stationData == null || level >= stationData.maxLevel) return false;
        SetLevel(level + 1);
        return true;
    }

    // ── Kachel-Cache ──────────────────────────────────────────────────────────

    private void HandleTilesApplied(TileType type, Vector3 worldPos, int count) => tilesDirty = true;
    private void HandleZonePurchased(string zoneId) => tilesDirty = true;

    /// <summary>
    /// Quadratisch (Chebyshev) um die Station, von innen nach außen. Gesperrte Zonen fliegen
    /// nicht raus — das erledigt CanApplyTool zur Laufzeit, und nach einem Zonenkauf wäre
    /// der Cache sonst zusätzlich falsch.
    /// </summary>
    private void RebuildTileCache()
    {
        targetTiles.Clear();
        tilesDirty = false;

        foreach (var module in modules)
            if (module != null) module.scanIndex = 0;

        var grid = GridManager.Instance;
        if (grid == null || stationData == null) return;

        int radius = stationData.GetRadius(level);

        for (int ring = 1; ring <= radius; ring++)
        {
            for (int dx = -ring; dx <= ring; dx++)
            {
                for (int dz = -ring; dz <= ring; dz++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;

                    int x = tileX + dx;
                    int z = tileZ + dz;
                    if (!grid.IsInBounds(x, z)) continue;

                    targetTiles.Add(new Vector2Int(x, z));
                }
            }
        }

        // Die eigene Kachel bleibt draußen — die Station steht darauf.
    }

    // ── Klick ─────────────────────────────────────────────────────────────────

    public void OnClick()
    {
        if (AutomationPlacementController.Instance != null
            && AutomationPlacementController.Instance.IsPlacing) return;

        AutomationDevicePopup.Instance?.Show(this);
    }

    // ── Fortschrittsring ──────────────────────────────────────────────────────

    /// <summary>
    /// Zeigt den Fortschritt des Moduls, das als nächstes dran ist. Grau, wenn kein einziges
    /// Modul etwas zu tun findet — ein stiller Leerlauf soll nicht wie ein Defekt aussehen.
    /// </summary>
    private void UpdateStatusVisual()
    {
        if (statusPrefab == null) return;

        bool anyActive = false;
        bool allIdle = true;
        float progress = 0f;

        foreach (var module in modules)
        {
            if (module == null || module.data == null || !module.enabled) continue;

            anyActive = true;
            if (!module.idle) allIdle = false;

            float p = module.Progress;
            if (p > progress) progress = p;
        }

        if (!anyActive || stationData == null)
        {
            if (statusInstance != null) statusInstance.SetActive(false);
            return;
        }

        if (statusInstance == null)
        {
            statusInstance = Instantiate(statusPrefab, transform);
            statusPropertyBlock = new MaterialPropertyBlock();
        }

        statusInstance.SetActive(true);
        statusInstance.transform.position = GetStatusPosition();

        var rend = statusInstance.GetComponentInChildren<Renderer>();
        if (rend == null) return;

        rend.GetPropertyBlock(statusPropertyBlock);
        statusPropertyBlock.SetColor(BaseColorId, allIdle ? idleColor : workingColor);
        statusPropertyBlock.SetFloat(ProgressId, progress);
        statusPropertyBlock.SetFloat(SymbolId, SymbolNone);
        statusPropertyBlock.SetFloat(TrackAlphaId, trackAlpha);
        statusPropertyBlock.SetFloat(RingWidthId, ringWidth);
        rend.SetPropertyBlock(statusPropertyBlock);
    }

    private Vector3 GetStatusPosition()
    {
        if (!boundsCached)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                cachedBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    cachedBounds.Encapsulate(renderers[i].bounds);
            }
            else
            {
                cachedBounds = new Bounds(transform.position, Vector3.one);
            }

            boundsCached = true;
        }

        return new Vector3(cachedBounds.center.x, cachedBounds.max.y + statusHeightOffset, cachedBounds.center.z);
    }
}

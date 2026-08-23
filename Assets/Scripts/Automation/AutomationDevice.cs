using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ein platziertes Automatik-Gerät in der Welt. Hält sein EIGENES Level — Upgrades laufen
/// pro Gerät, nicht global pro Typ.
///
/// Arbeitsweise: alle paar Sekunden (Intervall aus <see cref="AutomationDeviceData"/>) sucht
/// das Gerät in seiner Reichweite die nächste Kachel, auf der seine Aktion etwas bewirkt,
/// und führt sie dort aus. Pro Takt genau eine Kachel (ab dem Capstone-Meilenstein zwei) —
/// Handarbeit bleibt damit immer schneller. Der Reiz ist Layout, nicht Effizienz.
///
/// ETAPPE 1: ruft PlantManager direkt auf. Ab Etappe 2 läuft das über die Job-Queue des
/// ToolUseHandler (eigene Spur), damit Doppelbearbeitung, Fortschrittsringe und die
/// Saatgut-Reservierung greifen.
/// </summary>
public class AutomationDevice : MonoBehaviour
{
    [Header("Definition")]
    [SerializeField] private AutomationDeviceData data;

    [Header("Zustand")]
    [Tooltip("Level dieses einzelnen Geräts. Wird mit ihm gespeichert.")]
    [SerializeField] private int level;

    [Tooltip("Ausgeschaltete Geräte ticken nicht, bleiben aber stehen.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Nur für die Sämaschine: welche Sorte gesät wird. Pro Gerät gespeichert.")]
    [SerializeField] private PlantType seed;

    /// <summary>Wartezeit bis zum nächsten Versuch, wenn gerade keine Kachel etwas zu tun hat.
    /// Kurz, damit das Gerät zügig anspringt, sobald wieder Arbeit anfällt.</summary>
    private const float RetryInterval = 1f;

    private int tileX, tileZ;
    private bool initialized;
    private float cooldown;

    // Reichweiten-Kacheln, einmal gecacht und von innen nach außen sortiert.
    private readonly List<Vector2Int> targetTiles = new();
    private bool tilesDirty = true;

    // Round-Robin-Cursor: der Scan startet jeden Takt dort, wo er zuletzt fündig wurde.
    // Ohne den würde immer wieder dieselbe innere Kachel gewinnen und die Randkacheln
    // verhungern.
    private int scanIndex;

    // ── Öffentlicher Zustand ──────────────────────────────────────────────────

    public AutomationDeviceData Data => data;
    public int Level => level;
    public bool IsEnabled => isEnabled;
    public PlantType Seed => seed;
    public Vector2Int TilePosition => new(tileX, tileZ);
    public int Radius => data != null ? data.GetRadius(level) : 0;
    public float Interval => data != null ? data.GetInterval(level) : 1f;

    /// <summary>Fortschritt bis zum nächsten Takt, 0–1. Für den Welt-Fortschrittsring.</summary>
    public float TickProgress
    {
        get
        {
            float interval = Interval;
            if (interval <= 0f) return 1f;
            return Mathf.Clamp01(1f - cooldown / interval);
        }
    }

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
        // Reichweite verändert sich, wenn Kacheln umgebaut werden oder eine Zone aufgeht.
        GridManager.OnTilesAppliedStatic += HandleTilesApplied;
        GridZone.OnZonePurchasedStatic += HandleZonePurchased;
    }

    void OnDisable()
    {
        GridManager.OnTilesAppliedStatic -= HandleTilesApplied;
        GridZone.OnZonePurchasedStatic -= HandleZonePurchased;
    }

    void Update()
    {
        if (!isEnabled || data == null) return;
        if (!EnsureInitialized()) return;

        cooldown -= Time.deltaTime;
        if (cooldown > 0f) return;

        // Der Scan (max. 81 x CanApplyTool) läuft nur in diesem einen Frame — sonst ist
        // Update eine Subtraktion und ein Vergleich.
        if (!TryDispatch())
        {
            cooldown = RetryInterval;
            return;
        }

        cooldown = data.GetInterval(level);
    }

    // ── Initialisierung ───────────────────────────────────────────────────────

    /// <summary>
    /// Gitterposition aus der Weltposition ableiten. Lazy, weil der GridManager beim Start
    /// eines von Hand in die Szene gezogenen Geräts noch nicht bereit sein kann.
    /// </summary>
    private bool EnsureInitialized()
    {
        if (initialized) return true;

        var grid = GridManager.Instance;
        if (grid == null) return false;
        if (!grid.WorldToGrid(transform.position, out int x, out int z)) return false;

        SetTilePosition(x, z, snapTransform: true);
        return true;
    }

    /// <summary>Setzt das Gerät auf eine Gitterkachel. Vom Platzierungs-Controller und vom Laden benutzt.</summary>
    public void SetTilePosition(int x, int z, bool snapTransform = true)
    {
        tileX = x;
        tileZ = z;
        initialized = true;
        tilesDirty = true;
        scanIndex = 0;

        if (snapTransform && GridManager.Instance != null)
        {
            var world = GridManager.Instance.GridToWorld(x, z);
            transform.position = new Vector3(world.x, transform.position.y, world.z);
        }
    }

    // ── Zustands-Setter ───────────────────────────────────────────────────────

    public void SetData(AutomationDeviceData newData)
    {
        data = newData;
        tilesDirty = true;
    }

    public void SetEnabled(bool value)
    {
        isEnabled = value;
        if (!value) cooldown = 0f;
    }

    public void SetSeed(PlantType newSeed) => seed = newSeed;

    public void SetLevel(int newLevel)
    {
        int max = data != null ? data.maxLevel : newLevel;
        level = Mathf.Clamp(newLevel, 0, max);
        tilesDirty = true;   // Radius kann sich geändert haben
    }

    /// <summary>Hebt das Gerät um eine Stufe. Gold wird vom Aufrufer abgebucht.</summary>
    public bool TryUpgrade()
    {
        if (data == null || level >= data.maxLevel) return false;
        SetLevel(level + 1);
        return true;
    }

    // ── Kachel-Cache ──────────────────────────────────────────────────────────

    private void HandleTilesApplied(TileType type, Vector3 worldPos, int count) => tilesDirty = true;
    private void HandleZonePurchased(string zoneId) => tilesDirty = true;

    /// <summary>
    /// Baut die Reichweiten-Liste neu auf: quadratisch (Chebyshev) um das Gerät, sortiert von
    /// innen nach außen. Gesperrte Zonen fliegen nicht raus — das erledigt CanApplyTool zur
    /// Laufzeit, und nach einem Zonenkauf wäre der Cache sonst zusätzlich falsch.
    /// </summary>
    private void RebuildTileCache()
    {
        targetTiles.Clear();
        tilesDirty = false;
        scanIndex = 0;

        var grid = GridManager.Instance;
        if (grid == null || data == null) return;

        int radius = data.GetRadius(level);

        // Ringweise von innen nach außen: so bearbeitet ein frisch platziertes Gerät zuerst
        // das, was direkt daneben liegt.
        for (int ring = 1; ring <= radius; ring++)
        {
            for (int dx = -ring; dx <= ring; dx++)
            {
                for (int dz = -ring; dz <= ring; dz++)
                {
                    // Nur der äußere Rand dieses Rings — der Rest kam schon in Ring-1 dran.
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;

                    int x = tileX + dx;
                    int z = tileZ + dz;
                    if (!grid.IsInBounds(x, z)) continue;

                    targetTiles.Add(new Vector2Int(x, z));
                }
            }
        }

        // Die eigene Kachel bleibt bewusst draußen: das Gerät steht auf Weg oder Gras, und
        // eine Erntemaschine würde dort sonst endlos ihren eigenen Untergrund mähen.
    }

    // ── Ausführung ────────────────────────────────────────────────────────────

    /// <summary>
    /// Sucht ab dem gemerkten Cursor die nächste Kachel mit sinnvoller Arbeit und führt die
    /// Aktion dort aus. True, wenn mindestens eine Kachel bearbeitet wurde.
    /// </summary>
    private bool TryDispatch()
    {
        if (tilesDirty) RebuildTileCache();
        if (targetTiles.Count == 0) return false;

        int wanted = data.GetTilesPerTick(level);
        int done = 0;

        for (int step = 0; step < targetTiles.Count && done < wanted; step++)
        {
            int i = (scanIndex + step) % targetTiles.Count;
            var tile = targetTiles[i];

            if (!CanWorkTile(tile.x, tile.y)) continue;
            if (!Execute(tile.x, tile.y)) continue;

            done++;
            scanIndex = (i + 1) % targetTiles.Count;
        }

        return done > 0;
    }

    /// <summary>
    /// Prüft, ob die Aktion des Geräts auf dieser Kachel etwas bewirkt. Gesperrte Zonen sind
    /// dadurch automatisch mit abgedeckt — CanApplyTool liefert bei IsLocked false.
    /// </summary>
    private bool CanWorkTile(int x, int z)
    {
        var handler = ToolUseHandler.Instance;
        if (handler == null) return false;
        return handler.CanApplyTool(x, z, data.executesTool);
    }

    /// <summary>
    /// ETAPPE 1 — direkter Durchgriff auf den PlantManager, ohne Job-Queue.
    /// Ab Etappe 2 ersetzt durch ToolUseHandler.TryEnqueueAutomationJob.
    /// </summary>
    private bool Execute(int x, int z)
    {
        var plants = PlantManager.Instance;
        if (plants == null) return false;

        return data.executesTool switch
        {
            ToolType.WateringCan => plants.TryWater(x, z),
            ToolType.Hoe         => plants.TryTill(x, z),
            ToolType.Seed        => seed != null && plants.TryPlant(x, z, seed),
            ToolType.Scythe      => plants.TryHarvest(x, z),
            _                    => false
        };
    }
}

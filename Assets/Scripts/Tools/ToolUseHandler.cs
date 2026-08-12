using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Verwaltet Tool-Aktionen als Warteschlange von <see cref="ToolJob"/>s.
/// GridInput ruft TryStartUse() auf — der Rest läuft hier.
///
/// Vorher lief genau ein Cast; ein Klick während der Cast lief wurde verworfen und der
/// Spieler musste warten. Jetzt landen Aktionen in einer Queue und werden nacheinander
/// abgearbeitet, mit einem konfigurierbaren Limit paralleler Jobs. Die Gesamtzeit bleibt
/// dadurch gleich (die Duration behält ihre Bremswirkung), man kann aber vorausplanen
/// statt zu warten.
///
/// Das Limit ist bewusst ein eigener Wert und keine Konstante: "Gießkanne Level 3
/// bewässert 2 Felder gleichzeitig" ist als Tool-Upgrade vorgesehen, und die geplanten
/// Automatismus-Systeme (Bewässerungsanlage) können hier ihre Jobs einreihen statt
/// eine zweite Ausführungs-Logik zu bekommen.
/// </summary>
public class ToolUseHandler : MonoBehaviour
{
    public static ToolUseHandler Instance { get; private set; }

    [Header("Warteschlange")]
    [Tooltip("Wie viele Jobs gleichzeitig laufen dürfen. 1 = streng nacheinander.")]
    [SerializeField] private int maxParallelJobs = 1;
    [Tooltip("Obergrenze für wartende Jobs — verhindert dass ein Drag über das halbe Feld " +
             "hunderte Aktionen einreiht.")]
    [SerializeField] private int maxQueuedJobs = 24;

    // ── Zustand ───────────────────────────────────────────────────────────────

    private readonly List<ToolJob> running = new();
    private readonly List<ToolJob> queued = new();

    public IReadOnlyList<ToolJob> RunningJobs => running;
    public IReadOnlyList<ToolJob> QueuedJobs => queued;

    /// <summary>Läuft gerade mindestens ein Job?</summary>
    public bool IsCasting => running.Count > 0;

    /// <summary>Fortschritt des ältesten laufenden Jobs, 0–1. Für die Cursor-Castbar.</summary>
    public float CastProgress => running.Count > 0 ? running[0].Progress : 0f;

    public int MaxParallelJobs => Mathf.Max(1, maxParallelJobs);

    // ── Events ────────────────────────────────────────────────────────────────

    public event Action<ToolJob> OnJobEnqueued;
    public event Action<ToolJob> OnJobStarted;
    public event Action<ToolJob> OnJobFinished;

    /// <summary>Feuert bei jeder Änderung an Queue oder laufenden Jobs.</summary>
    public event Action OnQueueChanged;

    // Bestehende Events — die Cursor-Castbar und GridInput hängen daran und sollen
    // weiter den "primären" (ältesten laufenden) Job sehen.
    public event Action<IReadOnlyList<Vector2Int>> OnCastStarted;
    public event Action<float>                     OnCastProgressChanged;
    public event Action                            OnCastCompleted;
    public event Action                            OnCastCancelled;

    // ── Private ───────────────────────────────────────────────────────────────

    private readonly List<ToolJob> finishedThisFrame = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Reiht eine Aktion auf (x, z) mit dem gegebenen Tool ein.
    /// Gibt false zurück wenn die Aktion dort nichts bewirken würde oder die Queue voll ist.
    /// </summary>
    public bool TryStartUse(int x, int z, ToolType tool)
    {
        if (tool == ToolType.None) return false;

        // Queueing aus → altes Verhalten: solange etwas läuft, wird nichts angenommen.
        if (!GameSettings.ActionQueueingEnabled && running.Count > 0) return false;

        var origin = new Vector2Int(x, z);

        // Dieselbe Tile nicht doppelt einplanen. Beim Ziehen über das Feld würde man
        // sonst pro Frame denselben Job nachschieben.
        if (IsTileScheduled(origin)) return false;

        if (queued.Count >= maxQueuedJobs) return false;

        int aoSize = ToolRegistry.Instance != null ? ToolRegistry.Instance.GetAoSize(tool) : 1;
        var candidates = CalculateAoTiles(x, z, aoSize);

        var seed = tool == ToolType.Seed ? Hotbar.Instance?.SelectedSeed : null;

        // Nur die Tiles behalten auf denen das Tool wirklich etwas tut. Sonst zahlt man
        // bei 3x3-AoE volle Duration für Tiles, auf denen nichts passiert — und der
        // Fortschrittsring stünde auf leeren Feldern.
        var tiles = new List<Vector2Int>();
        foreach (var tile in candidates)
        {
            if (CanApplyTool(tile.x, tile.y, tool))
                tiles.Add(tile);
        }

        if (tiles.Count == 0) return false;

        // Saatgut reservieren: ohne das kann man 10 Felder einreihen obwohl nur 3 Samen
        // da sind, und 7 Jobs laufen durch um dann still zu scheitern.
        if (tool == ToolType.Seed)
        {
            if (seed == null) return false;

            int available = PlayerInventory.Instance != null
                ? PlayerInventory.Instance.GetSeedCount(seed)
                : 0;

            if (CountScheduledSeedUses(seed) + tiles.Count > available) return false;
        }

        float durationPerTile = ToolRegistry.Instance != null ? ToolRegistry.Instance.GetDuration(tool) : 0f;
        float duration = durationPerTile * tiles.Count;
        int yieldBonus = tool == ToolType.Scythe
            ? ToolRegistry.Instance?.GetYieldBonus(tool) ?? 0
            : 0;

        var job = new ToolJob(tool, seed, yieldBonus, origin, tiles, duration);
        queued.Add(job);

        OnJobEnqueued?.Invoke(job);
        OnQueueChanged?.Invoke();

        PromoteQueued();
        return true;
    }

    /// <summary>Bricht alles ab — laufende Jobs und Warteschlange.</summary>
    public void CancelAll()
    {
        if (running.Count == 0 && queued.Count == 0) return;

        for (int i = running.Count - 1; i >= 0; i--)
        {
            running[i].State = ToolJobState.Cancelled;
            OnJobFinished?.Invoke(running[i]);
        }
        running.Clear();

        for (int i = queued.Count - 1; i >= 0; i--)
        {
            queued[i].State = ToolJobState.Cancelled;
            OnJobFinished?.Invoke(queued[i]);
        }
        queued.Clear();

        OnQueueChanged?.Invoke();
        OnCastCancelled?.Invoke();
    }

    /// <summary>Leert nur die Warteschlange, laufende Jobs dürfen fertig werden.</summary>
    public void ClearQueue()
    {
        if (queued.Count == 0) return;

        for (int i = queued.Count - 1; i >= 0; i--)
        {
            queued[i].State = ToolJobState.Cancelled;
            OnJobFinished?.Invoke(queued[i]);
        }
        queued.Clear();

        OnQueueChanged?.Invoke();
    }

    /// <summary>Kompatibilität zum alten Aufrufpfad.</summary>
    public void CancelCast() => CancelAll();

    public bool IsTileScheduled(Vector2Int tile)
    {
        foreach (var job in running)
            if (job.Origin == tile || job.CoversTile(tile)) return true;

        foreach (var job in queued)
            if (job.Origin == tile || job.CoversTile(tile)) return true;

        return false;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (running.Count == 0)
        {
            PromoteQueued();
            return;
        }

        finishedThisFrame.Clear();

        for (int i = running.Count - 1; i >= 0; i--)
        {
            var job = running[i];
            job.Elapsed += Time.deltaTime;

            if (job.Elapsed >= job.Duration)
            {
                running.RemoveAt(i);
                finishedThisFrame.Add(job);
            }
        }

        // Erst nach der Schleife anwenden: CompleteJob löst Events aus, die selbst wieder
        // Jobs einreihen können (Missions-Trigger, später die Automatismus-Systeme).
        foreach (var job in finishedThisFrame)
            CompleteJob(job);

        if (running.Count > 0)
            OnCastProgressChanged?.Invoke(running[0].Progress);

        if (finishedThisFrame.Count > 0)
            OnQueueChanged?.Invoke();

        PromoteQueued();

        if (running.Count == 0 && queued.Count == 0 && finishedThisFrame.Count > 0)
            OnCastCompleted?.Invoke();
    }

    // ── Queue-Verwaltung ──────────────────────────────────────────────────────

    /// <summary>
    /// Schiebt wartende Jobs nach, solange das Parallel-Limit es zulässt.
    /// Beim Start wird nochmal validiert: zwischen Einreihen und Ausführen kann sich
    /// die Tile geändert haben (anderer Job hat sie umgegraben, Pflanze wurde geerntet).
    /// </summary>
    private void PromoteQueued()
    {
        bool changed = false;

        while (running.Count < MaxParallelJobs && queued.Count > 0)
        {
            var job = queued[0];
            queued.RemoveAt(0);
            changed = true;

            // Tiles die inzwischen ungültig geworden sind rausfiltern
            for (int i = job.Tiles.Count - 1; i >= 0; i--)
            {
                if (!CanApplyTool(job.Tiles[i].x, job.Tiles[i].y, job.Tool))
                    job.Tiles.RemoveAt(i);
            }

            if (job.Tiles.Count == 0)
            {
                job.State = ToolJobState.Cancelled;
                OnJobFinished?.Invoke(job);
                continue;
            }

            job.State = ToolJobState.Running;
            job.Elapsed = 0f;
            running.Add(job);

            OnJobStarted?.Invoke(job);

            // Die Cursor-Castbar kennt nur einen Cast — sie folgt dem ersten laufenden Job.
            if (running.Count == 1)
                OnCastStarted?.Invoke(job.Tiles);

            // Duration 0 → sofort anwenden, kein visuelles Warten
            if (job.Duration <= 0f)
            {
                running.Remove(job);
                CompleteJob(job);
            }
        }

        if (changed)
            OnQueueChanged?.Invoke();
    }

    private void CompleteJob(ToolJob job)
    {
        job.State = ToolJobState.Finished;

        foreach (var tile in job.Tiles)
            ApplyTool(tile.x, tile.y, job);

        OnJobFinished?.Invoke(job);
    }

    private void ApplyTool(int x, int z, ToolJob job)
    {
        bool applied = false;

        switch (job.Tool)
        {
            case ToolType.Hoe:
                applied = PlantManager.Instance.TryTill(x, z);
                break;

            case ToolType.Seed:
                // Snapshot statt Hotbar.SelectedSeed: der Spieler kann während der
                // Wartezeit längst auf eine andere Saat umgestellt haben.
                if (job.Seed != null)
                    applied = PlantManager.Instance.TryPlant(x, z, job.Seed);
                break;

            case ToolType.WateringCan:
                applied = PlantManager.Instance.TryWater(x, z);
                break;

            case ToolType.Scythe:
                applied = PlantManager.Instance.TryHarvest(x, z, job.YieldBonus);
                break;
        }

        // Treffer-Feedback: die Tile zuckt kurz, damit die Aktion einen Aufschlag hat
        // statt lautlos zu passieren.
        if (applied)
            GridManager.Instance?.PlayTileImpact(x, z);
    }

    private int CountScheduledSeedUses(PlantType seed)
    {
        int count = 0;

        foreach (var job in running)
            if (job.Tool == ToolType.Seed && job.Seed == seed) count += job.Tiles.Count;

        foreach (var job in queued)
            if (job.Tool == ToolType.Seed && job.Seed == seed) count += job.Tiles.Count;

        return count;
    }

    // ── Validierung ──────────────────────────────────────────────────────────

    /// <summary>
    /// Prüft ob das Tool auf dieser Tile überhaupt anwendbar ist.
    /// Public, damit AoEPreview pro Tile einfärben kann ob die Aktion dort etwas bewirkt.
    /// </summary>
    public bool CanApplyTool(int x, int z, ToolType tool)
    {
        var cell = GridManager.Instance?.GetCell(x, z);
        if (cell == null || cell.IsLocked) return false;

        return tool switch
        {
            ToolType.Hoe         => cell.Type == TileType.FarmPlot && !cell.IsTilled && !cell.HasPlant,
            // NeedsWatering statt nur HasPlant: eine Pflanze die ihre Gießungen für diese
            // Wachstumsphase schon hat, ist kein gültiges Ziel mehr. Sonst reiht man
            // Gieß-Jobs auf Felder ein, auf denen nichts passiert.
            ToolType.WateringCan => cell.HasPlant && cell.Plant != null && cell.Plant.NeedsWatering,
            ToolType.Scythe      => cell.HasPlant && cell.Plant != null && cell.Plant.IsFullyGrown,
            ToolType.Seed        => cell.IsTilled && !cell.HasPlant
                                    && Hotbar.Instance.SelectedSeed != null
                                    && PlayerInventory.Instance.GetSeedCount(Hotbar.Instance.SelectedSeed) > 0,
            _                    => false
        };
    }

    // ── AoE-Berechnung ────────────────────────────────────────────────────────

    public static List<Vector2Int> CalculateAoTiles(int cx, int cz, int aoSize)
    {
        var tiles = new List<Vector2Int>();

        if (aoSize <= 1)
        {
            tiles.Add(new Vector2Int(cx, cz));
            return tiles;
        }

        if (aoSize % 2 == 1)
        {
            // Ungerade: zentriert auf den Cursor
            int half = aoSize / 2;
            for (int dx = -half; dx <= half; dx++)
                for (int dz = -half; dz <= half; dz++)
                    tiles.Add(new Vector2Int(cx + dx, cz + dz));
        }
        else
        {
            // Gerade: Cursor sitzt in der +X+Z-Ecke, Tiles gehen Richtung -X-Z (zur Kamera)
            for (int dx = -(aoSize - 1); dx <= 0; dx++)
                for (int dz = -(aoSize - 1); dz <= 0; dz++)
                    tiles.Add(new Vector2Int(cx + dx, cz + dz));
        }

        return tiles;
    }
}

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
    [Tooltip("Obergrenze gleichzeitig laufender Jobs über ALLE Werkzeuge. Pro Werkzeug läuft " +
             "ohnehin nur einer — bei 4 Werkzeugen ist 4 also das sinnvolle Maximum.")]
    [SerializeField] private int maxParallelJobs = 4;

    [Tooltip("Harte Obergrenze über ALLE Werkzeuge — verhindert dass ein Drag über das halbe " +
             "Feld hunderte Aktionen einreiht. Die eigentliche Grenze kommt pro Werkzeug aus " +
             "ToolData.GetQueueSize() und ist über Meilensteine ausbaubar; dieser Wert ist " +
             "nur das Sicherheitsnetz darüber.")]
    [SerializeField] private int maxQueuedJobs = 64;

    [Header("Layering (experimentell)")]
    [Tooltip("Erlaubt, eine Tile für ein Werkzeug einzureihen, obwohl sie dafür noch gar " +
             "nicht bereit ist — z.B. pflanzen auf einem Feld das noch gehackt wird. Der " +
             "Pflanz-Job wartet dann, bis das Hacken durch ist.\n\n" +
             "Aus = altes Verhalten: nur Tiles annehmen, auf denen das Werkzeug sofort wirkt.")]
    [SerializeField] private bool allowLayering;

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

    /// <summary>
    /// Statisches Event fürs Missions-System: eine Aktion wurde eingereiht, obwohl schon
    /// eine lief oder wartete. Bewusst nur dann — sonst wäre "nutze die Warteschlange"
    /// von sechs normalen Klicks nacheinander nicht zu unterscheiden.
    /// </summary>
    public static event Action OnActionStackedStatic;

    // Bestehende Events — die Cursor-Castbar und GridInput hängen daran und sollen
    // weiter den "primären" (ältesten laufenden) Job sehen.
    public event Action<IReadOnlyList<Vector2Int>> OnCastStarted;
    public event Action<float>                     OnCastProgressChanged;
    public event Action                            OnCastCompleted;
    public event Action                            OnCastCancelled;

    // ── Private ───────────────────────────────────────────────────────────────

    private readonly List<ToolJob> finishedThisFrame = new();

    // Wiederverwendete Puffer für PromoteQueued — vermeidet Allokationen pro Frame.
    private readonly List<ToolJob> promotable = new();
    private readonly HashSet<ToolType> toolsBusyThisPass = new();

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
    /// <summary>Wartende Jobs dieses Werkzeugs (ohne die bereits laufenden).</summary>
    public int CountQueuedFor(ToolType tool)
    {
        int count = 0;
        foreach (var job in queued)
            if (job.Tool == tool) count++;
        return count;
    }

    /// <summary>
    /// Wie viele Aktionen dieses Werkzeug vorausplanen darf (Level-abhängig).
    /// Gilt nur bei aktivem Queueing — ist es aus, greift weiter oben schon die Regel
    /// "solange etwas läuft, wird nichts angenommen".
    /// </summary>
    public int GetQueueCapacity(ToolType tool)
    {
        int capacity = ToolRegistry.Instance != null ? ToolRegistry.Instance.GetQueueSize(tool) : 1;
        return Mathf.Clamp(capacity, 1, maxQueuedJobs);
    }

    public bool TryStartUse(int x, int z, ToolType tool)
    {
        if (tool == ToolType.None) return false;

        // Queueing aus → altes Verhalten: solange etwas läuft, wird nichts angenommen.
        if (!GameSettings.ActionQueueingEnabled && running.Count > 0) return false;

        var origin = new Vector2Int(x, z);

        // Dieselbe Tile nicht doppelt einplanen. Beim Ziehen über das Feld würde man
        // sonst pro Frame denselben Job nachschieben.
        // Ohne Layering blockiert jede eingeplante Tile; mit Layering nur dieselbe
        // Tile für dasselbe Werkzeug.
        if (IsTileScheduled(origin, allowLayering ? tool : ToolType.None)) return false;

        if (queued.Count >= maxQueuedJobs) return false;

        // Kapazität pro Werkzeug: die Warteschlange ist ein eigener Progressionsstrang.
        // Nur bei aktivem Queueing prüfen — sonst wäre die Kapazität auch die Schranke
        // für die allererste Aktion, und ohne Queueing ginge gar nichts mehr.
        // Bewusst nur die WARTENDEN Jobs zählen, nicht die laufenden: sonst könnte ein
        // Werkzeug mit Kapazität 1 nie etwas vorausplanen.
        if (GameSettings.ActionQueueingEnabled && CountQueuedFor(tool) >= GetQueueCapacity(tool))
            return false;

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
            {
                tiles.Add(tile);
                continue;
            }

            // Layering: auch Tiles annehmen, die gerade von einem anderen Werkzeug
            // vorbereitet werden — pflanzen auf einem Feld das noch gehackt wird.
            // Beim Ausführen wird ohnehin neu geprüft.
            if (allowLayering && IsTileScheduled(tile, ToolType.None))
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

        // Nicht mehr stur perTile × Anzahl: ToolData rechnet den Mengenrabatt ein, damit
        // eine größere Wirkungsfläche auch wirklich schneller ist und nicht nur Klicks spart.
        float duration = ToolRegistry.Instance != null
            ? ToolRegistry.Instance.GetJobDuration(tool, tiles.Count)
            : 0f;
        int yieldBonus = tool == ToolType.Scythe
            ? ToolRegistry.Instance?.GetYieldBonus(tool) ?? 0
            : 0;

        // Vor dem Einreihen prüfen: lag schon was an, staffelt der Spieler wirklich.
        bool stacked = running.Count > 0 || queued.Count > 0;

        var job = new ToolJob(tool, seed, yieldBonus, origin, tiles, duration);
        queued.Add(job);

        OnJobEnqueued?.Invoke(job);
        OnQueueChanged?.Invoke();

        if (stacked)
            OnActionStackedStatic?.Invoke();

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

    public bool IsTileScheduled(Vector2Int tile) => IsTileScheduled(tile, ToolType.None);

    /// <summary>
    /// Ist diese Tile schon eingeplant?
    ///
    /// <paramref name="tool"/> = None prüft werkzeugübergreifend (altes Verhalten).
    /// Beim Layering wird nur gegen dasselbe Werkzeug geprüft: dieselbe Tile darf dann
    /// gleichzeitig zum Hacken UND zum Pflanzen anstehen, nur nicht zweimal zum Hacken.
    /// </summary>
    public bool IsTileScheduled(Vector2Int tile, ToolType tool)
    {
        foreach (var job in running)
            if ((tool == ToolType.None || job.Tool == tool) &&
                (job.Origin == tile || job.CoversTile(tile))) return true;

        foreach (var job in queued)
            if ((tool == ToolType.None || job.Tool == tool) &&
                (job.Origin == tile || job.CoversTile(tile))) return true;

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
    /// Wartet dieser Job noch auf einen anderen, der dieselbe Tile bearbeitet?
    ///
    /// Nur beim Layering relevant: dort darf man ein Feld zum Pflanzen einreihen, das noch
    /// gehackt wird. Der Pflanz-Job muss dann warten statt sich als ungültig wegzuwerfen.
    /// </summary>
    private bool WaitsForEarlierJob(ToolJob job)
    {
        foreach (var other in running)
            if (other != job && SharesTile(other, job)) return true;

        foreach (var other in queued)
        {
            if (other == job) break; // ab hier stehen nur noch spätere Jobs
            if (SharesTile(other, job)) return true;
        }

        return false;
    }

    private static bool SharesTile(ToolJob a, ToolJob b)
    {
        foreach (var tile in b.Tiles)
            if (a.CoversTile(tile)) return true;
        return false;
    }

    /// <summary>
    /// Startet wartende Jobs. <b>Pro Werkzeug</b> läuft einer gleichzeitig — Hacken, Säen
    /// und Gießen laufen damit nebeneinander, jedes mit eigener Warteschlange.
    /// </summary>
    private void PromoteQueued()
    {
        // Erst auswählen, dann starten. Das Starten kann über CompleteJob (Duration 0)
        // Events auslösen, die selbst wieder Jobs einreihen — würde man dabei noch über
        // 'queued' iterieren, verschöben sich die Indizes mitten im Durchlauf.
        promotable.Clear();

        int busySlots = running.Count;
        toolsBusyThisPass.Clear();

        foreach (var job in running)
            toolsBusyThisPass.Add(job.Tool);

        foreach (var job in queued)
        {
            if (busySlots >= MaxParallelJobs) break;

            // Pro Werkzeug läuft genau einer — dadurch arbeiten Hacke, Seeder und
            // Gießkanne gleichzeitig, jeweils aus ihrer eigenen Warteschlange.
            if (toolsBusyThisPass.Contains(job.Tool)) continue;

            // Layering: Vorgänger auf derselben Tile noch nicht fertig → später nochmal.
            if (allowLayering && WaitsForEarlierJob(job)) continue;

            promotable.Add(job);
            toolsBusyThisPass.Add(job.Tool);
            busySlots++;
        }

        if (promotable.Count == 0) return;

        foreach (var job in promotable)
            queued.Remove(job);

        foreach (var job in promotable)
            StartJob(job);

        OnQueueChanged?.Invoke();
    }

    private void StartJob(ToolJob job)
    {
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
            return;
        }

        job.State = ToolJobState.Running;
        job.Elapsed = 0f;
        running.Add(job);

        OnJobStarted?.Invoke(job);

        // Die Cursor-Castbar kennt nur einen Cast — sie folgt dem ersten laufenden Job.
        // Die Fortschrittsringe auf den Tiles zeigen dagegen alle Jobs einzeln.
        if (running.Count == 1)
            OnCastStarted?.Invoke(job.Tiles);

        // Duration 0 → sofort anwenden, kein visuelles Warten
        if (job.Duration <= 0f)
        {
            running.Remove(job);
            CompleteJob(job);
        }
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

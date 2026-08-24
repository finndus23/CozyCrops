using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Anzeige ueber der Station")]
    [Tooltip("Hoehe der Samen-Anzeige ueber der Oberkante des Gehaeuses.")]
    [SerializeField] private float statusHeightOffset = 0.6f;

    [Tooltip("Weltgroesse der Anzeige. Groesser = aus der Ferne lesbar, aber aufdringlicher.")]
    [SerializeField] private float statusScale = 0.012f;

    [Tooltip("Zeichenreihenfolge. Muss ueber dem liegen, was sonst in der Welt gezeichnet " +
             "wird — Kachel-Highlights und Fortschrittsringe haengen beide in derselben " +
             "Transparenz-Warteschlange. Hoeher = weiter vorn.")]
    [SerializeField] private int sortingOrder = 200;

    [Tooltip("Hintergrund hinter Icon und Zahl. Ohne Sprite wird eine einfarbige Flaeche " +
             "gezeichnet — irgendein Panel-Sprite aus dem MenuClean-Satz sieht besser aus.")]
    [SerializeField] private Sprite backgroundSprite;

    [SerializeField] private Color backgroundColor = new(0.12f, 0.09f, 0.06f, 0.72f);

    [Tooltip("Schriftgroesse der Zahl, in Canvas-Einheiten.")]
    [SerializeField] private float countFontSize = 26f;

    [Tooltip("Farbe der Zahl, solange noch Samen da sind.")]
    [SerializeField] private Color countColor = new(1f, 1f, 1f, 1f);

    [Tooltip("Farbe der Zahl, wenn der Vorrat leer ist — das Saat-Modul laeuft dann leer.")]
    [SerializeField] private Color emptyCountColor = new(1f, 0.35f, 0.25f, 1f);

    private GameObject seedDisplay;
    private Image seedIconImage;
    private TMP_Text seedCountLabel;

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

    void LateUpdate() => UpdateSeedDisplay();

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

        // Sorten-Multiplikator wirkt auf Dauer UND Takt — sonst waere der Effekt durch den
        // konstanten Leerlauf verwaessert und 1,8 hiesse in Wahrheit nur 1,4.
        module.cooldown = module.data.GetInterval(module.level) * module.cropMultiplier;
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
        // Die Sorte steht erst jetzt fest — bei Giessen und Ernten haengt sie an dem, was
        // auf den Zielkacheln waechst, beim Saeen an der eingestellten Sorte.
        module.cropMultiplier = ResolveCropMultiplier(module, dispatchBuffer);

        var job = handler.TryEnqueueAutomationJob(
            dispatchBuffer, tool, module.seed, GetInstanceID(),
            module.data.actionDuration * module.cropMultiplier);

        if (job == null) return false;

        module.pendingJob = job;
        module.scanIndex = nextCursor;
        return true;
    }

    /// <summary>
    /// Wie stark diese Sorte die Station ausbremst. Nur fuer die Automatik — der Spieler
    /// hat mit PlantType.actionSpeedMultiplier seinen eigenen, davon unabhaengigen Wert.
    ///
    /// Die Hacke kennt keine Sorte (auf der Kachel waechst noch nichts) und bleibt neutral.
    /// </summary>
    private static float ResolveCropMultiplier(AutomationModule module, List<Vector2Int> tiles)
    {
        var tool = module.ExecutesTool;

        if (tool == ToolType.Seed)
            return module.seed != null
                ? Mathf.Clamp(module.seed.automationDurationMultiplier, 0.3f, 3f)
                : 1f;

        if (tool != ToolType.WateringCan && tool != ToolType.Scythe) return 1f;

        // Gemischte Zielkacheln: Durchschnitt ueber die betroffenen Pflanzen, genau wie
        // CropActionSpeedMultiplier es beim Spieler macht.
        float sum = 0f;
        int count = 0;

        foreach (var tile in tiles)
        {
            var cell = GridManager.Instance?.GetCell(tile.x, tile.y);
            if (cell?.Plant?.Type == null) continue;

            sum += Mathf.Clamp(cell.Plant.Type.automationDurationMultiplier, 0.3f, 3f);
            count++;
        }

        return count > 0 ? sum / count : 1f;
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
    /// <summary>Modul eingebaut — nur ueber die Popup-Aktion, nicht beim Laden
    /// (der Ladepfad geht ueber RestoreModules). Fuers Missions-System.</summary>
    public static event System.Action<AutomationDeviceType> OnModuleInstalledStatic;

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

        OnModuleInstalledStatic?.Invoke(data.deviceType);
        return module;
    }

    /// <summary>
    /// Nimmt der Station ihre Module ab und gibt sie zurueck — fuer das Einpacken.
    /// Level, An/Aus-Zustand und Sortenwahl bleiben in den Objekten erhalten; nur die
    /// Anbauteile in der Welt werden abgeraeumt.
    /// </summary>
    public List<AutomationModule> DetachModules()
    {
        var detached = new List<AutomationModule>(modules);

        foreach (var module in detached)
        {
            if (module == null) continue;

            if (module.attachment != null) Destroy(module.attachment);
            module.attachment = null;

            // Laufzeitzustand faellt weg, die Einstellungen bleiben.
            module.pendingJob = null;
            module.scanIndex = 0;
            module.idle = false;
        }

        modules.Clear();
        return detached;
    }

    /// <summary>Setzt eingelagerte Module wieder ein — Gegenstueck zu DetachModules.</summary>
    public void RestoreModules(List<AutomationModule> restored)
    {
        if (restored == null) return;

        foreach (var module in restored)
        {
            if (module?.data == null) continue;
            if (HasModule(module.data.deviceType)) continue;

            module.attachment = null;
            module.pendingJob = null;
            module.scanIndex = 0;
            modules.Add(module);
        }

        RefreshAttachments();
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

    /// <summary>Reichweite der Station aufgewertet. Fuers Missions-System.</summary>
    public static event System.Action<int> OnStationUpgradedStatic;

    /// <summary>Hebt die Reichweite um eine Stufe. Gold bucht der Aufrufer ab.</summary>
    public bool TryUpgrade()
    {
        if (stationData == null || level >= stationData.maxLevel) return false;
        SetLevel(level + 1);
        OnStationUpgradedStatic?.Invoke(level);
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

    // ── Anzeige ueber der Station ─────────────────────────────────────────────

    /// <summary>
    /// Zeigt ueber der Station, WELCHE Sorte im Saat-Modul steckt und wie viele Samen davon
    /// noch im Inventar sind.
    ///
    /// Bewusst statt eines Fortschrittsrings: der Takt ist keine Information, auf die man
    /// reagieren kann — er laeuft ohnehin weiter. Der Samenvorrat dagegen ist der einzige
    /// Grund, aus dem die Kette von selbst stehenbleibt, und das soll man von weitem sehen.
    /// </summary>
    private void UpdateSeedDisplay()
    {
        var seeder = GetSeedModule();
        bool show = seeder != null && seeder.seed != null && seeder.enabled;

        if (!show)
        {
            if (seedDisplay != null) seedDisplay.SetActive(false);
            return;
        }

        EnsureSeedDisplay();
        if (seedDisplay == null) return;

        seedDisplay.SetActive(true);
        seedDisplay.transform.position = GetStatusPosition();

        // Billboard: die Anzeige dreht sich immer zur Kamera, sonst steht sie schraeg im Bild.
        var cam = Camera.main;
        if (cam != null)
            seedDisplay.transform.rotation = cam.transform.rotation;

        if (seedIconImage != null)
        {
            seedIconImage.sprite = seeder.seed.icon;
            seedIconImage.enabled = seeder.seed.icon != null;
        }

        if (seedCountLabel != null)
        {
            int count = PlayerInventory.Instance != null
                ? PlayerInventory.Instance.GetSeedCount(seeder.seed)
                : 0;

            seedCountLabel.text = count.ToString();
            seedCountLabel.color = count > 0 ? countColor : emptyCountColor;
        }
    }

    /// <summary>Das Saat-Modul dieser Station, oder null.</summary>
    private AutomationModule GetSeedModule()
    {
        foreach (var module in modules)
            if (module != null && module.data != null && module.NeedsSeed) return module;

        return null;
    }

    /// <summary>
    /// Baut die Anzeige einmalig als World-Space-Canvas. Kein Prefab noetig — die Anzeige
    /// besteht aus genau zwei Elementen und waere im Inspector nur Pflegeaufwand.
    /// </summary>
    private void EnsureSeedDisplay()
    {
        if (seedDisplay != null) return;

        seedDisplay = new GameObject("SeedDisplay", typeof(Canvas));
        seedDisplay.transform.SetParent(transform, false);

        var canvas = seedDisplay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        // Ohne eigene Sortierung verschwindet die Anzeige hinter den Kachel-Highlights und
        // den Fortschrittsringen: die haengen alle in derselben Transparenz-Warteschlange,
        // und wer dort zuletzt gezeichnet wird, gewinnt.
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        var rect = seedDisplay.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(104f, 46f);
        rect.localScale = Vector3.one * Mathf.Max(0.001f, statusScale);

        // Hintergrund, damit Icon und Zahl nicht im Ackerbraun untergehen.
        var bgObj = new GameObject("Background", typeof(RectTransform));
        bgObj.transform.SetParent(seedDisplay.transform, false);
        var bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;

        var bgImage = bgObj.AddComponent<Image>();
        bgImage.sprite = backgroundSprite;
        bgImage.type = backgroundSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        bgImage.color = backgroundColor;
        bgImage.raycastTarget = false;

        // Icon links
        var iconObj = new GameObject("Icon", typeof(RectTransform));
        iconObj.transform.SetParent(seedDisplay.transform, false);
        var iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(-24f, 0f);
        iconRect.sizeDelta = new Vector2(34f, 34f);

        seedIconImage = iconObj.AddComponent<Image>();
        seedIconImage.preserveAspect = true;
        seedIconImage.raycastTarget = false;

        // Zahl rechts
        var countObj = new GameObject("Count", typeof(RectTransform));
        countObj.transform.SetParent(seedDisplay.transform, false);
        var countRect = countObj.GetComponent<RectTransform>();
        countRect.anchorMin = countRect.anchorMax = new Vector2(0.5f, 0.5f);
        countRect.anchoredPosition = new Vector2(20f, 0f);
        countRect.sizeDelta = new Vector2(56f, 40f);

        seedCountLabel = countObj.AddComponent<TextMeshProUGUI>();
        seedCountLabel.fontSize = countFontSize;
        seedCountLabel.fontStyle = FontStyles.Bold;
        seedCountLabel.alignment = TextAlignmentOptions.Left;
        seedCountLabel.raycastTarget = false;
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

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Auf das Komposter-3D-Objekt legen. Klick verhält sich je nach Zustand unterschiedlich:
///
///  • <b>Leer</b>       — öffnet ein Bestätigungsfenster mit einer Zeile PRO Frucht-Sorte
///                         im Inventar. Der Spieler wählt selbst, wie viel von welcher Sorte
///                         reinwandert — z.B. Karotten kompostieren, aber die Sonnenblumen
///                         für den Verkauf behalten.
///  • <b>Brodelt noch</b> — öffnet ein kleines Status-Fenster (Fortschrittsbalken + Restzeit).
///                         Von dort aus: "Schließen" (brodelt einfach weiter) oder "Abbrechen"
///                         (Vorgang stoppt, die reingeworfene Ernte kommt komplett zurück ins
///                         Inventar — kein Dünger). Zusätzlich läuft über dem Komposter
///                         DERSELBE Fortschritts-Indikator wie bei den Pflanzen (identisches
///                         Prefab + Shader wie <see cref="PlantStatusOverlay"/>, statusPrefab).
///  • <b>Fertig</b>     — spielt einmalig einen Benachrichtigungs-Sound (UiSfx.CompostReady).
///                         Klick sammelt den Dünger ein (PlayerInventory.AddFertilizer,
///                         eigener Sound UiSfx.FertilizerCollected) und setzt den Komposter
///                         zurück.
///
/// Bewusst EIN Batch zur Zeit statt einer Warteschlange: das deckt sich mit dem Save-Format
/// (FarmSaveManager: composterBrewing/-TimeRemaining/-FertilizerYield). Was in den Batch
/// reinkommt, ist aber voll wählbar — pro Sorte eine Zeile mit -/+ Stepper, Default 0
/// (nichts vorausgewählt, der Spieler entscheidet bewusst was reingeht statt versehentlich
/// die Verkaufsware zu verlieren).
///
/// Setup: auf das Komposter-Objekt in der SampleScene legen (Collider für den Klick nötig,
/// wie bei BarnInteraction). Optional: dieselben MenuClean-Sprites wie beim Schmied-Popup
/// auf confirmPanelSprite/confirmOkButtonSprite/confirmCancelButtonSprite ziehen, sonst
/// fällt das Popup auf Flachfarben zurück.
/// </summary>
public class ComposterInteraction : MonoBehaviour, IClickable
{
    public static ComposterInteraction Instance { get; private set; }

    public bool IsBrewing { get; private set; }
    public float TimeRemaining { get; private set; }
    public int PendingFertilizerYield { get; private set; }

    /// <summary>Gesamtdauer des laufenden Brauvorgangs — Basis für den Fortschrittsring
    /// (TimeRemaining allein sagt nichts über den Anteil, der schon vorbei ist).</summary>
    public float TotalBrewTime { get; private set; }

    /// <summary>Brauvorgang fertig, wartet nur noch aufs Abholen.</summary>
    public bool IsReadyToCollect => IsBrewing && TimeRemaining <= 0f;

    /// <summary>Was aktuell im Topf ist, pro Sorte — Grundlage für die volle Rückerstattung
    /// bei "Abbrechen". Nur lesend nach außen, siehe StartBrewing/CancelBrewing.</summary>
    public IReadOnlyDictionary<PlantType, int> BrewingComposition => brewingComposition;

    [Header("Ausbeute")]
    [Tooltip("Nenner für die Umrechnung Ernte-Wert → Dünger (abgerundet, mindestens 1 " +
             "sobald überhaupt kompostiert wird). Globaler Balance-Hebel — die tatsächliche " +
             "Sorte zählt zusätzlich mit ihrem PlantType.compostValue (Standard 1.0), das " +
             "einzelne Früchte wertvoller oder weniger wertvoll beim Kompostieren macht.")]
    [SerializeField] private int cropsPerFertilizer = 2;

    [Tooltip("Mindestanzahl Ernte-Items INSGESAMT (über alle Sorten), um überhaupt starten " +
             "zu können.")]
    [SerializeField] private int minCropsToStart = 3;

    [Tooltip("Schrittgröße der +/- Buttons pro Zeile.")]
    [SerializeField] private int stepAmount = 1;

    [Header("Brauzeit")]
    [SerializeField] private float baseBrewTime = 20f;
    [SerializeField] private float perCropBrewTime = 2f;
    [SerializeField] private float maxBrewTime = 180f;

    [Header("Ausbau")]
    [Tooltip("Hoechststufe des Komposters.")]
    [SerializeField] private int maxLevel = 10;

    [Tooltip("Goldkosten fuer die erste Stufe (0 auf 1).")]
    [SerializeField] private int upgradeBaseCost = 150;

    [Tooltip("Zusaetzliche Goldkosten pro Stufe.")]
    [SerializeField] private int upgradeCostPerLevel = 50;

    [Tooltip("Brauzeit pro Frucht auf Hoechststufe. Der Wert oben gilt auf Stufe 0, " +
             "dazwischen wird linear interpoliert.")]
    [SerializeField] private float minPerCropBrewTime = 0.8f;

    [Tooltip("Chance auf einen Extra-Duenger pro Batch auf Hoechststufe, auf Stufe 0 null. " +
             "Wirkt frueh am staerksten, solange Ackerflaeche knapp ist und jeder Duenger " +
             "echtes Land kostet. Spaeter, wenn eine Station das Futter liefert, ist der " +
             "Nachschub ohnehin im Ueberfluss da.")]
    [Range(0f, 1f)]
    [SerializeField] private float maxExtraFertilizerChance = 0.5f;

    /// <summary>Ausbaustufe des Komposters. Wird mitgespeichert.</summary>
    public int Level { get; private set; }

    public int MaxLevel => maxLevel;

    /// <summary>Goldkosten der naechsten Stufe, oder -1 wenn schon am Maximum.</summary>
    public int GetUpgradeCost() =>
        Level >= maxLevel ? -1 : upgradeBaseCost + Level * upgradeCostPerLevel;

    /// <summary>Brauzeit pro Frucht auf der aktuellen Stufe.</summary>
    private float CurrentPerCropBrewTime =>
        maxLevel <= 0 ? perCropBrewTime
                      : Mathf.Lerp(perCropBrewTime, minPerCropBrewTime, Level / (float)maxLevel);

    /// <summary>Chance auf einen Extra-Duenger auf der aktuellen Stufe.</summary>
    public float ExtraFertilizerChance =>
        maxLevel <= 0 ? 0f : Mathf.Lerp(0f, maxExtraFertilizerChance, Level / (float)maxLevel);

    /// <summary>Setzt die Stufe (Ladepfad).</summary>
    public void SetLevel(int level) => Level = Mathf.Clamp(level, 0, maxLevel);

    /// <summary>Hebt den Komposter um eine Stufe. Gold bucht der Aufrufer ab.</summary>
    public bool TryUpgrade()
    {
        if (Level >= maxLevel) return false;
        Level++;
        return true;
    }

    [Header("Bestätigungsfenster — Optik (optional)")]
    [Tooltip("Leer = Flachfarben-Fallback. Für denselben Look wie den Schmied: " +
             "MenuClean_Panel aus Assets/UI/MenuSheetClean.png.")]
    [SerializeField] private Sprite confirmPanelSprite;
    [SerializeField] private Sprite confirmOkButtonSprite;
    [SerializeField] private Sprite confirmCancelButtonSprite;
    [SerializeField] private Canvas hudCanvas;

    private UnityEngine.UI.Button upgradeButton;
    private TextMeshProUGUI upgradeLabel;

    [Header("Status-Anzeige")]
    [Tooltip("DASSELBE Prefab, das PlantStatusOverlay für den Wachstums-Bogen benutzt " +
             "(Quad mit CozyCrops/PlantStatus-Material) — für optische Einheitlichkeit mit " +
             "den Pflanzen. Leer = kein Indikator, aber sonst funktioniert alles normal.")]
    [SerializeField] private GameObject statusPrefab;

    [Tooltip("Abstand über der Objekt-Oberkante (aus den Renderer-Bounds), an dem der " +
             "Indikator hängt.")]
    [SerializeField] private float statusHeightOffset = 0.4f;
    [SerializeField] private Color brewingColor = new(0.7f, 0.55f, 0.9f, 1f);
    [SerializeField] private Color readyColor = new(1f, 0.85f, 0.35f, 1f);

    [Tooltip("Alpha der UNGEFÜLLTEN Ring-Bahn. Das geteilte Material ist hier standardmäßig " +
             "blass (0.18), damit der Bogen über einer Pflanze diese nicht verdeckt — der " +
             "Komposter verdeckt aber nichts, darf also deutlich kräftiger stehen. Wird " +
             "gezielt NUR für diese Instanz per MaterialPropertyBlock überschrieben, " +
             "das geteilte Material/die Pflanzen bleiben unangetastet.")]
    [SerializeField, Range(0f, 1f)] private float trackAlpha = 0.6f;

    [Tooltip("Ringbreite — etwas dicker als bei den Pflanzen (0.055), damit er auf dem " +
             "Komposter-Modell nicht in der Textur untergeht.")]
    [SerializeField, Range(0.01f, 0.2f)] private float ringWidth = 0.09f;

    private GameObject confirmRoot;
    private Transform panelTransform;
    private TextMeshProUGUI confirmText;
    private Button confirmOkButton;

    /// <summary>Wie viel von welcher Sorte im geöffneten Popup aktuell gewählt ist.</summary>
    private readonly Dictionary<PlantType, int> selectedPerType = new();
    private readonly Dictionary<PlantType, TextMeshProUGUI> rowAmountLabels = new();
    private readonly List<GameObject> rowObjects = new();

    /// <summary>Was im LAUFENDEN Batch tatsächlich verbraucht wurde — Grundlage für die
    /// Rückerstattung bei "Abbrechen". Wird in StartBrewing befüllt, in Collect() geleert
    /// (dann gibt's ja Dünger statt Rückgabe) und beim Laden aus dem Save wiederhergestellt.</summary>
    private readonly Dictionary<PlantType, int> brewingComposition = new();

    private GameObject brewingRoot;
    private Image brewingFillImage;
    private TextMeshProUGUI brewingStatusText;

    private GameObject statusInstance;
    private MaterialPropertyBlock statusPropertyBlock;
    private Bounds cachedBounds;
    private bool boundsCached;

    // Dieselben Shader-Property-IDs und Symbol-Werte wie PlantStatusOverlay — müssen exakt
    // matchen, sonst greift das CozyCrops/PlantStatus-Material nicht richtig.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ProgressId  = Shader.PropertyToID("_Progress");
    private static readonly int SymbolId    = Shader.PropertyToID("_Symbol");
    private static readonly int TrackAlphaId = Shader.PropertyToID("_TrackAlpha");
    private static readonly int RingWidthId  = Shader.PropertyToID("_RingWidth");
    private const float SymbolNone    = 0f;
    private const float SymbolSparkle = 2f;

    /// <summary>Verhindert, dass der "fertig"-Sound bei jedem Frame nochmal spielt, solange
    /// der Spieler nicht abholt — und dass er beim Laden eines bereits fertigen Batches
    /// erneut losgeht.</summary>
    private bool readyChimePlayed;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (IsBrewing && TimeRemaining > 0f)
        {
            TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);

            // Genau der Moment, in dem der Timer bei 0 ankommt — einmalig, unabhängig
            // vom Abholen (das klingt separat, siehe Collect()).
            if (TimeRemaining <= 0f && !readyChimePlayed)
            {
                readyChimePlayed = true;
                UiSfx.CompostReady();
            }
        }

        UpdateStatusVisual();

        if (brewingRoot != null && brewingRoot.activeSelf)
        {
            // Fertig geworden während das Fenster offen war: schließen statt veraltete
            // "brodelt noch"-Anzeige stehen zu lassen. Ein erneuter Klick auf den Komposter
            // holt dann über den normalen IsReadyToCollect-Zweig in OnClick() ab.
            if (IsReadyToCollect) HideBrewingPopup();
            else RefreshBrewingPopup();
        }
    }

    public void OnClick()
    {
        if (BuildModeManager.Instance != null && BuildModeManager.Instance.IsActive) return;

        if (IsBrewing)
        {
            if (IsReadyToCollect) Collect();
            else ShowBrewingPopup();
            return;
        }

        ShowConfirmPopup();
    }

    // ── Brauen ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Kauft eine Ausbaustufe. Der Komposter ist die Stellschraube fuers fruehe Spiel:
    /// solange Ackerflaeche knapp ist, kostet jeder Duenger echtes Land — spaeter liefert
    /// eine Automations-Station das Futter ohnehin im Ueberfluss.
    /// </summary>
    private void TryBuyUpgrade()
    {
        int cost = GetUpgradeCost();
        if (cost < 0) return;

        var inventory = PlayerInventory.Instance;
        if (inventory == null || !inventory.TrySpendMoney(cost)) return;

        if (!TryUpgrade())
        {
            inventory.AddMoney(cost);
            return;
        }

        UiSfx.StationUpgraded();
        FarmSaveManager.Instance?.RequestSave();
        RefreshUpgradeButton();
    }

    /// <summary>Beschriftet den Upgrade-Knopf mit Stufe, Kosten und Wirkung.</summary>
    private void RefreshUpgradeButton()
    {
        if (upgradeButton == null || upgradeLabel == null) return;

        int cost = GetUpgradeCost();
        if (cost < 0)
        {
            upgradeLabel.text = $"Komposter Stufe {Level} — Hoechststufe";
            upgradeButton.interactable = false;
            return;
        }

        int money = PlayerInventory.Instance != null ? PlayerInventory.Instance.Money : 0;
        upgradeLabel.text = $"Komposter Stufe {Level} aufwerten — {cost} G";
        upgradeButton.interactable = money >= cost;
    }

    private void StartBrewing()
    {
        HideConfirmPopup();

        if (PlayerInventory.Instance == null) return;

        int totalItems = 0;
        float totalValue = 0f;
        brewingComposition.Clear();

        foreach (var kvp in selectedPerType)
        {
            if (kvp.Value <= 0) continue;
            if (!PlayerInventory.Instance.TryRemoveCrop(kvp.Key, kvp.Value)) continue;

            totalItems += kvp.Value;
            totalValue += kvp.Value * CompostValueOf(kvp.Key);
            brewingComposition[kvp.Key] = kvp.Value;
        }

        if (totalItems < minCropsToStart)
        {
            // Abbruch NACH dem Abziehen (z.B. weil eine Sorte doch nicht die geplante
            // Menge hergab) — die schon entnommene Ernte muss zurück, sonst verschwindet
            // sie ersatzlos.
            RefundComposition();
            return;
        }

        int yield = Mathf.Max(1, Mathf.FloorToInt(totalValue / cropsPerFertilizer));

        // Extra-Duenger einmal pro Batch, nicht pro Frucht — sonst skaliert der Bonus mit
        // der Batchgroesse und ein einziger grosser Wurf waere immer optimal.
        if (Random.value < ExtraFertilizerChance) yield++;

        PendingFertilizerYield = yield;
        TimeRemaining = Mathf.Min(maxBrewTime, baseBrewTime + totalItems * CurrentPerCropBrewTime);
        TotalBrewTime = TimeRemaining;
        IsBrewing = true;
        readyChimePlayed = false;

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();
    }

    private void Collect()
    {
        if (!IsReadyToCollect) return;

        PlayerInventory.Instance?.AddFertilizer(PendingFertilizerYield);
        UiSfx.FertilizerCollected();

        brewingComposition.Clear();
        IsBrewing = false;
        TimeRemaining = 0f;
        TotalBrewTime = 0f;
        PendingFertilizerYield = 0;

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();
    }

    /// <summary>Bricht einen laufenden Batch ab und gibt die komplette reingeworfene Ernte
    /// zurück — kein Dünger, aber auch kein Verlust. Anders als beim Abholen (Collect) gibt's
    /// hier keinen Erfolgs-Sound: der Klick auf "Abbrechen" hat schon seinen eigenen
    /// Button-Klick über UiSfx' automatisches Button-Hooking.</summary>
    private void CancelBrewing()
    {
        if (!IsBrewing) return;

        RefundComposition();

        IsBrewing = false;
        TimeRemaining = 0f;
        TotalBrewTime = 0f;
        PendingFertilizerYield = 0;
        readyChimePlayed = false;

        HideBrewingPopup();

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();
    }

    /// <summary>Gibt brewingComposition komplett ans Inventar zurück und leert es. Eigene
    /// Methode, weil sowohl CancelBrewing als auch der Fehlerpfad in StartBrewing (zu wenig
    /// Ernte für minCropsToStart NACH dem Entnehmen) dasselbe brauchen.</summary>
    private void RefundComposition()
    {
        if (PlayerInventory.Instance != null)
        {
            foreach (var kvp in brewingComposition)
                if (kvp.Key != null && kvp.Value > 0)
                    PlayerInventory.Instance.AddCrop(kvp.Key, kvp.Value);
        }

        brewingComposition.Clear();
    }

    /// <summary>Wird nur vom Save-/Load-System benutzt.</summary>
    public void ApplyLoadedData(bool brewing, float timeRemaining, int pendingYield, float totalBrewTime,
        List<InventoryStackSaveData> composition)
    {
        IsBrewing = brewing;
        TimeRemaining = Mathf.Max(0f, timeRemaining);
        PendingFertilizerYield = Mathf.Max(0, pendingYield);

        // Alte Saves ohne dieses Feld liefern 0 — dann lieber "voll fertig" annehmen
        // (Ring zeigt 100%) als durch 0 zu teilen.
        TotalBrewTime = totalBrewTime > 0f ? totalBrewTime : TimeRemaining;

        // War der Batch beim Speichern schon fertig, soll der Chime nicht sofort beim
        // Laden nochmal losgehen — nur ein frischer 0-Übergang während dieser Session zählt.
        readyChimePlayed = IsReadyToCollect;

        brewingComposition.Clear();
        if (composition != null && PlantDatabase.Instance != null)
        {
            foreach (var stack in composition)
            {
                if (stack == null || stack.amount <= 0) continue;

                PlantType type = PlantDatabase.Instance.GetById(stack.plantId);
                if (type == null) continue;

                brewingComposition[type] = (brewingComposition.TryGetValue(type, out int v) ? v : 0) + stack.amount;
            }
        }
    }

    // ── Bestätigungsfenster ──────────────────────────────────────────────────

    private void ShowConfirmPopup()
    {
        int available = PlayerInventory.Instance != null ? TotalCropCount() : 0;

        if (available < minCropsToStart)
        {
            UiSfx.Denied();
            return;
        }

        EnsureConfirmPopup();
        if (confirmRoot == null) return;

        BuildAmountRows();
        RefreshConfirmText();
        confirmRoot.SetActive(true);
    }

    private void HideConfirmPopup()
    {
        if (confirmRoot != null) confirmRoot.SetActive(false);
    }

    private int TotalCropCount()
    {
        int total = 0;
        foreach (var kvp in PlayerInventory.Instance.GetAllCrops())
            total += kvp.Value;
        return total;
    }

    // ── Status-Fenster während des Brauens ──────────────────────────────────
    //
    // Eigenständiges Popup statt Wiederverwendung von confirmRoot: inhaltlich komplett
    // anders (Fortschrittsbalken + Restzeit statt Sorten-Zeilen), und beide könnten sich
    // sonst nie gleichzeitig sinnvoll abgrenzen. "Schließen" lässt den Vorgang unangetastet
    // weiterlaufen — nur zum Reingucken. "Abbrechen" ist die einzige Stelle, die tatsächlich
    // stoppt und die komplette Ernte zurückgibt (kein Dünger dann).

    private void ShowBrewingPopup()
    {
        EnsureBrewingPopup();
        if (brewingRoot == null) return;

        RefreshBrewingPopup();
        brewingRoot.SetActive(true);
    }

    private void HideBrewingPopup()
    {
        if (brewingRoot != null) brewingRoot.SetActive(false);
    }

    private void RefreshBrewingPopup()
    {
        if (brewingFillImage == null || brewingStatusText == null) return;

        float progress = TotalBrewTime > 0f ? Mathf.Clamp01(1f - TimeRemaining / TotalBrewTime) : 1f;
        brewingFillImage.fillAmount = progress;

        brewingStatusText.text = $"Ergibt: {PendingFertilizerYield} Dünger\n{Mathf.CeilToInt(TimeRemaining)}s verbleibend";
    }

    private void EnsureBrewingPopup()
    {
        if (brewingRoot != null) return;

        Canvas canvas = ResolveHudCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("[ComposterInteraction] Kein Canvas gefunden — Status-Fenster kann nicht gebaut werden.");
            return;
        }

        brewingRoot = RuntimePopupBuilder.CreateUiObject("ComposterBrewingStatus", canvas.transform);
        brewingRoot.transform.SetAsLastSibling();

        Canvas popupCanvas = brewingRoot.AddComponent<Canvas>();
        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = 300;
        brewingRoot.AddComponent<GraphicRaycaster>();

        RectTransform rootRect = brewingRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image blocker = brewingRoot.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.55f);
        blocker.raycastTarget = true;

        GameObject panel = RuntimePopupBuilder.CreateUiObject("Panel", brewingRoot.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(400f, 260f);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.sprite = confirmPanelSprite;
        panelImage.type = confirmPanelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        panelImage.color = confirmPanelSprite != null ? Color.white : new Color(1f, 0.86f, 0.62f, 1f);

        GameObject titleObj = RuntimePopupBuilder.CreateUiObject("Title", panel.transform);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 95f);
        titleRect.sizeDelta = new Vector2(360f, 36f);

        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Komposter brodelt …";
        titleText.fontSize = 20f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.22f, 0.12f, 0.06f, 1f);
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.raycastTarget = false;

        GameObject statusObj = RuntimePopupBuilder.CreateUiObject("StatusText", panel.transform);
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchorMin = statusRect.anchorMax = new Vector2(0.5f, 0.5f);
        statusRect.anchoredPosition = new Vector2(0f, 45f);
        statusRect.sizeDelta = new Vector2(360f, 50f);

        brewingStatusText = statusObj.AddComponent<TextMeshProUGUI>();
        brewingStatusText.fontSize = 16f;
        brewingStatusText.color = new Color(0.22f, 0.12f, 0.06f, 1f);
        brewingStatusText.alignment = TextAlignmentOptions.Center;
        brewingStatusText.textWrappingMode = TextWrappingModes.Normal;
        brewingStatusText.raycastTarget = false;

        // Fortschrittsbalken: Hintergrund + gefüllter Vordergrund übereinander.
        GameObject barBg = RuntimePopupBuilder.CreateUiObject("ProgressBarBg", panel.transform);
        RectTransform barBgRect = barBg.GetComponent<RectTransform>();
        barBgRect.anchorMin = barBgRect.anchorMax = new Vector2(0.5f, 0.5f);
        barBgRect.anchoredPosition = new Vector2(0f, -5f);
        barBgRect.sizeDelta = new Vector2(340f, 24f);

        Image barBgImage = barBg.AddComponent<Image>();
        barBgImage.color = new Color(0.28f, 0.18f, 0.1f, 0.55f);
        barBgImage.raycastTarget = false;

        GameObject barFill = RuntimePopupBuilder.CreateUiObject("ProgressBarFill", barBg.transform);
        RectTransform barFillRect = barFill.GetComponent<RectTransform>();
        barFillRect.anchorMin = Vector2.zero;
        barFillRect.anchorMax = Vector2.one;
        barFillRect.offsetMin = Vector2.zero;
        barFillRect.offsetMax = Vector2.zero;

        brewingFillImage = barFill.AddComponent<Image>();
        brewingFillImage.color = brewingColor;
        brewingFillImage.type = Image.Type.Filled;
        brewingFillImage.fillMethod = Image.FillMethod.Horizontal;
        brewingFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        brewingFillImage.raycastTarget = false;

        // "Abbrechen" (destruktiv, gibt die Ernte zurück) links, "Schließen" (unbedenklich,
        // brodelt einfach weiter) rechts — dieselbe Links/Rechts-Konvention wie beim
        // Upgrade-Bestätigungsfenster im Schmied: rechts ist immer der Weg, der nichts
        // kaputt macht oder rückgängig ist.
        RuntimePopupBuilder.CreateButton(panel.transform, "CancelBrewButton", new Vector2(-95f, -95f), new Vector2(170f, 52f),
            "Abbrechen", confirmCancelButtonSprite, CancelBrewing);

        RuntimePopupBuilder.CreateButton(panel.transform, "CloseButton", new Vector2(95f, -95f), new Vector2(170f, 52f),
            "Schließen", confirmOkButtonSprite, HideBrewingPopup);

        brewingRoot.SetActive(false);
    }

    // ── Zeilen pro Frucht-Sorte ──────────────────────────────────────────────

    /// <summary>Baut für jede Sorte mit Bestand > 0 eine frische Zeile. Wird bei jedem
    /// Öffnen neu aufgebaut, weil sich das Inventar zwischen zwei Besuchen ändert.</summary>
    private void BuildAmountRows()
    {
        ClearAmountRows();

        if (PlayerInventory.Instance == null) return;

        const float rowStartY = 110f;
        const float rowSpacing = 48f;

        int i = 0;
        foreach (var kvp in PlayerInventory.Instance.GetAllCrops())
        {
            PlantType type = kvp.Key;
            int have = kvp.Value;
            if (type == null || have <= 0) continue;

            selectedPerType[type] = 0;
            CreateAmountRow(type, have, rowStartY - i * rowSpacing);
            i++;
        }
    }

    private void ClearAmountRows()
    {
        foreach (var go in rowObjects)
            if (go != null) Destroy(go);

        rowObjects.Clear();
        rowAmountLabels.Clear();
        selectedPerType.Clear();
    }

    private void CreateAmountRow(PlantType type, int have, float y)
    {
        GameObject row = RuntimePopupBuilder.CreateUiObject($"Row_{type.plantName}", panelTransform);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = new Vector2(0f, y);
        rowRect.sizeDelta = new Vector2(400f, 44f);
        rowObjects.Add(row);

        if (type.icon != null)
        {
            GameObject iconObj = RuntimePopupBuilder.CreateUiObject("Icon", row.transform);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-175f, 0f);
            iconRect.sizeDelta = new Vector2(36f, 36f);

            Image iconImage = iconObj.AddComponent<Image>();
            iconImage.sprite = type.icon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
        }

        GameObject nameObj = RuntimePopupBuilder.CreateUiObject("Name", row.transform);
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 0.5f);
        nameRect.anchoredPosition = new Vector2(-80f, 0f);
        nameRect.sizeDelta = new Vector2(150f, 36f);

        TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = $"{type.plantName} ({have})";
        nameText.fontSize = 16f;
        nameText.color = new Color(0.22f, 0.12f, 0.06f, 1f);
        nameText.alignment = TextAlignmentOptions.Left;
        nameText.raycastTarget = false;

        RuntimePopupBuilder.CreateButton(row.transform, "Minus", new Vector2(45f, 0f), new Vector2(34f, 34f),
            "-", confirmCancelButtonSprite, () => AdjustTypeAmount(type, -stepAmount, have));

        GameObject amountObj = RuntimePopupBuilder.CreateUiObject("Amount", row.transform);
        RectTransform amountRect = amountObj.GetComponent<RectTransform>();
        amountRect.anchorMin = amountRect.anchorMax = new Vector2(0.5f, 0.5f);
        amountRect.anchoredPosition = new Vector2(100f, 0f);
        amountRect.sizeDelta = new Vector2(64f, 36f);

        TextMeshProUGUI amountLabel = amountObj.AddComponent<TextMeshProUGUI>();
        amountLabel.fontSize = 16f;
        amountLabel.fontStyle = FontStyles.Bold;
        amountLabel.color = new Color(0.22f, 0.12f, 0.06f, 1f);
        amountLabel.alignment = TextAlignmentOptions.Center;
        amountLabel.raycastTarget = true; // Klick auf die Zahl = Sorte komplett rein/raus

        Button amountButton = amountObj.AddComponent<Button>();
        amountButton.targetGraphic = amountLabel;
        amountButton.onClick.AddListener(() => ToggleTypeMax(type, have));

        rowAmountLabels[type] = amountLabel;

        RuntimePopupBuilder.CreateButton(row.transform, "Plus", new Vector2(155f, 0f), new Vector2(34f, 34f),
            "+", confirmCancelButtonSprite, () => AdjustTypeAmount(type, stepAmount, have));
    }

    private void AdjustTypeAmount(PlantType type, int delta, int have)
    {
        int current = selectedPerType.TryGetValue(type, out int v) ? v : 0;
        selectedPerType[type] = Mathf.Clamp(current + delta, 0, have);
        RefreshConfirmText();
    }

    /// <summary>Klick auf die Zahl selbst: springt zwischen "alles von dieser Sorte" und
    /// "nichts" — schneller als sich mit +1 zur vollen Menge hochzuklicken.</summary>
    private void ToggleTypeMax(PlantType type, int have)
    {
        int current = selectedPerType.TryGetValue(type, out int v) ? v : 0;
        selectedPerType[type] = current >= have ? 0 : have;
        RefreshConfirmText();
    }

    private void RefreshConfirmText()
    {
        // Der Ausbau-Knopf haengt an Gold und Stufe — beides kann sich geaendert haben,
        // seit das Popup zuletzt offen war.
        RefreshUpgradeButton();

        int totalItems = 0;
        float totalValue = 0f;
        foreach (var kvp in selectedPerType)
        {
            totalItems += kvp.Value;
            totalValue += kvp.Value * CompostValueOf(kvp.Key);
        }

        foreach (var kvp in rowAmountLabels)
        {
            int have = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetCropCount(kvp.Key) : 0;
            int sel = selectedPerType.TryGetValue(kvp.Key, out int v) ? v : 0;
            kvp.Value.text = $"{sel}/{have}";
        }

        if (confirmText != null)
        {
            if (totalItems > 0)
            {
                int yield = Mathf.Max(1, Mathf.FloorToInt(totalValue / cropsPerFertilizer));
                float brewSeconds = Mathf.Min(maxBrewTime, baseBrewTime + totalItems * CurrentPerCropBrewTime);
                // Der Bonus ist eine Chance — als "(+1 zu 30%)" statt als glatte Zahl,
                // sonst wirkt ein Batch ohne Bonus wie ein Fehler.
                string bonus = ExtraFertilizerChance > 0f
                    ? $" (+1 zu {ExtraFertilizerChance * 100f:F0}%)"
                    : "";
                
                confirmText.text = $"Ergibt: {yield}{bonus} Dünger\nDauer: {brewSeconds:F0}s";
            }
            else
            {
                confirmText.text = "Wähle mit +/- aus, was in den Komposter soll.";
            }
        }

        if (confirmOkButton != null)
            confirmOkButton.interactable = totalItems >= minCropsToStart;
    }

    /// <summary>Kompost-Wert pro Stück einer Sorte, siehe PlantType.compostValue. Fällt auf
    /// 1.0 zurück (Standard-Sorte) falls type aus irgendeinem Grund null ist.</summary>
    private static float CompostValueOf(PlantType type) =>
        type != null ? Mathf.Max(0.05f, type.compostValue) : 1f;

    // ── Popup-Aufbau ─────────────────────────────────────────────────────────

    private void EnsureConfirmPopup()
    {
        if (confirmRoot != null) return;

        Canvas canvas = ResolveHudCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("[ComposterInteraction] Kein Canvas gefunden — Bestätigungsfenster kann nicht gebaut werden.");
            return;
        }

        confirmRoot = RuntimePopupBuilder.CreateUiObject("ComposterConfirmation", canvas.transform);
        confirmRoot.transform.SetAsLastSibling();

        Canvas popupCanvas = confirmRoot.AddComponent<Canvas>();
        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = 300;
        confirmRoot.AddComponent<GraphicRaycaster>();

        RectTransform rootRect = confirmRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image blocker = confirmRoot.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.55f);
        blocker.raycastTarget = true;

        GameObject panel = RuntimePopupBuilder.CreateUiObject("Panel", confirmRoot.transform);
        panelTransform = panel.transform;
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(460f, 480f);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.sprite = confirmPanelSprite;
        panelImage.type = confirmPanelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        panelImage.color = confirmPanelSprite != null ? Color.white : new Color(1f, 0.86f, 0.62f, 1f);

        GameObject titleObj = RuntimePopupBuilder.CreateUiObject("Title", panel.transform);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 210f);
        titleRect.sizeDelta = new Vector2(400f, 36f);

        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Kompostieren";
        titleText.fontSize = 22f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.22f, 0.12f, 0.06f, 1f);
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.raycastTarget = false;

        GameObject textObj = RuntimePopupBuilder.CreateUiObject("Text", panel.transform);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0f, 175f);
        textRect.sizeDelta = new Vector2(400f, 50f);

        confirmText = textObj.AddComponent<TextMeshProUGUI>();
        confirmText.fontSize = 16f;
        confirmText.color = new Color(0.22f, 0.12f, 0.06f, 1f);
        confirmText.alignment = TextAlignmentOptions.Center;
        confirmText.textWrappingMode = TextWrappingModes.Normal;
        confirmText.raycastTarget = false;

        // Die Sorten-Zeilen selbst kommen erst in BuildAmountRows() — die Menge an
        // Sorten steht erst fest, wenn das Popup tatsächlich geöffnet wird.

        RuntimePopupBuilder.CreateButton(panel.transform, "CancelButton", new Vector2(-95f, -195f), new Vector2(180f, 56f),
            "Abbrechen", confirmCancelButtonSprite, HideConfirmPopup);

        confirmOkButton = RuntimePopupBuilder.CreateButton(panel.transform, "OkButton", new Vector2(95f, -195f), new Vector2(180f, 56f),
            "Kompostieren", confirmOkButtonSprite, StartBrewing);

        upgradeButton = RuntimePopupBuilder.CreateButton(panel.transform, "UpgradeButton", new Vector2(0f, -245f),
            new Vector2(370f, 44f), "", confirmOkButtonSprite, TryBuyUpgrade);
        upgradeLabel = upgradeButton.GetComponentInChildren<TextMeshProUGUI>();

        confirmRoot.SetActive(false);
    }

    /// <summary>
    /// Delegiert an den gemeinsamen Popup-Baukasten. Das im Inspector gesetzte hudCanvas
    /// gewinnt weiterhin, wenn es verdrahtet ist.
    /// </summary>
    private Canvas ResolveHudCanvas() => RuntimePopupBuilder.ResolveHudCanvas(hudCanvas);

    // ── Fortschritts-Indikator über dem Komposter ───────────────────────────
    //
    // Exakt dasselbe Prefab wie PlantStatusOverlay bei den Pflanzen (Quad mit dem
    // CozyCrops/PlantStatus-Material), nur ohne deren Pooling — für ein Einzelstück wie
    // den Komposter lohnt sich das nicht. Gesteuert über dieselben Shader-Properties
    // (_BaseColor/_Progress/_Symbol), damit es optisch ununterscheidbar vom Wachstums-Bogen
    // ist: leerer Bogen der sich füllt, beim Fertigwerden ein Funkeln statt Zahl/Text.

    private void UpdateStatusVisual()
    {
        if (!IsBrewing)
        {
            if (statusInstance != null) statusInstance.SetActive(false);
            return;
        }

        EnsureStatusVisual();
        if (statusInstance == null) return; // kein statusPrefab zugewiesen

        statusInstance.SetActive(true);
        statusInstance.transform.position = GetStatusPosition();
        // Keine manuelle Rotation nötig — das Shader-Billboarding von CozyCrops/PlantStatus
        // dreht das Quad selbst zur Kamera, genau wie bei den Pflanzen.

        var rend = statusInstance.GetComponentInChildren<Renderer>();
        if (rend == null) return;

        bool ready = IsReadyToCollect;
        float progress = ready || TotalBrewTime <= 0f
            ? 1f
            : Mathf.Clamp01(1f - TimeRemaining / TotalBrewTime);

        rend.GetPropertyBlock(statusPropertyBlock);
        statusPropertyBlock.SetColor(BaseColorId, ready ? readyColor : brewingColor);
        statusPropertyBlock.SetFloat(ProgressId, progress);
        statusPropertyBlock.SetFloat(SymbolId, ready ? SymbolSparkle : SymbolNone);
        // Nur diese Instanz überschreiben, nicht das geteilte Material — die Pflanzen
        // sollen bei ihrer blasseren, "verdeckt nichts"-Optik bleiben.
        statusPropertyBlock.SetFloat(TrackAlphaId, trackAlpha);
        statusPropertyBlock.SetFloat(RingWidthId, ringWidth);
        rend.SetPropertyBlock(statusPropertyBlock);
    }

    private void EnsureStatusVisual()
    {
        if (statusInstance != null || statusPrefab == null) return;

        statusInstance = Instantiate(statusPrefab, transform);
        statusPropertyBlock = new MaterialPropertyBlock();
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

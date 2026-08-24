using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// EINE Popup-Instanz, die alle Stationen bedient — <see cref="Show"/> tauscht nur das Ziel
/// aus. Damit entkommt man der Singleton-Annahme des Komposters: die UI ist eine, die
/// Stationen sind viele.
///
/// Aufbau: oben die Station (Reichweite + Aufwerten), darunter eine Zeile je Modultyp.
/// Ein noch nicht eingebautes Modul zeigt seinen Kaufpreis, ein eingebautes sein Level und
/// die Aufwertung. Die Sortenwahl erscheint nur beim Saat-Modul.
///
/// Der Aufbau entsteht zur Laufzeit über <see cref="RuntimePopupBuilder"/>, wie beim
/// Komposter — kein Prefab, das im Inspector gepflegt werden müsste.
/// </summary>
public class AutomationDevicePopup : MonoBehaviour
{
    public static AutomationDevicePopup Instance { get; private set; }

    [Header("Optik")]
    [Tooltip("Leer lassen — wird über HotbarUI aufgeloest.")]
    [SerializeField] private Canvas hudCanvas;

    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite buttonSprite;

    [Tooltip("Farbe der Reichweiten-Vorschau, solange das Popup offen ist.")]
    [SerializeField] private Color rangeColor = new(0.45f, 0.8f, 1f, 0.5f);

    /// <summary>Eine Zeile im Popup — gehört zu genau einem Modultyp.</summary>
    private class ModuleRow
    {
        public AutomationDeviceData data;
        public TextMeshProUGUI titleLabel;
        public Button actionButton;
        public TextMeshProUGUI actionLabel;
        public Button toggleButton;
        public TextMeshProUGUI toggleLabel;
        public Button seedButton;
        public TextMeshProUGUI seedLabel;
        public Image seedIcon;
        public GameObject root;
    }

    private AutomationDevice target;

    private GameObject panel;
    private TextMeshProUGUI headerLabel;
    private TextMeshProUGUI nextLevelLabel;
    private TextMeshProUGUI statusLabel;
    private TextMeshProUGUI upgradeLabel;
    private Button upgradeButton;
    private readonly List<ModuleRow> rows = new();

    public bool IsOpen => panel != null && panel.activeSelf;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Öffnen und Schließen ──────────────────────────────────────────────────

    public void Show(AutomationDevice station)
    {
        if (station == null || station.Data == null) return;

        target = station;
        EnsurePanel();
        if (panel == null) return;

        panel.SetActive(true);
        UiSfx.PanelOpen();
        Refresh();
    }

    public void Close()
    {
        bool wasOpen = IsOpen;
        target = null;

        if (panel != null) panel.SetActive(false);
        if (wasOpen) UiSfx.PanelClose();
        AoEPreview.Instance?.ClearExternalPreview();
    }

    // ── Inhalt ────────────────────────────────────────────────────────────────

    private void Refresh()
    {
        if (target == null || target.Data == null) { Close(); return; }

        var data = target.Data;
        int level = target.Level;
        int side = data.GetSideLength(level);

        headerLabel.text = $"{ResolveName(data)}\nStufe {level} · {side}×{side} · {data.GetTileCount(level)} Kacheln";

        int cost = data.GetUpgradeCost(level);
        if (cost < 0)
        {
            upgradeLabel.text = "Höchststufe";
            upgradeButton.interactable = false;
            nextLevelLabel.text = "";
        }
        else
        {
            int money = PlayerInventory.Instance != null ? PlayerInventory.Instance.Money : 0;
            upgradeLabel.text = $"Reichweite aufwerten — {cost} G";
            upgradeButton.interactable = money >= cost;

            int nextSide = data.GetSideLength(level + 1);
            string change = $"→ Stufe {level + 1}: {nextSide}×{nextSide} · {data.GetTileCount(level + 1)} Kacheln";

            var milestone = data.GetMilestoneAt(level + 1);
            if (milestone != null && !string.IsNullOrWhiteSpace(milestone.unlockText))
                change += $"\n{milestone.unlockText}";

            nextLevelLabel.text = change;
        }

        foreach (var row in rows)
            RefreshRow(row);

        statusLabel.text = ResolveStatusText();

        // Reichweite sichtbar lassen, solange das Popup offen ist — ein eigener Knopf
        // "Radius anzeigen" eruebrigt sich dadurch.
        AoEPreview.Instance?.SetExternalPreview(target.TargetTiles, rangeColor);
    }

    private void RefreshRow(ModuleRow row)
    {
        if (row?.data == null) return;

        var module = target.GetModule(row.data.deviceType);
        int money = PlayerInventory.Instance != null ? PlayerInventory.Instance.Money : 0;

        if (module == null)
        {
            row.titleLabel.text = $"{ResolveName(row.data)} — nicht eingebaut";
            row.actionLabel.text = $"Einbauen — {row.data.buyPrice} G";
            row.actionButton.interactable = money >= row.data.buyPrice;

            row.toggleButton.gameObject.SetActive(false);
            row.seedButton.gameObject.SetActive(false);
            return;
        }

        row.titleLabel.text = $"{ResolveName(row.data)} · Stufe {module.level} · alle {module.Interval:0.#}s";

        int cost = row.data.GetUpgradeCost(module.level);
        if (cost < 0)
        {
            row.actionLabel.text = "Höchststufe";
            row.actionButton.interactable = false;
        }
        else
        {
            row.actionLabel.text = $"Aufwerten — {cost} G";
            row.actionButton.interactable = money >= cost;
        }

        row.toggleButton.gameObject.SetActive(true);
        row.toggleLabel.text = module.enabled ? "An" : "Aus";

        bool needsSeed = module.NeedsSeed;
        row.seedButton.gameObject.SetActive(needsSeed);
        if (needsSeed) ApplySeedButton(row, module.seed);
    }

    /// <summary>
    /// Erklärt, warum gerade nichts passiert. Ohne diese Zeile wirkt ein still
    /// wiederholender Versuch — etwa bei leerem Saatgut — wie ein Defekt.
    /// </summary>
    private string ResolveStatusText()
    {
        if (target.Modules.Count == 0)
            return "Noch kein Modul eingebaut.";

        foreach (var module in target.Modules)
        {
            if (module?.data == null || !module.enabled) continue;

            if (module.NeedsSeed)
            {
                if (module.seed == null) return "Saat-Modul: keine Sorte gewählt.";

                int seeds = PlayerInventory.Instance != null
                    ? PlayerInventory.Instance.GetSeedCount(module.seed)
                    : 0;

                if (seeds <= 0) return $"Keine {ResolveSeedName(module.seed)}-Samen.";
            }
        }

        // Vollständig ist die Kette erst mit allen vier Modulen — sonst laeuft sie nach
        // einer Runde tot, weil Harvest die Kachel auf ungehackt zurücksetzt.
        int installed = 0;
        foreach (var m in target.Modules)
            if (m?.data != null) installed++;

        if (installed < rows.Count)
            return $"{installed} von {rows.Count} Modulen — die Kette schließt sich erst mit allen.";

        return "";
    }

    private static string ResolveName(AutomationStationData data) =>
        string.IsNullOrWhiteSpace(data.displayName) ? "Automations-Station" : data.displayName;

    private static string ResolveName(AutomationDeviceData data) =>
        string.IsNullOrWhiteSpace(data.displayName) ? data.deviceType.ToString() : data.displayName;

    /// <summary>
    /// Zeigt die gewaehlte Sorte als Sprite. Der Text bleibt nur als Rueckfall stehen —
    /// wenn keine Sorte gewaehlt ist, oder die Sorte kein Icon hat.
    /// </summary>
    private static void ApplySeedButton(ModuleRow row, PlantType seed)
    {
        var sprite = seed != null ? seed.icon : null;

        if (row.seedIcon != null)
        {
            row.seedIcon.sprite = sprite;
            row.seedIcon.enabled = sprite != null;
        }

        if (row.seedLabel == null) return;

        if (sprite != null) row.seedLabel.text = "";
        else                row.seedLabel.text = seed != null ? ResolveSeedName(seed) : "Sorte?";
    }

    private static string ResolveSeedName(PlantType seed) =>
        seed == null ? "—" : (string.IsNullOrWhiteSpace(seed.plantName) ? seed.name : seed.plantName);

    // ── Aktionen ──────────────────────────────────────────────────────────────

    private void OnUpgradeStationClicked()
    {
        if (target?.Data == null) return;

        int cost = target.Data.GetUpgradeCost(target.Level);
        if (cost < 0) return;

        var inventory = PlayerInventory.Instance;
        if (inventory == null || !inventory.TrySpendMoney(cost)) return;

        if (!target.TryUpgrade())
        {
            inventory.AddMoney(cost);
            return;
        }

        UiSfx.StationUpgraded();
        FarmSaveManager.Instance?.RequestSave();
        Refresh();
    }

    /// <summary>Einbauen, wenn das Modul fehlt — sonst aufwerten.</summary>
    private void OnModuleActionClicked(AutomationDeviceData data)
    {
        if (target == null || data == null) return;

        var inventory = PlayerInventory.Instance;
        if (inventory == null) return;

        var module = target.GetModule(data.deviceType);

        if (module == null)
        {
            if (!inventory.TrySpendMoney(data.buyPrice)) return;

            if (target.InstallModule(data) == null)
            {
                inventory.AddMoney(data.buyPrice);
                return;
            }

            UiSfx.ModuleInstalled();
        }
        else
        {
            int cost = data.GetUpgradeCost(module.level);
            if (cost < 0) return;
            if (!inventory.TrySpendMoney(cost)) return;

            if (!target.TryUpgradeModule(data.deviceType))
            {
                inventory.AddMoney(cost);
                return;
            }

            UiSfx.StationUpgraded();
        }

        FarmSaveManager.Instance?.RequestSave();
        Refresh();
    }

    private void OnModuleToggleClicked(AutomationDeviceData data)
    {
        var module = target?.GetModule(data.deviceType);
        if (module == null) return;

        module.enabled = !module.enabled;
        if (!module.enabled) module.cooldown = 0f;

        UiSfx.ModuleToggled();

        FarmSaveManager.Instance?.RequestSave();
        Refresh();
    }

    private void OnModuleSeedClicked(AutomationDeviceData data)
    {
        var module = target?.GetModule(data.deviceType);
        if (module == null || SeedDropdownUI.Instance == null) return;

        var station = target;
        SeedDropdownUI.Instance.Open(seed =>
        {
            module.seed = seed;
            FarmSaveManager.Instance?.RequestSave();

            if (target == station) Refresh();
        }, module.seed);
    }

    private void OnMoveClicked()
    {
        if (target == null) return;

        var station = target;
        Close();
        AutomationPlacementController.Instance?.BeginMove(station);
    }

    /// <summary>
    /// Legt die Station ins Lager — samt Modulen, deren Leveln und der Sortenwahl. Kein
    /// Gold zurueck: der Wert steckt weiter in der eingelagerten Station, sie laesst sich
    /// im Baumodus kostenlos wieder aufstellen.
    ///
    /// Zum blossen Umsetzen ist "Verschieben" gedacht — das ist ein Schritt statt zwei.
    /// </summary>
    private void OnPackUpClicked()
    {
        if (target?.Data == null) return;

        AutomationDeviceManager.Instance?.Pack(target);
        UiSfx.StationPacked();
        FarmSaveManager.Instance?.RequestSave();
        Close();
    }

    // ── Aufbau ────────────────────────────────────────────────────────────────

    private void EnsurePanel()
    {
        if (panel != null) return;

        var canvas = RuntimePopupBuilder.ResolveHudCanvas(hudCanvas);
        if (canvas == null) return;

        var modules = AutomationDeviceCatalog.Modules;

        // Das Panel wird genau EINMAL gebaut und danach nur noch neu befuellt. Waere der
        // Katalog jetzt noch leer, blieben die Modulzeilen dauerhaft weg — also lieber gar
        // nicht bauen und beim naechsten Show erneut versuchen.
        if (modules.Count == 0)
        {
            Debug.LogWarning("[Automation] Im AutomationDeviceCatalog sind keine Module " +
                             "hinterlegt — das Stations-Popup bleibt leer.");
            return;
        }

        float panelHeight = 250f + modules.Count * 62f;

        panel = RuntimePopupBuilder.CreatePanel(canvas.transform, "AutomationStationPopup",
                                                new Vector2(420f, panelHeight), panelSprite);

        float top = panelHeight * 0.5f;
        float y = top - 44f;

        headerLabel = RuntimePopupBuilder.CreateLabel(panel.transform, "Header",
            new Vector2(0f, y), new Vector2(380f, 56f), "", 20f);
        headerLabel.fontStyle = FontStyles.Bold;
        y -= 52f;

        nextLevelLabel = RuntimePopupBuilder.CreateLabel(panel.transform, "NextLevel",
            new Vector2(0f, y), new Vector2(380f, 42f), "", 14f);
        y -= 40f;

        upgradeButton = RuntimePopupBuilder.CreateButton(panel.transform, "UpgradeStation",
            new Vector2(0f, y), new Vector2(376f, 36f), "", buttonSprite, OnUpgradeStationClicked);
        upgradeLabel = upgradeButton.GetComponentInChildren<TextMeshProUGUI>();
        y -= 48f;

        // Eine Zeile je Modultyp aus dem Katalog — nicht je eingebautem Modul, damit der
        // Spieler sieht, was noch fehlt.
        foreach (var data in modules)
        {
            if (data == null) continue;
            y -= CreateModuleRow(data, y);
        }

        y -= 8f;
        RuntimePopupBuilder.CreateButton(panel.transform, "Move", new Vector2(-96f, y),
            new Vector2(180f, 34f), "Verschieben", buttonSprite, OnMoveClicked);
        RuntimePopupBuilder.CreateButton(panel.transform, "PackUp", new Vector2(96f, y),
            new Vector2(180f, 34f), "Einpacken", buttonSprite, OnPackUpClicked);
        y -= 34f;

        statusLabel = RuntimePopupBuilder.CreateLabel(panel.transform, "Status",
            new Vector2(0f, y), new Vector2(380f, 28f), "", 13f);
        statusLabel.color = new Color(0.6f, 0.25f, 0.1f, 1f);

        RuntimePopupBuilder.CreateButton(panel.transform, "Close",
            new Vector2(182f, top - 20f), new Vector2(34f, 34f), "X", buttonSprite, Close);

        panel.SetActive(false);
    }

    /// <summary>Baut eine Modulzeile und gibt ihre Höhe zurück.</summary>
    private float CreateModuleRow(AutomationDeviceData data, float y)
    {
        var row = new ModuleRow { data = data };

        row.titleLabel = RuntimePopupBuilder.CreateLabel(panel.transform, $"Row_{data.deviceType}_Title",
            new Vector2(0f, y), new Vector2(380f, 22f), "", 14f);
        row.titleLabel.alignment = TextAlignmentOptions.Left;

        float buttonY = y - 24f;
        var captured = data;

        row.actionButton = RuntimePopupBuilder.CreateButton(panel.transform, $"Row_{data.deviceType}_Action",
            new Vector2(-70f, buttonY), new Vector2(232f, 30f), "", buttonSprite,
            () => OnModuleActionClicked(captured));
        row.actionLabel = row.actionButton.GetComponentInChildren<TextMeshProUGUI>();

        row.toggleButton = RuntimePopupBuilder.CreateButton(panel.transform, $"Row_{data.deviceType}_Toggle",
            new Vector2(78f, buttonY), new Vector2(60f, 30f), "", buttonSprite,
            () => OnModuleToggleClicked(captured));
        row.toggleLabel = row.toggleButton.GetComponentInChildren<TextMeshProUGUI>();

        row.seedButton = RuntimePopupBuilder.CreateButton(panel.transform, $"Row_{data.deviceType}_Seed",
            new Vector2(152f, buttonY), new Vector2(84f, 30f), "", buttonSprite,
            () => OnModuleSeedClicked(captured));
        row.seedLabel = row.seedButton.GetComponentInChildren<TextMeshProUGUI>();

        // Icon-Flaeche ueber dem Knopf. raycastTarget aus, sonst schluckt sie den Klick.
        var iconObj = RuntimePopupBuilder.CreateUiObject("Icon", row.seedButton.transform);
        var iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(26f, 26f);

        row.seedIcon = iconObj.AddComponent<Image>();
        row.seedIcon.preserveAspect = true;
        row.seedIcon.raycastTarget = false;
        row.seedIcon.enabled = false;

        rows.Add(row);
        return 62f;
    }
}

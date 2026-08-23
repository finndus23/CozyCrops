using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// EINE Popup-Instanz, die alle Geräte bedient — <see cref="Show"/> tauscht nur das Ziel aus.
///
/// Damit entkommt man der Singleton-Annahme des Komposters: die UI ist eine, die Geräte sind
/// viele. Das Popup hält deshalb keinen eigenen Zustand über das Ziel hinaus und liest bei
/// jedem Show neu.
///
/// Der Aufbau entsteht zur Laufzeit über <see cref="RuntimePopupBuilder"/>, wie beim
/// Komposter — kein Prefab, das im Inspector gepflegt werden müsste.
/// </summary>
public class AutomationDevicePopup : MonoBehaviour
{
    public static AutomationDevicePopup Instance { get; private set; }

    [Header("Optik")]
    [Tooltip("Leer lassen — wird über HotbarUI aufgeloest. Nur setzen, wenn das Popup in " +
             "einem anderen Canvas landen soll.")]
    [SerializeField] private Canvas hudCanvas;

    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite buttonSprite;

    [Tooltip("Farbe der Reichweiten-Vorschau, solange das Popup offen ist. Ein eigener " +
             "Knopf 'Radius anzeigen' eruebrigt sich dadurch.")]
    [SerializeField] private Color rangeColor = new(0.45f, 0.8f, 1f, 0.5f);

    private AutomationDevice target;

    private GameObject panel;
    private TextMeshProUGUI headerLabel;
    private TextMeshProUGUI nextLevelLabel;
    private TextMeshProUGUI statusLabel;
    private TextMeshProUGUI upgradeLabel;
    private TextMeshProUGUI toggleLabel;
    private TextMeshProUGUI seedLabel;
    private Button upgradeButton;
    private GameObject seedRow;

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

    public void Show(AutomationDevice device)
    {
        if (device == null || device.Data == null) return;

        target = device;
        EnsurePanel();
        if (panel == null) return;

        panel.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        target = null;

        if (panel != null) panel.SetActive(false);
        AoEPreview.Instance?.ClearExternalPreview();
    }

    // ── Inhalt ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Schreibt alle Texte neu und legt die Reichweiten-Vorschau. Wird nach jeder Aktion
    /// aufgerufen, weil sich mit dem Level auch Radius und Takt ändern.
    /// </summary>
    private void Refresh()
    {
        if (target == null || target.Data == null) { Close(); return; }

        var data = target.Data;
        int level = target.Level;
        int side = data.GetSideLength(level);

        headerLabel.text = $"{ResolveName(data)}\nStufe {level} · {side}×{side} · alle {data.GetInterval(level):0.#}s";

        // Upgrade
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
            upgradeLabel.text = $"Aufwerten — {cost} G";
            upgradeButton.interactable = money >= cost;

            // Vorher/Nachher, damit der Spieler sieht, wofür er zahlt.
            int nextSide = data.GetSideLength(level + 1);
            float nextInterval = data.GetInterval(level + 1);
            var milestone = data.GetMilestoneAt(level + 1);

            string change = $"→ Stufe {level + 1}: {nextSide}×{nextSide} · alle {nextInterval:0.#}s";
            if (milestone != null && !string.IsNullOrWhiteSpace(milestone.unlockText))
                change += $"\n{milestone.unlockText}";

            nextLevelLabel.text = change;
        }

        toggleLabel.text = target.IsEnabled ? "Läuft — ausschalten" : "Aus — einschalten";

        // Sortenwahl nur bei der Sämaschine.
        bool isSeeder = data.executesTool == ToolType.Seed;
        if (seedRow != null) seedRow.SetActive(isSeeder);

        if (isSeeder)
            seedLabel.text = target.Seed != null ? $"Sorte: {ResolveSeedName(target.Seed)}" : "Sorte: keine gewählt";

        statusLabel.text = ResolveStatusText(isSeeder);

        // Reichweite sichtbar lassen, solange das Popup offen ist.
        AoEPreview.Instance?.SetExternalPreview(target.TargetTiles, rangeColor);
    }

    /// <summary>
    /// Erklärt, warum ein Gerät gerade nichts tut. Ohne diese Zeile wirkt ein still
    /// wiederholender Versuch — etwa bei leerem Saatgut — wie ein Defekt.
    /// </summary>
    private string ResolveStatusText(bool isSeeder)
    {
        if (!target.IsEnabled) return "Ausgeschaltet.";

        if (isSeeder)
        {
            if (target.Seed == null) return "Keine Sorte gewählt.";

            int seeds = PlayerInventory.Instance != null
                ? PlayerInventory.Instance.GetSeedCount(target.Seed)
                : 0;

            if (seeds <= 0) return $"Keine {ResolveSeedName(target.Seed)}-Samen.";
        }

        return "";
    }

    private static string ResolveName(AutomationDeviceData data) =>
        string.IsNullOrWhiteSpace(data.displayName) ? data.deviceType.ToString() : data.displayName;

    private static string ResolveSeedName(PlantType seed) =>
        seed == null ? "—" : (string.IsNullOrWhiteSpace(seed.plantName) ? seed.name : seed.plantName);

    // ── Aktionen ──────────────────────────────────────────────────────────────

    private void OnUpgradeClicked()
    {
        if (target == null || target.Data == null) return;

        int cost = target.Data.GetUpgradeCost(target.Level);
        if (cost < 0) return;

        var inventory = PlayerInventory.Instance;
        if (inventory == null || !inventory.TrySpendMoney(cost)) return;

        if (!target.TryUpgrade())
        {
            inventory.AddMoney(cost);   // Aufwertung nicht moeglich → Gold zurueck
            return;
        }

        FarmSaveManager.Instance?.RequestSave();
        Refresh();
    }

    private void OnToggleClicked()
    {
        if (target == null) return;

        target.SetEnabled(!target.IsEnabled);
        FarmSaveManager.Instance?.RequestSave();
        Refresh();
    }

    private void OnSeedClicked()
    {
        if (target == null || SeedDropdownUI.Instance == null) return;

        var device = target;
        SeedDropdownUI.Instance.Open(seed =>
        {
            device.SetSeed(seed);
            FarmSaveManager.Instance?.RequestSave();

            if (target == device) Refresh();
        }, device.Seed);
    }

    private void OnMoveClicked()
    {
        if (target == null) return;

        var device = target;
        Close();
        AutomationPlacementController.Instance?.BeginMove(device);
    }

    /// <summary>
    /// Notausgang, nicht der normale Weg zum Umstellen — dafür gibt es "Verschieben",
    /// das kostenlos ist und das Level behält.
    /// </summary>
    private void OnPackUpClicked()
    {
        if (target == null || target.Data == null) return;

        int refund = target.Data.buyPrice / 2;
        PlayerInventory.Instance?.AddMoney(refund);

        AutomationDeviceManager.Instance?.Remove(target);
        FarmSaveManager.Instance?.RequestSave();
        Close();
    }

    // ── Aufbau ────────────────────────────────────────────────────────────────

    private void EnsurePanel()
    {
        if (panel != null) return;

        var canvas = RuntimePopupBuilder.ResolveHudCanvas(hudCanvas);
        if (canvas == null) return;

        panel = RuntimePopupBuilder.CreatePanel(canvas.transform, "AutomationDevicePopup",
                                                new Vector2(380f, 340f), panelSprite);

        headerLabel = RuntimePopupBuilder.CreateLabel(panel.transform, "Header",
            new Vector2(0f, 128f), new Vector2(340f, 60f), "", 20f);
        headerLabel.fontStyle = FontStyles.Bold;

        nextLevelLabel = RuntimePopupBuilder.CreateLabel(panel.transform, "NextLevel",
            new Vector2(0f, 78f), new Vector2(340f, 46f), "", 15f);

        upgradeLabel = ButtonWithLabel("Upgrade", new Vector2(0f, 32f), OnUpgradeClicked, out upgradeButton);
        toggleLabel = ButtonWithLabel("Toggle", new Vector2(0f, -14f), OnToggleClicked, out _);

        var seedButtonLabel = ButtonWithLabel("Seed", new Vector2(0f, -60f), OnSeedClicked, out var seedButton);
        seedLabel = seedButtonLabel;
        seedRow = seedButton.gameObject;

        ButtonWithLabel("Move", new Vector2(-88f, -106f), OnMoveClicked, out _, width: 160f)
            .text = "Verschieben";
        ButtonWithLabel("PackUp", new Vector2(88f, -106f), OnPackUpClicked, out _, width: 160f)
            .text = "Einpacken";

        statusLabel = RuntimePopupBuilder.CreateLabel(panel.transform, "Status",
            new Vector2(0f, -142f), new Vector2(340f, 30f), "", 14f);
        statusLabel.color = new Color(0.6f, 0.25f, 0.1f, 1f);

        RuntimePopupBuilder.CreateButton(panel.transform, "Close", new Vector2(162f, 148f),
            new Vector2(36f, 36f), "X", buttonSprite, Close);

        panel.SetActive(false);
    }

    private TextMeshProUGUI ButtonWithLabel(string name, Vector2 position,
        UnityEngine.Events.UnityAction action, out Button button, float width = 336f)
    {
        button = RuntimePopupBuilder.CreateButton(panel.transform, name, position,
                                                  new Vector2(width, 38f), "", buttonSprite, action);
        return button.GetComponentInChildren<TextMeshProUGUI>();
    }
}

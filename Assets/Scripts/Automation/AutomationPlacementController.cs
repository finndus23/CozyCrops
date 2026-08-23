using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Setzt Automatik-Geräte auf die Farm — beim Kauf und beim Verschieben.
///
/// Kaufen und Platzieren sind derselbe Vorgang: Gold wird erst abgezogen, wenn das Gerät
/// wirklich steht. Ein Abbruch kostet deshalb nichts, und es braucht keinen unplatzierten
/// Bestand, den irgendwer verwalten müsste.
///
/// Verschieben ist kostenlos und behält das Level: es läuft über denselben Controller, nur
/// mit einer bestehenden Instanz statt einem Prefab.
/// </summary>
public class AutomationPlacementController : MonoBehaviour
{
    public static AutomationPlacementController Instance { get; private set; }

    [Header("Vorschau-Farben")]
    [Tooltip("Zielkachel, wenn dort gesetzt werden darf.")]
    [SerializeField] private Color validColor = new(0.35f, 1f, 0.45f, 0.75f);

    [Tooltip("Zielkachel, wenn dort nicht gesetzt werden darf.")]
    [SerializeField] private Color invalidColor = new(1f, 0.3f, 0.3f, 0.75f);

    [Tooltip("Reichweite rund um die Zielkachel — zeigt, welche Flaeche das Geraet spaeter bedient.")]
    [SerializeField] private Color rangeColor = new(0.45f, 0.8f, 1f, 0.4f);

    private enum PlacementMode { None, Buy, Move }

    private PlacementMode mode;
    private AutomationDeviceData pendingData;
    private AutomationDevice movingDevice;
    private Vector2Int moveOrigin;

    private readonly List<Vector2Int> previewTiles = new();
    private int lastPreviewX = int.MinValue;
    private int lastPreviewZ = int.MinValue;
    private bool lastPreviewValid;
    private bool hasPreview;

    public bool IsPlacing => mode != PlacementMode.None;

    /// <summary>Das Gerät, das gerade verschoben wird — null beim Kauf.</summary>
    public AutomationDevice MovingDevice => movingDevice;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnEnable()
    {
        BuildModeManager.OnBuildModeExitedStatic += HandleBuildModeExited;
    }

    void OnDisable()
    {
        BuildModeManager.OnBuildModeExitedStatic -= HandleBuildModeExited;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void HandleBuildModeExited()
    {
        if (mode == PlacementMode.Buy) Cancel();
    }

    // ── Starten ───────────────────────────────────────────────────────────────

    /// <summary>Kauf-Modus: ein neues Gerät dieses Typs setzen.</summary>
    public void BeginBuy(AutomationDeviceData data)
    {
        if (data == null) { Cancel(); return; }

        Cancel();
        pendingData = data;
        mode = PlacementMode.Buy;
        InvalidatePreview();
    }

    /// <summary>
    /// Verschiebe-Modus. Das Gerät bleibt sichtbar stehen, bis der Spieler ein neues Ziel
    /// bestätigt — bei Abbruch geht es an seinen alten Platz zurück.
    /// </summary>
    public void BeginMove(AutomationDevice device)
    {
        if (device == null || device.Data == null) { Cancel(); return; }

        Cancel();
        movingDevice = device;
        pendingData = device.Data;
        moveOrigin = device.TilePosition;
        mode = PlacementMode.Move;
        InvalidatePreview();
    }

    // ── Eingabe ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Wird von GridInput aufgerufen, solange platziert wird — vor dem Baumodus-Zweig,
    /// damit das Setzen eines Geräts nicht als Kachel-Bemalen durchgeht.
    /// </summary>
    public void HandleInput(Mouse mouse, int hoveredX, int hoveredZ, bool isOverGrid)
    {
        if (!IsPlacing) return;

        bool valid = isOverGrid && IsValidPlacement(hoveredX, hoveredZ);
        UpdatePreview(hoveredX, hoveredZ, isOverGrid, valid);

        var keyboard = Keyboard.current;
        if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            || (mouse != null && mouse.rightButton.wasPressedThisFrame))
        {
            Cancel();
            return;
        }

        if (mouse != null && mouse.leftButton.wasPressedThisFrame && valid)
            Confirm(hoveredX, hoveredZ);
    }

    // ── Gültigkeit ────────────────────────────────────────────────────────────

    /// <summary>
    /// Erlaubter Untergrund ist nur Weg und Gras, ausdrücklich NICHT Ackerland.
    ///
    /// Das hält die Anbaufläche frei, ergibt als Layout ein Wegenetz mit Maschinen daran und
    /// umgeht jede Wechselwirkung mit IsTilled, Plant und Harvest() auf der Kachel, auf der
    /// das Gerät selbst steht.
    /// </summary>
    public bool IsValidPlacement(int x, int z)
    {
        if (pendingData == null) return false;

        var grid = GridManager.Instance;
        if (grid == null || !grid.IsInBounds(x, z)) return false;

        var cell = grid.GetCell(x, z);
        if (cell == null || cell.IsLocked) return false;
        if (cell.Type != TileType.Path && cell.Type != TileType.Grass) return false;
        if (cell.HasPlant) return false;

        // Beim Verschieben ist die eigene Ausgangskachel natürlich frei.
        var occupant = AutomationDeviceManager.At(x, z);
        if (occupant != null && occupant != movingDevice) return false;

        // Gold nur beim Kauf. Verschieben ist kostenlos.
        if (mode == PlacementMode.Buy)
        {
            int money = PlayerInventory.Instance != null ? PlayerInventory.Instance.Money : 0;
            if (money < pendingData.buyPrice) return false;
        }

        return true;
    }

    // ── Abschluss ─────────────────────────────────────────────────────────────

    private void Confirm(int x, int z)
    {
        if (mode == PlacementMode.Buy)
        {
            var inventory = PlayerInventory.Instance;
            if (inventory == null || !inventory.TrySpendMoney(pendingData.buyPrice)) return;

            var manager = AutomationDeviceManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[Automation] Kein AutomationDeviceManager in der Szene. " +
                                 "Die Komponente gehoert auf das PlantManager-GameObject.");
                inventory.AddMoney(pendingData.buyPrice);
                return;
            }

            var device = manager.Spawn(pendingData, x, z);
            if (device == null)
            {
                // Spawn fehlgeschlagen — Gold zurück, sonst zahlt der Spieler für nichts.
                // Die genaue Ursache steht als Warnung aus Spawn() in der Konsole.
                inventory.AddMoney(pendingData.buyPrice);
                return;
            }
        }
        else if (mode == PlacementMode.Move)
        {
            var manager = AutomationDeviceManager.Instance;
            if (manager == null || !manager.Move(movingDevice, x, z)) return;
        }

        FarmSaveManager.Instance?.RequestSave();
        Finish();
    }

    /// <summary>Bricht ab. Beim Kauf kostet das nichts, beim Verschieben geht das Gerät zurück.</summary>
    public void Cancel()
    {
        if (mode == PlacementMode.Move && movingDevice != null)
            AutomationDeviceManager.Instance?.Move(movingDevice, moveOrigin.x, moveOrigin.y);

        Finish();
    }

    private void Finish()
    {
        mode = PlacementMode.None;
        pendingData = null;
        movingDevice = null;
        previewTiles.Clear();
        hasPreview = false;

        AoEPreview.Instance?.ClearExternalPreview();

        // Slot-Hervorhebung in der Hotbar wieder loesen, sonst sieht das Geraet nach dem
        // Setzen oder Abbrechen weiter ausgewaehlt aus.
        BuildModeManager.Instance?.ClearDeviceSelection();
    }

    // ── Vorschau ──────────────────────────────────────────────────────────────

    private void InvalidatePreview()
    {
        lastPreviewX = int.MinValue;
        lastPreviewZ = int.MinValue;
        hasPreview = false;
    }

    /// <summary>
    /// Zeigt die Reichweite um die Zielkachel, die Zielkachel selbst grün oder rot.
    /// Wird nur neu gebaut, wenn sich Kachel oder Gültigkeit geändert haben.
    /// </summary>
    private void UpdatePreview(int x, int z, bool isOverGrid, bool valid)
    {
        if (!isOverGrid)
        {
            if (hasPreview)
            {
                AoEPreview.Instance?.ClearExternalPreview();
                InvalidatePreview();
            }

            return;
        }

        if (hasPreview && x == lastPreviewX && z == lastPreviewZ && valid == lastPreviewValid)
            return;

        lastPreviewX = x;
        lastPreviewZ = z;
        lastPreviewValid = valid;
        hasPreview = true;

        BuildRangeTiles(x, z);
        AoEPreview.Instance?.SetExternalPreview(previewTiles, rangeColor,
                                                new Vector2Int(x, z),
                                                valid ? validColor : invalidColor);
    }

    /// <summary>
    /// Reichweite auf Stufe 0 rund um die Zielkachel, plus die Zielkachel selbst.
    /// Kacheln außerhalb des Gitters fallen weg — die Vorschau zeigt damit genau das, was
    /// das Gerät später wirklich erreicht, statt eines geschönten Rechtecks.
    /// </summary>
    private void BuildRangeTiles(int centerX, int centerZ)
    {
        previewTiles.Clear();

        var grid = GridManager.Instance;
        if (grid == null || pendingData == null) return;

        int level = movingDevice != null ? movingDevice.Level : 0;
        int radius = pendingData.GetRadius(level);

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                int x = centerX + dx;
                int z = centerZ + dz;
                if (!grid.IsInBounds(x, z)) continue;

                previewTiles.Add(new Vector2Int(x, z));
            }
        }
    }
}

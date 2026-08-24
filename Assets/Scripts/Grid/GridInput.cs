using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class GridInput : MonoBehaviour
{
    [SerializeField] [Range(0.3f, 0.95f)] private float hoverDarken = 0.65f;

    // Für AoEPreview von außen lesbar
    public static int  HoveredX       { get; private set; }
    public static int  HoveredZ       { get; private set; }
    public static bool IsHoveringGrid { get; private set; }

    private Camera cam;
    private bool isDragging;
    private int hoveredX, hoveredZ;
    private bool isOverGrid;
    private bool isLockedTile;

    // Hover-Tracking für Material-Tint
    private Renderer hoveredRenderer;
    private Material cachedOriginalMaterial;
    private int lastHoverX = -1, lastHoverZ = -1;
    private bool lastHoverVisible = false;

    // Drag-Tracking für Farm-Modus
    private int lastToolX = -1, lastToolZ = -1;
    private int lastBuildX = -1, lastBuildZ = -1;

    void Start()
    {
        cam = Camera.main;
        if (ToolUseHandler.Instance != null)
            ToolUseHandler.Instance.OnCastCompleted += () => { lastToolX = -1; lastToolZ = -1; };
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if ((InventoryUI.Instance != null && InventoryUI.Instance.IsOpen)
            || (GameSceneMenuController.Instance != null && GameSceneMenuController.Instance.IsConfirmationOpen))
        {
            IsHoveringGrid = false;
            UpdateHover(false);
            return;
        }

        Vector2 screenPos = mouse.position.ReadValue();
        isOverGrid = TryGetGridPosition(screenPos, out hoveredX, out hoveredZ);
        isLockedTile = isOverGrid && (GridManager.Instance.GetCell(hoveredX, hoveredZ)?.IsLocked ?? false);

        HoveredX       = hoveredX;
        HoveredZ       = hoveredZ;
        IsHoveringGrid = isOverGrid && !isLockedTile;

        // Geraete-Platzierung hat Vorrang vor dem Baumodus: sonst wuerde derselbe Klick
        // zusaetzlich als Kachel-Bemalen durchgehen.
        var placement = AutomationPlacementController.Instance;
        if (placement != null && placement.IsPlacing)
        {
            UpdateHover(false);
            placement.HandleInput(mouse, hoveredX, hoveredZ, isOverGrid && !isLockedTile);
            return;
        }

        // Build-Modus: Tiles platzieren (bestehende Logik)
        if (BuildModeManager.Instance.IsActive)
        {
            UpdateHover(isOverGrid && !isLockedTile);
            HandleBuildPlacement(mouse);
            HandleEscape();
            return;
        }

        // Farm-Modus: AoEPreview übernimmt das Tile-Highlighting
        UpdateHover(false);

        if (mouse.leftButton.isPressed && isOverGrid && !isLockedTile
            && TutorialManager.Instance?.IsBlocked(TutorialBlockedAction.FarmTools) != true)
        {
            // Mit Queueing darf während einer laufenden Aktion weiter markiert werden —
            // die neuen Felder landen in der Warteschlange statt verworfen zu werden.
            // Ohne Queueing bleibt das alte Verhalten: solange ein Cast läuft, passiert nichts.
            bool acceptsInput = GameSettings.ActionQueueingEnabled
                                || !ToolUseHandler.Instance.IsCasting;

            if (acceptsInput && (hoveredX != lastToolX || hoveredZ != lastToolZ))
            {
                lastToolX = hoveredX;
                lastToolZ = hoveredZ;
                ToolUseHandler.Instance.TryStartUse(hoveredX, hoveredZ, Hotbar.Instance.ActiveTool);
            }
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            lastToolX = -1;
            lastToolZ = -1;
            // Eingereihte Aktionen laufen immer bis zum Ende — kein Cancel bei Maus-Release
        }

        HandleQueueCancel();
    }

    /// <summary>
    /// Leert die Warteschlange. Bewusst NICHT auf Escape: das öffnet bereits das
    /// Pausenmenü (GameSceneMenuController), man würde die Queue also jedes Mal beim
    /// Pausieren mit wegwerfen.
    ///
    /// Laufende Aktionen bleiben stehen — die sind schon angefangen, ein Abbruch
    /// mittendrin fühlt sich nach Verlust an.
    /// </summary>
    void HandleQueueCancel()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.qKey.wasPressedThisFrame) return;

        ToolUseHandler.Instance?.ClearQueue();
    }

    void HandleBuildPlacement(Mouse mouse)
    {
        if (mouse.leftButton.isPressed && isOverGrid && !isLockedTile && !IsPointerOverUI())
        {
            if (hoveredX != lastBuildX || hoveredZ != lastBuildZ)
            {
                lastBuildX = hoveredX;
                lastBuildZ = hoveredZ;

                TileType type = BuildModeManager.Instance.SelectedTileType;
                if (GridManager.Instance.TryApplyTile(hoveredX, hoveredZ, type))
                    TileContextMenu.NotifyTilePlaced(type);
            }
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            lastBuildX = -1;
            lastBuildZ = -1;
        }
    }

    void HandleSelection(Mouse mouse)
    {
        if (mouse.leftButton.wasPressedThisFrame && isOverGrid && !isLockedTile && !IsPointerOverUI())
        {
            isDragging = true;
            SelectionManager.Instance.StartSelection(hoveredX, hoveredZ);
        }

        if (isDragging && mouse.leftButton.isPressed && isOverGrid && !isLockedTile)
            SelectionManager.Instance.AddToSelection(hoveredX, hoveredZ);

        if (mouse.leftButton.wasReleasedThisFrame)
            isDragging = false;
    }

    bool IsPointerOverUI() =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    void HandleContextMenu(Mouse mouse, Vector2 screenPos)
    {
        if (mouse.rightButton.wasPressedThisFrame
            && TutorialManager.Instance?.IsBlocked(TutorialBlockedAction.ContextMenu) != true)
            TileContextMenu.Instance?.Show(screenPos);
    }

    void HandleEscape()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            SelectionManager.Instance.ClearSelection();
            TileContextMenu.Instance?.Hide();
        }
    }

    void UpdateHover(bool visible)
    {
        // Nur neu berechnen wenn sich Tile oder Sichtbarkeit geändert hat
        if (visible == lastHoverVisible && hoveredX == lastHoverX && hoveredZ == lastHoverZ) return;

        lastHoverVisible = visible;
        lastHoverX = hoveredX;
        lastHoverZ = hoveredZ;

        SetHoverVisible(visible);
    }

    void SetHoverVisible(bool visible)
    {
        // Vorheriges Tile wiederherstellen
        if (hoveredRenderer != null)
        {
            hoveredRenderer.material = cachedOriginalMaterial;
            hoveredRenderer        = null;
            cachedOriginalMaterial = null;
        }

        if (!visible) return;

        var tileObj = GridManager.Instance.GetTileObject(hoveredX, hoveredZ);
        if (tileObj == null) return;

        var rend = tileObj.GetComponentInChildren<Renderer>();
        if (rend == null) return;

        cachedOriginalMaterial = rend.sharedMaterial;
        hoveredRenderer        = rend;

        // Material-Instanz erstellen und Farbe abdunkeln
        var mat   = rend.material;
        var color = mat.color;
        mat.color = new Color(color.r * hoverDarken, color.g * hoverDarken, color.b * hoverDarken, color.a);
    }

    bool TryGetGridPosition(Vector2 screenPos, out int x, out int z)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        Plane groundPlane = new Plane(Vector3.up, GridManager.Instance.transform.position);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 worldPos = ray.GetPoint(distance);
            return GridManager.Instance.WorldToGrid(worldPos, out x, out z);
        }

        x = z = 0;
        return false;
    }
}

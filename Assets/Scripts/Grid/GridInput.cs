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

        Vector2 screenPos = mouse.position.ReadValue();
        isOverGrid = TryGetGridPosition(screenPos, out hoveredX, out hoveredZ);
        isLockedTile = isOverGrid && (GridManager.Instance.GetCell(hoveredX, hoveredZ)?.IsLocked ?? false);

        HoveredX       = hoveredX;
        HoveredZ       = hoveredZ;
        IsHoveringGrid = isOverGrid && !isLockedTile;

        // Build-Modus: Tiles platzieren (bestehende Logik)
        if (BuildModeManager.Instance.IsActive)
        {
            UpdateHover(isOverGrid && !isLockedTile);
            HandleSelection(mouse);
            HandleContextMenu(mouse, screenPos);
            HandleEscape();
            return;
        }

        // Farm-Modus: AoEPreview übernimmt das Tile-Highlighting
        UpdateHover(false);

        if (mouse.leftButton.isPressed && isOverGrid && !isLockedTile
            && TutorialManager.Instance?.IsBlocked(TutorialBlockedAction.FarmTools) != true)
        {
            // Während eines Casts auf dieselbe Tile gelockt bleiben
            if (!ToolUseHandler.Instance.IsCasting)
            {
                if (hoveredX != lastToolX || hoveredZ != lastToolZ)
                {
                    lastToolX = hoveredX;
                    lastToolZ = hoveredZ;
                    ToolUseHandler.Instance.TryStartUse(hoveredX, hoveredZ, Hotbar.Instance.ActiveTool);
                }
            }
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            lastToolX = -1;
            lastToolZ = -1;
            // Cast läuft immer bis zum Ende — kein Cancel bei Maus-Release
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

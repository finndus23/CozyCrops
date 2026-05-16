using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class GridInput : MonoBehaviour
{
    [SerializeField] [Range(0.3f, 0.95f)] private float hoverDarken = 0.65f;

    private Camera cam;
    private bool isDragging;
    private int hoveredX, hoveredZ;
    private bool isOverGrid;
    private bool isLockedTile;

    // Hover-Tracking für Material-Tint
    private Renderer hoveredRenderer;
    private Material cachedOriginalMaterial;  // für Grass/Path-Tiles
    private FarmTileVisual hoveredTileVisual; // für FarmPlot-Tiles

    // Drag-Tracking für Farm-Modus
    private int lastToolX = -1, lastToolZ = -1;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 screenPos = mouse.position.ReadValue();
        isOverGrid = TryGetGridPosition(screenPos, out hoveredX, out hoveredZ);
        isLockedTile = isOverGrid && (GridManager.Instance.GetCell(hoveredX, hoveredZ)?.IsLocked ?? false);

        // Build-Modus: Tiles platzieren (bestehende Logik)
        if (BuildModeManager.Instance.IsActive)
        {
            SetHoverVisible(isOverGrid && !isLockedTile);
            HandleSelection(mouse);
            HandleContextMenu(mouse, screenPos);
            HandleEscape();
            return;
        }

        // Farm-Modus: Tools benutzen
        SetHoverVisible(isOverGrid && !isLockedTile && Hotbar.Instance.ActiveTool != ToolType.None);

        if (mouse.leftButton.isPressed && isOverGrid && !isLockedTile)
        {
            if (hoveredX != lastToolX || hoveredZ != lastToolZ)
            {
                lastToolX = hoveredX;
                lastToolZ = hoveredZ;
                HandleToolUse(hoveredX, hoveredZ);
            }
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            lastToolX = -1;
            lastToolZ = -1;
        }
    }

    void HandleToolUse(int x, int z)
    {
        switch (Hotbar.Instance.ActiveTool)
        {
            case ToolType.Hoe:
                PlantManager.Instance.TryTill(x, z);
                break;

            case ToolType.Seed:
                var seed = Hotbar.Instance.SelectedSeed;
                if (seed != null)
                    PlantManager.Instance.TryPlant(x, z, seed);
                break;

            case ToolType.WateringCan:
                PlantManager.Instance.TryWater(x, z);
                break;

            case ToolType.Scythe:
                PlantManager.Instance.TryHarvest(x, z);
                break;
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
        if (mouse.rightButton.wasPressedThisFrame)
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

    void SetHoverVisible(bool visible)
    {
        // Vorheriges Tile wiederherstellen
        if (hoveredRenderer != null)
        {
            if (hoveredTileVisual != null)
                hoveredTileVisual.RestoreMaterial();            // FarmPlot: State-Material zurück
            else
                hoveredRenderer.material = cachedOriginalMaterial; // Grass/Path: Original zurück

            hoveredRenderer        = null;
            hoveredTileVisual      = null;
            cachedOriginalMaterial = null;
        }

        if (!visible) return;

        var tileObj = GridManager.Instance.GetTileObject(hoveredX, hoveredZ);
        if (tileObj == null) return;

        var rend = tileObj.GetComponentInChildren<Renderer>();
        if (rend == null) return;

        hoveredRenderer   = rend;
        hoveredTileVisual = tileObj.GetComponent<FarmTileVisual>();

        // Für Grass/Path: Original-Material cachen bevor wir eine Instanz erstellen
        if (hoveredTileVisual == null)
            cachedOriginalMaterial = rend.sharedMaterial;

        // Material-Instanz erstellen und Farbe abdunkeln
        var mat   = rend.material; // erstellt automatisch eine Instanz
        var color = mat.color;
        mat.color = new Color(color.r * hoverDarken, color.g * hoverDarken, color.b * hoverDarken, color.a);
    }

    bool TryGetGridPosition(Vector2 screenPos, out int x, out int z)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 worldPos = ray.GetPoint(distance);
            return GridManager.Instance.WorldToGrid(worldPos, out x, out z);
        }

        x = z = 0;
        return false;
    }
}

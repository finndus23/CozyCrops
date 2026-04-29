using UnityEngine;
using UnityEngine.InputSystem;

public class GridInput : MonoBehaviour
{
    [SerializeField] private GameObject hoverHighlight;

    private Camera cam;
    private bool isDragging;
    private int hoveredX, hoveredZ;
    private bool isOverGrid;

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

        if (!BuildModeManager.Instance.IsActive)
        {
            SetHoverVisible(false);
            return;
        }

        SetHoverVisible(isOverGrid);
        HandleSelection(mouse);
        HandleContextMenu(mouse, screenPos);
        HandleEscape();
    }

    void HandleSelection(Mouse mouse)
    {
        if (mouse.leftButton.wasPressedThisFrame && isOverGrid)
        {
            isDragging = true;
            SelectionManager.Instance.StartSelection(hoveredX, hoveredZ);
        }

        if (isDragging && mouse.leftButton.isPressed && isOverGrid)
            SelectionManager.Instance.AddToSelection(hoveredX, hoveredZ);

        if (mouse.leftButton.wasReleasedThisFrame)
            isDragging = false;
    }

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
        if (hoverHighlight == null) return;
        hoverHighlight.SetActive(visible);
        if (visible)
            hoverHighlight.transform.position = GridManager.Instance.GridToWorld(hoveredX, hoveredZ);
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

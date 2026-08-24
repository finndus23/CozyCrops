using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Zentraler Raycast-Handler für klickbare 3D-Objekte.
/// Einmal in der Scene — alle IClickable-Objekte werden automatisch erkannt.
/// </summary>
public class WorldClickHandler : MonoBehaviour
{
    public static WorldClickHandler Instance { get; private set; }

    [SerializeField] private float maxDistance = 100f;

    private Camera cam;
    private HighlightTarget hoveredTarget;

    void Awake()
    {
        Instance = this;
        cam = Camera.main;
    }

    void Update()
    {
        var mouse = Mouse.current;
        bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen || mouse == null || pointerOverUi)
        {
            SetHoveredTarget(null);
            return;
        }

        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            SetHoveredTarget(null);
            return;
        }

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, maxDistance);
        IClickable clickable = hasHit ? hit.collider.GetComponentInParent<IClickable>() : null;

        SetHoveredTarget(FindHighlightTarget(clickable));

        if (mouse.leftButton.wasPressedThisFrame)
            clickable?.OnClick();
    }

    private static HighlightTarget FindHighlightTarget(IClickable clickable)
    {
        if (clickable is not Component component) return null;

        // Normalfall: Klick- und Highlight-Komponente liegen auf demselben Objekt.
        HighlightTarget target = component.GetComponent<HighlightTarget>();
        if (target != null) return target;

        target = component.GetComponentInParent<HighlightTarget>();
        if (target != null) return target;

        target = component.GetComponentInChildren<HighlightTarget>();
        if (target != null) return target;

        // Die Scheune hat einen unsichtbaren Klick-Collider und das sichtbare Modell als
        // Geschwister unter einem gemeinsamen Parent. Dadurch leuchtet das echte Modell
        // und nicht der große Collider-Würfel.
        return component.transform.parent != null
            ? component.transform.parent.GetComponentInChildren<HighlightTarget>()
            : null;
    }

    private void SetHoveredTarget(HighlightTarget target)
    {
        if (hoveredTarget == target) return;

        if (hoveredTarget != null) hoveredTarget.SetHovered(false);
        hoveredTarget = target;
        if (hoveredTarget != null) hoveredTarget.SetHovered(true);
    }

    private void OnDisable() => SetHoveredTarget(null);
}

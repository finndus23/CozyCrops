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

    void Awake()
    {
        Instance = this;
        cam = Camera.main;
    }

    void Update()
    {
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) return;

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        // UI hat Vorrang — nicht feuern wenn Pointer über UI-Element ist
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance)) return;

        var clickable = hit.collider.GetComponentInParent<IClickable>();
        clickable?.OnClick();
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Auf ein Auto-Objekt legen. Klick auf das Auto → Szenenwechsel.
/// Funktioniert in jeder Scene ohne WorldClickHandler.
/// Destination im Inspector wählen: ToMarket oder ToFarm.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CarClickHandler : MonoBehaviour
{
    public enum Destination { ToMarket, ToFarm }

    [SerializeField] private Destination destination = Destination.ToMarket;
    [SerializeField] private FarmMarketSceneTransition sceneTransition;
    [SerializeField] private float maxRayDistance = 100f;

    private void Awake()
    {
        if (sceneTransition == null)
            sceneTransition = FindFirstObjectByType<FarmMarketSceneTransition>();
    }

    private void Update()
    {
        if (Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance)) return;

        if (hit.collider.gameObject != gameObject && !hit.collider.transform.IsChildOf(transform))
            return;

        if (sceneTransition == null)
        {
            Debug.LogWarning("[CarClickHandler] Kein FarmMarketSceneTransition gefunden.");
            return;
        }

        if (destination == Destination.ToMarket)
            sceneTransition.GoToMarket();
        else
            sceneTransition.GoToFarm();
    }
}

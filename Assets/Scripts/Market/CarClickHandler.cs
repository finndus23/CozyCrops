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

    private HighlightTarget highlightTarget;

    private void Awake()
    {
        if (sceneTransition == null)
            sceneTransition = FindFirstObjectByType<FarmMarketSceneTransition>();

        highlightTarget = GetComponent<HighlightTarget>();
        if (highlightTarget == null) highlightTarget = GetComponentInChildren<HighlightTarget>();
        if (highlightTarget == null) highlightTarget = GetComponentInParent<HighlightTarget>();
    }

    private void Update()
    {
        if (Mouse.current == null)
        {
            SetHovered(false);
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            SetHovered(false);
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            SetHovered(false);
            return;
        }

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, maxRayDistance);
        bool pointsAtCar = hasHit
            && (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform));

        SetHovered(pointsAtCar);

        if (!pointsAtCar || !Mouse.current.leftButton.wasPressedThisFrame) return;

        if (TutorialManager.Instance?.IsBlocked(TutorialBlockedAction.CarTravel) == true) return;

        if (sceneTransition == null)
        {
            Debug.LogWarning("[CarClickHandler] Kein FarmMarketSceneTransition gefunden.");
            return;
        }

        // OnTraveledTo*Static feuert jetzt IN GoToMarket()/GoToFarm() selbst (siehe
        // FarmMarketSceneTransition) — so lösen auch andere Wege dorthin (z.B. ein
        // UI-Button) dieselben Missionsziele/Sounds aus, nicht nur der Autoklick hier.
        if (destination == Destination.ToMarket)
        {
            sceneTransition.GoToMarket();
        }
        else
        {
            sceneTransition.GoToFarm();
        }
    }

    private void SetHovered(bool on)
    {
        if (highlightTarget != null) highlightTarget.SetHovered(on);
    }

    private void OnDisable() => SetHovered(false);
}

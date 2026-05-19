using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Raycast-Klicker für Markt-NPCs. Funktioniert mit altem und neuem Input System.
/// </summary>
public class FarmMarketNpcClickController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera clickCamera;
    [SerializeField] private FarmMarketDialogueShopController dialogueController;

    [Header("Raycast")]
    [SerializeField] private LayerMask npcLayerMask = ~0;
    [SerializeField] private float maxClickDistance = 100f;

    private void Awake()
    {
        if (clickCamera == null)
            clickCamera = Camera.main;

        if (dialogueController == null)
            dialogueController = FindFirstObjectByType<FarmMarketDialogueShopController>();
    }

    private void Update()
    {
        if (dialogueController != null && dialogueController.IsOpen)
            return;

        if (!WasPrimaryClickPressed())
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        TryClickNpc(GetPointerPosition());
    }

    private void TryClickNpc(Vector2 screenPosition)
    {
        if (clickCamera == null || dialogueController == null)
            return;

        Ray ray = clickCamera.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxClickDistance, npcLayerMask))
            return;

        FarmMarketNpc npc = hit.collider.GetComponentInParent<FarmMarketNpc>();
        if (npc == null)
            return;

        dialogueController.Open(npc);
    }

    private bool WasPrimaryClickPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private Vector2 GetPointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();

        return Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }
}

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

    private HighlightTarget hoveredTarget;

    private void Awake()
    {
        if (clickCamera == null)
            clickCamera = Camera.main;

        if (dialogueController == null)
            dialogueController = FindFirstObjectByType<FarmMarketDialogueShopController>();
    }

    private void Update()
    {
        bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (dialogueController != null && dialogueController.IsOpen || pointerOverUi || !HasPointer())
        {
            SetHoveredTarget(null);
            return;
        }

        FarmMarketNpc npc = GetNpcAt(GetPointerPosition());
        SetHoveredTarget(FindHighlightTarget(npc));

        if (!WasPrimaryClickPressed())
            return;

        TryClickNpc(npc);
    }

    private FarmMarketNpc GetNpcAt(Vector2 screenPosition)
    {
        if (clickCamera == null) clickCamera = Camera.main;
        if (clickCamera == null) return null;

        Ray ray = clickCamera.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxClickDistance, npcLayerMask))
            return null;

        return hit.collider.GetComponentInParent<FarmMarketNpc>();
    }

    private void TryClickNpc(FarmMarketNpc npc)
    {
        if (npc == null || dialogueController == null) return;

        // Hat dieser NPC gerade einen Story-Auftrag? Dann erst reden, danach handeln.
        // Der Marktplatz kennt kein IClickable/WorldClickHandler — ohne diesen Haken
        // käme man an den Quest-Dialogen der Markt-NPCs nie vorbei, weil der Klick
        // sofort das Shop-Fenster aufreißt.
        StoryDialogueNpc story = npc.GetComponentInParent<StoryDialogueNpc>();
        if (story != null && story.TryPlayStoryDialogue())
        {
            OpenShopAfterDialogue(npc);
            return;
        }

        dialogueController.Open(npc);
    }

    private static HighlightTarget FindHighlightTarget(FarmMarketNpc npc)
    {
        if (npc == null) return null;

        HighlightTarget target = npc.GetComponent<HighlightTarget>();
        if (target != null) return target;

        target = npc.GetComponentInParent<HighlightTarget>();
        return target != null ? target : npc.GetComponentInChildren<HighlightTarget>();
    }

    private void SetHoveredTarget(HighlightTarget target)
    {
        if (hoveredTarget == target) return;

        if (hoveredTarget != null) hoveredTarget.SetHovered(false);
        hoveredTarget = target;
        if (hoveredTarget != null) hoveredTarget.SetHovered(true);
    }

    private void OnDisable() => SetHoveredTarget(null);

    /// <summary>
    /// Shop aufmachen sobald der Quest-Dialog durch ist — die Aufträge der Markt-NPCs
    /// lauten ja durchweg "kauf/verkauf hier etwas". Ein zweiter Klick wäre nur Reibung.
    /// </summary>
    private void OpenShopAfterDialogue(FarmMarketNpc npc)
    {
        if (DialogueManager.Instance == null)
        {
            dialogueController.Open(npc);
            return;
        }

        void Handler()
        {
            DialogueManager.Instance.OnDialogueEnded -= Handler;
            if (npc != null && dialogueController != null)
                dialogueController.Open(npc);
        }

        DialogueManager.Instance.OnDialogueEnded += Handler;
    }

    private bool WasPrimaryClickPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private bool HasPointer()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null;
#else
        return true;
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

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

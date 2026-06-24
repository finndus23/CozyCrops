using System.Collections;
using UnityEngine;

/// <summary>
/// Tutorial-NPC in der Farm-Scene.
/// Startet beim ersten Spiel (kein Save) automatisch das Tutorial via TutorialManager.
/// Beim Klicken: zeigt einen Wiederhol-Dialog (optional).
/// </summary>
public class TutorialNpc : MonoBehaviour, IClickable
{
    [Tooltip("Dialog wenn der Spieler nach dem Tutorial nochmal mit dem NPC spricht")]
    [SerializeField] private DialogueData repeatDialogue;

    private IEnumerator Start()
    {
        yield return null;
        yield return null;

        Debug.Log($"[TutorialNpc] Start: FarmSaveManager={FarmSaveManager.Instance}, TutorialManager={TutorialManager.Instance}");

        if (FarmSaveManager.Instance == null || TutorialManager.Instance == null) yield break;

        bool saveExists = FarmSaveManager.Instance.SaveExists(FarmSaveManager.Instance.ActiveSlot);
        Debug.Log($"[TutorialNpc] SaveExists={saveExists}, ActiveSlot={FarmSaveManager.Instance.ActiveSlot}");

        if (!saveExists)
            TutorialManager.Instance.BeginTutorial();
    }

    public void OnClick()
    {
        if (DialogueManager.Instance == null) return;
        if (DialogueManager.Instance.IsActive) return;
        if (TutorialManager.Instance?.IsActive == true) return; // Während Tutorial kein Repeat

        if (repeatDialogue != null)
            DialogueManager.Instance.StartDialogue(repeatDialogue);
    }
}

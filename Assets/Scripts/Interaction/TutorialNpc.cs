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

        // Nicht SaveExists() nehmen: seit CreateSlot() existiert die Datei schon beim
        // Anlegen eines neuen Spiels (isInitialized=false). Tutorial soll aber genau dann
        // starten. Deshalb auf "echtes, initialisiertes Spiel" prüfen.
        bool hasInitializedSave = FarmSaveManager.Instance.HasInitializedSave(FarmSaveManager.Instance.ActiveSlot);
        Debug.Log($"[TutorialNpc] HasInitializedSave={hasInitializedSave}, ActiveSlot={FarmSaveManager.Instance.ActiveSlot}");

        if (!hasInitializedSave)
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

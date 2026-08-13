using System.Collections;
using UnityEngine;

/// <summary>
/// Tutorial-Bootstrap in der Farm-Scene.
/// Startet beim ersten Spiel (kein Save) automatisch das Tutorial via TutorialManager.
///
/// <b>Kein IClickable mehr.</b> WorldClickHandler löst per GetComponentInParent&lt;IClickable&gt;()
/// nur EINE Komponente auf — auf dem NPC-Cube lagen aber TutorialNpc und TutorialDialogueRepeat
/// gleichzeitig, und die Komponenten-Reihenfolge entschied still, welche überhaupt reagiert
/// (es gewann TutorialDialogueRepeat, dieses OnClick war also totes Gewicht).
///
/// Das Anklicken übernimmt jetzt komplett <see cref="StoryDialogueNpc"/>: Tutorial-Wiederholung,
/// Story-Dialoge und Smalltalk in einem klaren Vorrang. Der frühere repeatDialogue gehört
/// dort in das Feld 'smallTalkDialogue'.
/// </summary>
public class TutorialNpc : MonoBehaviour
{
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
}

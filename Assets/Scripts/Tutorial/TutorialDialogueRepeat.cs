using UnityEngine;

/// <summary>
/// Auf ein beliebiges klickbares GO legen (IClickable).
/// Wiederholt den Intro-Dialog des aktuellen Tutorial-Schritts,
/// falls der Spieler ihn versehentlich weggeklickt hat.
/// </summary>
public class TutorialDialogueRepeat : MonoBehaviour, IClickable
{
    public void OnClick()
    {
        if (TutorialManager.Instance == null || !TutorialManager.Instance.IsActive) return;
        if (DialogueManager.Instance == null || DialogueManager.Instance.IsActive) return;

        TutorialManager.Instance.RepeatCurrentStepDialogue();
    }
}

using System;
using UnityEngine;

/// <summary>
/// Ein Tutorial-Schritt: entspricht exakt einem Objective im Tutorial-MissionData (selber Index).
/// </summary>
[Serializable]
public class TutorialStep
{
    [Tooltip("Dialog der gezeigt wird wenn dieser Schritt startet")]
    public DialogueData introDialogue;

    [Tooltip("Diese Aktionen sind blockiert während der Spieler diesen Schritt erfüllt")]
    public TutorialBlockedAction[] blockedDuring;
}

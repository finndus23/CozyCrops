using System;
using UnityEngine;

/// <summary>
/// Dialog der mitten in einem Objective ausgelöst wird sobald der Fortschritt einen bestimmten Wert erreicht.
/// Beispiel: Beim 2. Gießen (atProgress=1) sagt der NPC "Gut, noch einmal!"
/// </summary>
[Serializable]
public class TutorialMidDialogue
{
    [Tooltip("Bei welchem Fortschritts-Wert (inkl.) dieser Dialog feuert — 1 = nach dem 1. Mal, 2 = nach dem 2. Mal, ...")]
    public int atProgress = 1;

    public DialogueData dialogue;
}

/// <summary>
/// Ein Tutorial-Schritt: entspricht exakt einem Objective im Tutorial-MissionData (selber Index).
/// </summary>
[Serializable]
public class TutorialStep
{
    [Tooltip("Dialog der gezeigt wird wenn dieser Schritt startet")]
    public DialogueData introDialogue;

    [Tooltip("Dialoge die während des Objectives bei bestimmten Fortschrittswerten ausgelöst werden")]
    public TutorialMidDialogue[] midDialogues;

    [Tooltip("Dialog wenn eine Pflanze eine Wachstumsstufe erreicht (aber noch nicht erntereif ist) — nur einmal pro Schritt")]
    public DialogueData onPlantGrowDialogue;

    [Tooltip("Dialog wenn eine Pflanze vollständig gewachsen und erntereif ist — nur einmal pro Schritt")]
    public DialogueData onPlantFullyGrownDialogue;

    [Tooltip("Wenn aktiv: Intro-Dialog dieses Schritts wird NICHT sofort gespielt, sondern wartet bis eine Pflanze vollständig gewachsen ist")]
    public bool delayIntroUntilPlantGrown;

    [Tooltip("Diese Aktionen sind blockiert während der Spieler diesen Schritt erfüllt")]
    public TutorialBlockedAction[] blockedDuring;

    [Tooltip("Belohnungen die der Spieler erhält wenn dieser Schritt abgeschlossen wird")]
    public MissionReward[] rewards;
}

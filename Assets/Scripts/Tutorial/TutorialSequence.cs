using UnityEngine;

/// <summary>
/// Definiert die Dialogue- und Blocking-Abfolge des Tutorials.
/// Die eigentlichen Objectives (Was muss getan werden) stehen im TutorialMission MissionData Asset.
/// steps[i] entspricht tutorialMission.objectives[i] — gleicher Index!
/// </summary>
[CreateAssetMenu(menuName = "CozyCrops/Tutorial Sequence", fileName = "TutorialSequence")]
public class TutorialSequence : ScriptableObject
{
    [Tooltip("MissionId des Tutorial-MissionData Assets (muss übereinstimmen)")]
    public string tutorialMissionId = "tutorial";

    [Tooltip("Dialogue + Blocking pro Schritt — Index muss mit objectives[] übereinstimmen")]
    public TutorialStep[] steps;

    [Tooltip("Dialog nach Abschluss aller Schritte")]
    public DialogueData completionDialogue;
}

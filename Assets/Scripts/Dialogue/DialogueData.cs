using UnityEngine;

[CreateAssetMenu(menuName = "CozyCrops/Dialogue", fileName = "NewDialogue")]
public class DialogueData : ScriptableObject
{
    public DialogueLine[] lines;

    [Tooltip("Mission die nach Abschluss dieses Dialogs gestartet wird (optional)")]
    public MissionData missionToStartAfter;
}

using UnityEngine;

[CreateAssetMenu(menuName = "CozyCrops/Mission", fileName = "NewMission")]
public class MissionData : ScriptableObject
{
    [Header("Identifikation")]
    public string missionId;
    public string title;
    [TextArea(2, 4)]
    public string description;

    [Header("Aufgaben")]
    public MissionObjectiveData[] objectives;

    [Header("Belohnung")]
    public int rewardMoney;

    [Header("Kette")]
    [Tooltip("Wenn true, wird diese Mission als Teil der Story-Kette behandelt")]
    public bool isStoryMission;

    [Tooltip("Missionen die nach Abschluss dieser Mission freigeschaltet werden")]
    public MissionData[] unlocks;

    [Header("Verhalten")]
    [Tooltip("Objectives müssen in Reihenfolge abgeschlossen werden (nur das erste unvollständige ist aktiv)")]
    public bool sequentialObjectives;
}

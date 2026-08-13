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
    public MissionReward[] rewards;

    [Header("Kette")]
    [Tooltip("Wenn true, wird diese Mission als Teil der Story-Kette behandelt")]
    public bool isStoryMission;

    [Tooltip("Missionen die nach Abschluss dieser Mission freigeschaltet werden")]
    public MissionData[] unlocks;

    [Tooltip("IDs von Missionen die vorher abgeschlossen sein müssen. Leer = sofort verfügbar.\n" +
             "Greift auch für Neben-Missionen — dadurch kann eine Nebenmission an einem " +
             "Story-Fortschritt hängen, ohne selbst Teil der Kette zu sein.")]
    public string[] requiredMissionIds;

    [Tooltip("Mindestanzahl abgeschlossener Missionen bevor diese verfügbar wird. " +
             "Grober Gatekeeper für Nebenmissionen, wenn eine exakte ID-Abhängigkeit zu starr wäre.")]
    public int requiredCompletedCount;

    [Tooltip("Nur für Neben-Missionen: startet automatisch sobald die Voraussetzungen erfüllt sind.\n" +
             "Aus = die Mission muss aktiv vergeben werden (z.B. über einen NPC-Dialog).")]
    public bool autoStartWhenAvailable;

    [Tooltip("Diese Story-Mission wird NICHT automatisch gestartet, sondern erst durch einen " +
             "NPC-Dialog (DialogueData.missionToStartAfter).\n\n" +
             "Ohne das Flag überholt AdvanceStoryChain() den NPC: die Mission liefe schon, " +
             "bevor der Spieler den Dialog überhaupt gesehen hat. Die Kette hält hier an und " +
             "zeigt stattdessen nextStepHint an.")]
    public bool startedByDialogue;

    [Header("Anzeige")]
    [Tooltip("Kurzer Hinweis wohin der Spieler als Nächstes soll. Wird in der Missions-UI " +
             "angezeigt wenn gerade keine Mission aktiv ist.")]
    [TextArea(1, 3)]
    public string nextStepHint;

    [Header("Verhalten")]
    [Tooltip("Objectives müssen in Reihenfolge abgeschlossen werden (nur das erste unvollständige ist aktiv)")]
    public bool sequentialObjectives;
}

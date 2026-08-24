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

    [Tooltip("Zaehlen Aktionen von Automatik-Geraeten fuer diese Mission?\n\n" +
             "Standard aus: sonst hakt eine Maschine abseits vom Bildschirm Tutorial-Ziele " +
             "ab, waehrend der Tutorial-Highlight noch auf etwas Erledigtes zeigt. Ein neuer " +
             "bool ist auf allen bestehenden MissionData-Assets false — genau der gewuenschte " +
             "Default, kein Asset muss angefasst werden.")]
    public bool countsAutomatedActions;

    [Header("Belohnung")]
    public MissionReward[] rewards;

    [Header("Kette")]
    [Tooltip("Wenn true, wird diese Mission als Teil der Story-Kette behandelt")]
    public bool isStoryMission;

    [Tooltip("Hintergrund-Achievement statt normaler Quest: laeuft ohne Dialog von " +
             "Spielbeginn an mit, taucht NICHT im Quest-Tracker auf und wird vom " +
             "Highlighting ignoriert (kein NPC/Objekt leuchtet dafuer). Erreichbar nur " +
             "ueber die Erfolge-Uebersicht (AchievementsUI). Fuer Meilenstein- und " +
             "Endgame-Ziele gedacht, die den Spieler nicht auf Schritt und Tritt begleiten " +
             "sollen — 'verdiene 100000 Gold' waere als staendig sichtbarer Quest-Eintrag " +
             "nur Rauschen.")]
    public bool isBackgroundAchievement;

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

    [Tooltip("highlightId des NPCs, der diese Mission per Dialog startet — z.B. 'ozan'. " +
             "Fuellt die Luecke zwischen 'Mission ist dran' und 'Mission laeuft': solange sie " +
             "auf das Gespraech wartet, gibt es noch KEIN Objective, an dem der " +
             "MissionHighlightDirector ein Ziel festmachen koennte. Der Spieler liest im " +
             "nextStepHint 'Sprich mit Onkel Ozan', bekommt aber nichts gezeigt. " +
             "Nur sinnvoll zusammen mit startedByDialogue.")]
    public string starterHighlightId;

    [Header("Anzeige")]
    [Tooltip("Kurzer Hinweis wohin der Spieler als Nächstes soll. Wird in der Missions-UI " +
             "angezeigt wenn gerade keine Mission aktiv ist.")]
    [TextArea(1, 3)]
    public string nextStepHint;

    [Header("Verhalten")]
    [Tooltip("Objectives müssen in Reihenfolge abgeschlossen werden (nur das erste unvollständige ist aktiv)")]
    public bool sequentialObjectives;
}

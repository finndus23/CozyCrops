using System.Collections.Generic;
using UnityEngine;

/// <summary>Laufzeitstatus einer aktiven Mission.</summary>
public class MissionState
{
    public MissionData Data { get; }
    private readonly int[] progress;

    public MissionState(MissionData data)
    {
        Data = data;
        progress = new int[data.objectives?.Length ?? 0];
    }

    public MissionState(MissionData data, List<int> loadedProgress) : this(data)
    {
        if (loadedProgress == null) return;
        for (int i = 0; i < Mathf.Min(progress.Length, loadedProgress.Count); i++)
            progress[i] = loadedProgress[i];
    }

    /// <summary>
    /// Niedrigste Stufe, in der noch ein Ziel offen ist. int.MaxValue = alles fertig.
    ///
    /// Damit laesst sich ein Durchlauf zerlegen, ohne ihn starr zu sequenzieren: pflanzen,
    /// giessen und ernten duerfen parallel laufen (Stufe 0), das Verkaufen geht erst danach
    /// auf (Stufe 1). Sonst leuchtet von Anfang an alles gleichzeitig und der Spieler sieht
    /// nicht, was als Naechstes dran ist.
    /// </summary>
    public int ActiveStage
    {
        get
        {
            var objectives = Data.objectives;
            if (objectives == null) return int.MaxValue;

            int lowest = int.MaxValue;
            for (int i = 0; i < objectives.Length; i++)
            {
                if (objectives[i] == null || ObjectiveCompleted(i)) continue;
                if (objectives[i].stage < lowest) lowest = objectives[i].stage;
            }

            return lowest;
        }
    }

    /// <summary>
    /// Darf dieses Ziel gerade Fortschritt machen? Beruecksichtigt sowohl die harte
    /// Sequenz (sequentialObjectives) als auch die Stufen.
    /// </summary>
    public bool IsObjectiveActive(int index)
    {
        var objectives = Data.objectives;
        if (objectives == null || index < 0 || index >= objectives.Length) return false;
        if (ObjectiveCompleted(index)) return false;

        if (Data.sequentialObjectives)
        {
            for (int i = 0; i < objectives.Length; i++)
                if (!ObjectiveCompleted(i)) return i == index;

            return false;
        }

        return objectives[index] != null && objectives[index].stage == ActiveStage;
    }

    public void AddProgress(int objectiveIndex, int amount)
    {
        if (objectiveIndex < 0 || objectiveIndex >= progress.Length) return;
        int required = Data.objectives[objectiveIndex].requiredAmount;
        progress[objectiveIndex] = Mathf.Min(progress[objectiveIndex] + amount, required);
    }

    /// <summary>
    /// Setzt den Fortschritt auf einen absoluten Wert statt ihn zu addieren.
    ///
    /// Für Ziele, die einen Zustand abfragen statt Ereignisse zu zählen — etwa
    /// ToolLevelReached, wo der gemeldete Wert die aktuelle Werkzeugstufe ist.
    /// Mit AddProgress würde sich die Stufe bei jedem Upgrade aufsummieren
    /// (1+2+3+… statt 3) und das Ziel viel zu früh erfüllen.
    /// </summary>
    public void SetProgress(int objectiveIndex, int value)
    {
        if (objectiveIndex < 0 || objectiveIndex >= progress.Length) return;
        int required = Data.objectives[objectiveIndex].requiredAmount;
        progress[objectiveIndex] = Mathf.Clamp(value, 0, required);
    }

    public int GetProgress(int objectiveIndex) =>
        objectiveIndex >= 0 && objectiveIndex < progress.Length ? progress[objectiveIndex] : 0;

    public bool ObjectiveCompleted(int objectiveIndex) =>
        Data.objectives != null &&
        objectiveIndex < Data.objectives.Length &&
        GetProgress(objectiveIndex) >= Data.objectives[objectiveIndex].requiredAmount;

    public bool IsCompleted()
    {
        if (Data.objectives == null || Data.objectives.Length == 0) return true;
        for (int i = 0; i < Data.objectives.Length; i++)
            if (!ObjectiveCompleted(i)) return false;
        return true;
    }

    public int[] GetAllProgress() => (int[])progress.Clone();
}

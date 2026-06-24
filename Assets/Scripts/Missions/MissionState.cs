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

    public void AddProgress(int objectiveIndex, int amount)
    {
        if (objectiveIndex < 0 || objectiveIndex >= progress.Length) return;
        int required = Data.objectives[objectiveIndex].requiredAmount;
        progress[objectiveIndex] = Mathf.Min(progress[objectiveIndex] + amount, required);
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

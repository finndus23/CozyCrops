using System;
using System.Collections.Generic;

[Serializable]
public class MissionProgressSaveData
{
    public string missionId;
    public bool isCompleted;
    public bool isActive;
    public List<int> objectiveProgress = new();
}

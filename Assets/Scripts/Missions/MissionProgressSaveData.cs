using System;
using System.Collections.Generic;

[Serializable]
public class MissionProgressSaveData
{
    public string missionId;
    public bool isCompleted;
    public bool isActive;
    public List<int> objectiveProgress = new();

    /// <summary>
    /// Mission ist durch, die Belohnung liegt aber noch zum Abholen im Quest-Panel.
    ///
    /// Default false ist Absicht: Altsaves kennen das Feld nicht, JsonUtility setzt es
    /// dadurch auf false — dort gelten alle Belohnungen als kassiert. Andersherum würden
    /// nach dem Update plötzlich alle je abgeschlossenen Missionen wieder Beute anbieten.
    /// </summary>
    public bool rewardsPending;
}

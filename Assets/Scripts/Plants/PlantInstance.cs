using UnityEngine;

/// <summary>
/// Laufender Zustand einer Pflanze auf einer GridCell.
/// </summary>
public class PlantInstance
{
    public PlantType Type { get; }
    public int StageIndex { get; private set; }
    public float GrowthTimer { get; private set; }
    public int WateringsThisStage { get; private set; }

    public bool IsFullyGrown => Type.IsLastStage(StageIndex);

    public bool NeedsWatering =>
        Type.requiresWatering && WateringsThisStage < Type.wateringsPerStage;

    public PlantInstance(PlantType type)
    {
        Type = type;
        StageIndex = 0;
        GrowthTimer = 0f;
        WateringsThisStage = 0;
    }

    /// <summary>
    /// Wird vom Load-System benutzt, um den exakten gespeicherten Pflanzenzustand wiederherzustellen.
    /// </summary>
    public PlantInstance(PlantType type, int stageIndex, float growthTimer, int wateringsThisStage)
    {
        Type = type;

        int maxStage = Mathf.Max(0, Type.StageCount - 1);
        StageIndex = Mathf.Clamp(stageIndex, 0, maxStage);
        GrowthTimer = Mathf.Max(0f, growthTimer);
        WateringsThisStage = Mathf.Max(0, wateringsThisStage);
    }

    /// <summary>
    /// Gibt zurück ob die Pflanze in die nächste Phase gewachsen ist.
    /// </summary>
    public bool Tick(float deltaTime)
    {
        if (IsFullyGrown) return false;
        if (Type.requiresWatering && WateringsThisStage < Type.wateringsPerStage) return false;

        GrowthTimer += deltaTime;

        if (GrowthTimer >= Type.growthStages[StageIndex].timeToNextStage)
        {
            GrowthTimer = 0f;
            WateringsThisStage = 0;
            StageIndex++;
            return true; // Stage-Wechsel
        }

        return false;
    }

    public void Water()
    {
        if (IsFullyGrown) return;
        if (WateringsThisStage < Type.wateringsPerStage)
            WateringsThisStage++;
    }

    public GameObject GetCurrentPrefab()
    {
        if (!Type.IsValidStage(StageIndex)) return null;
        return Type.growthStages[StageIndex].prefab;
    }
}

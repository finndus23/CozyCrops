using UnityEngine;

[CreateAssetMenu(fileName = "NewPlantType", menuName = "CozyCrops/Plant Type")]
public class PlantType : ScriptableObject
{
    [Header("Save")]
    [Tooltip("Eindeutige ID für Savegames. Leer lassen = Asset-Name wird benutzt. Nicht später ändern, sonst können alte Saves die Pflanze nicht mehr finden.")]
    [SerializeField] private string saveId;
    public string SaveId => string.IsNullOrWhiteSpace(saveId) ? name : saveId;

    [Header("Info")]
    public string plantName;
    public Sprite icon;

    [Header("Economy")]
    public int seedPrice = 10;
    public int sellPrice = 25;

    [Header("Growth")]
    public GrowthStage[] growthStages;

    [Header("Watering")]
    public bool requiresWatering = true;
    [Tooltip("Wie oft muss pro Wachstumsphase gegossen werden?")]
    public int wateringsPerStage = 1;

    public int StageCount => growthStages?.Length ?? 0;
    public bool IsLastStage(int stageIndex) => stageIndex >= StageCount - 1;
    public bool IsValidStage(int stageIndex) => stageIndex >= 0 && stageIndex < StageCount;
}

[System.Serializable]
public class GrowthStage
{
    [Tooltip("Name der Phase, z.B. Setzling, Halbgewachsen, Reif")]
    public string stageName;

    [Tooltip("Prefab das in dieser Phase angezeigt wird")]
    public GameObject prefab;

    [Tooltip("Zeit in Sekunden bis zur nächsten Phase (letzte Phase wird ignoriert)")]
    public float timeToNextStage = 60f;
}

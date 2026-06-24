using System;
using UnityEngine;

/// <summary>
/// Eine einzelne Belohnung für das Abschließen einer Mission.
/// Erweiterbar: neue RewardType-Werte hinzufügen + Handling in MissionManager.CompleteMission.
/// </summary>
[Serializable]
public class MissionReward
{
    public enum RewardType
    {
        Money,
        Seed,
        // Hier später: Fertilizer, Tool, Sprinkler, ...
    }

    [Tooltip("Art der Belohnung")]
    public RewardType type;

    [Tooltip("Menge (Gold oder Anzahl Seeds)")]
    public int amount = 1;

    [Tooltip("Nur für Seed-Rewards: welche Pflanze")]
    public PlantType plant;
}

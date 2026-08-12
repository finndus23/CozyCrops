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
        Tool,
        UnlockZone,
        // Hier später: Fertilizer, Sprinkler, ...
    }

    [Tooltip("Art der Belohnung")]
    public RewardType type;

    [Tooltip("Menge (Gold oder Anzahl Seeds)")]
    public int amount = 1;

    [Tooltip("Nur für Seed-Rewards: welche Pflanze")]
    public PlantType plant;

    [Tooltip("Nur für Tool-Rewards: welches Werkzeug der Spieler bekommt.\n" +
             "Damit kann die Story ein Werkzeug verschenken statt es nur im Shop anzubieten — " +
             "genau das braucht eine Kette, in der jeder Schritt etwas Neues aufmacht.")]
    public ToolType tool;

    [Tooltip("Nur für UnlockZone-Rewards: Name des GridZone-GameObjects das freigeschaltet wird.")]
    public string zoneName;
}

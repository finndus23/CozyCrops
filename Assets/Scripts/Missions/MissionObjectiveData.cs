using System;
using UnityEngine;

[Serializable]
public class MissionObjectiveData
{
    [Tooltip("Art der Aufgabe")]
    public MissionObjectiveType type;

    [Tooltip("Optional: Spezifischer Pflanzentyp (nur für PlantCrop, HarvestCrop, SellCrop, WaterCrop, BuySeed)")]
    public PlantType targetPlantType;

    [Tooltip("Optional: Spezifisches Werkzeug (nur für AcquireTool, SelectTool, UpgradeTool).\n" +
             "None = jedes Werkzeug zählt.")]
    public ToolType targetTool;

    [Tooltip("Optional: Name/SaveId der Zone (nur für UnlockZone). Leer = jede Zone zählt.")]
    public string targetZoneId;

    [Tooltip("Optional: licenseId (nur für BuyLicense). Leer = jede Lizenz zählt.")]
    public string targetLicenseId;

    [Tooltip("Optional: ID des Objekts, das hervorgehoben werden soll, solange dieses Ziel " +
             "offen ist (entspricht HighlightTarget.highlightId am NPC/Objekt).\n\n" +
             "Nur nötig, wenn es mehrere Kandidaten gibt und genau einer gemeint ist.\n" +
             "Leer = es leuchtet automatisch, was diesen Objective-TYP bei sich eingetragen " +
             "hat (z.B. Scheune bei OpenBarn).")]
    public string targetHighlightId;

    [Tooltip("Wie viele Male muss die Aktion ausgeführt werden?\n\n" +
             "Ausnahme ToolLevelReached: dort ist das die ZIEL-STUFE, kein Zähler.")]
    public int requiredAmount = 1;

    [Tooltip("Beschreibung für die UI, z.B. 'Hacke 1 Feld um'")]
    public string description;
}

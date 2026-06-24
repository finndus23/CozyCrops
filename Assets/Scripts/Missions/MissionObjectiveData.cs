using System;
using UnityEngine;

[Serializable]
public class MissionObjectiveData
{
    [Tooltip("Art der Aufgabe")]
    public MissionObjectiveType type;

    [Tooltip("Optional: Spezifischer Pflanzentyp (nur für PlantCrop, HarvestCrop, SellCrop)")]
    public PlantType targetPlantType;

    [Tooltip("Wie viele Male muss die Aktion ausgeführt werden?")]
    public int requiredAmount = 1;

    [Tooltip("Beschreibung für die UI, z.B. 'Hacke 1 Feld um'")]
    public string description;
}

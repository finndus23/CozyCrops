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

    [Header("Balancing")]
    [Tooltip("Multiplikator auf die Wachstumszeit JEDER Phase (GrowthStage.timeToNextStage). " +
             "1 = wie in den Phasen eingetragen. Unter 1 = wächst schneller, über 1 = " +
             "langsamer.\n\n" +
             "Eigener Hebel getrennt von den einzelnen Phasenzeiten — damit du am " +
             "Gesamttempo einer Sorte drehen kannst, ohne jede Phase einzeln nachzuziehen. " +
             "Wirkt multiplikativ mit dem Dünger-Bonus (GridCell.IsFertilized).")]
    [Range(0.3f, 2f)]
    public float growthSpeedMultiplier = 1f;

    [Tooltip("Multiplikator auf die Dauer von Werkzeug-Aktionen AN DIESER SORTE: Säen " +
             "(Seed-Tool), Gießen und Ernten (Sichel). 1 = Standard, unter 1 = Aktionen an " +
             "dieser Sorte gehen schneller (z.B. Karotte — zart, schnell weg), über 1 = " +
             "langsamer (z.B. Sonnenblume — robuster, mehr Aufwand pro Stück).\n\n" +
             "Betrifft NICHT die Hacke (noch keine Pflanze auf der Tile) oder den Dünger " +
             "(wirkt auf die Tile, nicht auf eine bestimmte Sorte). Bei Gießen/Ernten mit " +
             "gemischter AoE-Fläche zählt der Durchschnitt über alle betroffenen Pflanzen.")]
    [Range(0.5f, 2f)]
    public float actionSpeedMultiplier = 1f;

    [Header("Kompostieren")]
    [Tooltip("Wie viel Dünger-Wert 1 Stück dieser Frucht beim Kompostieren beisteuert " +
             "(ComposterInteraction.cropsPerFertilizer ist der Nenner darunter — 1.0 = " +
             "Standard-Sorte).\n\n" +
             "Niedriger für schnell wachsende Sorten (z.B. Karotte): weniger Wert pro Stück, " +
             "aber mehr Ernten pro Zeit gleichen das wieder aus — mehr Klicks, aber auch mehr " +
             "Dünger/Minute.\n" +
             "Höher für langsame Sorten (z.B. Blumenkohl), die \"AFK-Variante\": weniger " +
             "Klicks nötig, dafür ist jedes einzelne Stück beim Kompostieren mehr wert.")]
    [Range(0.1f, 3f)]
    public float compostValue = 1f;

    [Header("Sound")]
    [Tooltip("Gesät. Optional — liegt zusätzlich über dem Klang aus dem Werkzeug. Leise " +
             "halten, das Werkzeug trägt die Aktion.")]
    public AudioClip[] plantSfx;

    [Tooltip("Geerntet. Hier lohnt sich der Aufwand am meisten — eine Karotte, die anders " +
             "klingt als eine Sonnenblume, macht die Ernte greifbar.")]
    public AudioClip[] harvestSfx;

    [Tooltip("Verkauft. Leer = der allgemeine Verkaufsklang aus der UiSfxLibrary.")]
    public AudioClip[] sellSfx;

    [Tooltip("Lautstärke der frucht-eigenen Klänge, 0–1. Unter dem Werkzeug bleiben: sie " +
             "sollen die Frucht kennzeichnen, nicht die Aktion überdecken.")]
    [Range(0f, 1f)]
    public float sfxVolume = 0.6f;

    [Header("Darstellung")]
    [Tooltip("Drehung um die Y-Achse beim Platzieren, in Grad.\n\n" +
             "Für Modelle, die in ihrem Prefab in die falsche Richtung schauen. Hier statt " +
             "in den Prefabs, weil eine Pflanze pro Wachstumsphase ein eigenes Prefab hat — " +
             "sonst müsste man dieselbe Drehung dreimal eintragen und bei jeder neuen Phase " +
             "daran denken.")]
    public float modelYRotation;

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

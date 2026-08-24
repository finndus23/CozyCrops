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
    [Tooltip("TEMPO-Multiplikator aufs Wachstum. 1 = wie in den Phasen eingetragen. " +
             "UEBER 1 = waechst SCHNELLER, unter 1 = langsamer. " +
             "Achtung, das ist die Gegenrichtung zu actionSpeedMultiplier weiter unten: " +
             "dieser Wert geht auf die RATE (PlantManager: dt *= growthSpeedMultiplier), " +
             "jener auf die DAUER. 0,8 heisst hier also 25 Prozent langsamer, dort 20 " +
             "Prozent schneller.\n\n" +
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

    [Tooltip("DAUER-Multiplikator fuer AUTOMATIK-Jobs an dieser Sorte — wirkt nur auf " +
             "Stationen, nie auf den Spieler. Unter 1 = die Station arbeitet an dieser " +
             "Sorte schneller, ueber 1 = langsamer. " +
             "Der Hebel, mit dem billige Sorten zum Automatik-Futter werden und teure der " +
             "Handarbeit vorbehalten bleiben: Karotte niedrig, Sonnenblume hoch. Wirkt auf " +
             "Jobdauer UND Takt, ein Wert von 1,8 verlangsamt den Zyklus also wirklich um " +
             "das 1,8-fache.")]
    [Range(0.3f, 3f)]
    public float automationDurationMultiplier = 1f;

    [Tooltip("Diese Sorte laesst sich NUR auf geduengtem Boden pflanzen. Harvest() setzt " +
             "den Duenger zurueck, es braucht also vor jedem Anbau frischen — die Sorte " +
             "haengt damit dauerhaft am Komposter-Kreislauf. " +
             "Der Hebel fuer die Spitzensorte: statt Duengen wirtschaftlich attraktiv zu " +
             "rechnen, wird es zur Voraussetzung. Das haelt auch, wenn spaeter an Preisen " +
             "gedreht wird.")]
    public bool requiresFertilizedSoil;

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

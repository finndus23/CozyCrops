public enum MissionObjectiveType
{
    // Farm-Aktionen
    TillField,
    PlantCrop,
    WaterCrop,
    HarvestCrop,
    SellCrop,
    EarnMoney,
    AcquireTool,

    BuySeed,
    SelectTool,

    // Tutorial-spezifisch
    EnterBuildMode,
    ExitBuildMode,
    PlaceFarmTile,
    TravelToMarket,
    TravelToFarm,
    OpenBarn,

    // ACHTUNG: Neue Werte immer HINTEN anhängen.
    // MissionData-Assets serialisieren den Enum als Zahl (siehe tutorial.asset: "type: 9").
    // Ein Einschub in der Mitte würde jedes bestehende Objective still auf eine andere
    // Aufgabe umbiegen — der Fehler fiele erst im Playtest auf.
    UpgradeTool,
    UnlockZone,

    /// <summary>
    /// Aktion eingereiht, während schon eine lief. Zählt also echtes Stapeln,
    /// nicht einfach N Klicks nacheinander.
    /// </summary>
    QueueActions,

    /// <summary>
    /// Werkzeug hat ein bestimmtes LEVEL erreicht — <c>requiredAmount</c> ist die
    /// Ziel-Stufe, kein Zähler.
    ///
    /// Braucht es zusätzlich zu UpgradeTool, weil das nur Upgrade-Vorgänge zählt: "10 mal
    /// aufrüsten" wäre auch mit vier Werkzeugen quer erfüllbar und würde den eigentlichen
    /// Meilenstein — den AoE-Sprung bei Stufe 10 auf EINEM Werkzeug — gar nicht abbilden.
    /// </summary>
    ToolLevelReached,

    /// <summary>Lizenz gekauft. Über <c>targetLicenseId</c> auf eine bestimmte eingrenzbar.</summary>
    BuyLicense
}

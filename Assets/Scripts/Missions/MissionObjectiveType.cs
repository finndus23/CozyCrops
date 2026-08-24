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
    BuyLicense,

    /// <summary>Ein Feld gedüngt. <c>requiredAmount</c> zählt gedüngte Kacheln.</summary>
    FertilizeField,

    /// <summary>
    /// Ernte in den Komposter geworfen. <c>requiredAmount</c> zählt die STÜCKE, nicht die
    /// Vorgänge — sonst wäre "kompostiere 10" mit zehn Ein-Stück-Würfen erfüllbar, und der
    /// Spieler hätte die eigentliche Mechanik (viel auf einmal ist effizienter) nie gesehen.
    /// </summary>
    CompostCrops,

    /// <summary>Fertigen Dünger abgeholt. <c>requiredAmount</c> zählt die Einheiten.</summary>
    CollectFertilizer,

    /// <summary>Eine Automations-Station platziert (gekauft oder aus dem Lager
    /// aufgestellt). <c>requiredAmount</c> zählt Platzierungen.</summary>
    PlaceStation,

    /// <summary>Ein Modul in eine Station eingebaut. <c>requiredAmount</c> zählt
    /// Einbau-Vorgänge — bei 4 sind das alle vier Modultypen.</summary>
    InstallModule,

    /// <summary>Die Reichweite einer Station aufgewertet. <c>requiredAmount</c> zählt
    /// Aufwertungen, nicht die erreichte Stufe.</summary>
    UpgradeStation,

    /// <summary>
    /// Ernte, die eine Automations-Station SELBST eingebracht hat — nicht der Spieler.
    /// Getrennt von HarvestCrop, damit dieses Ziel nur durch die Automatik voranschreitet
    /// und nicht durch eigenhändiges Ernten umgangen werden kann; die Mission soll ja
    /// zeigen, dass der Kreislauf tatsächlich von selbst läuft.
    /// </summary>
    AutomationHarvest
}

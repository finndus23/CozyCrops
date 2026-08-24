/// <summary>
/// Generiert automatisch lesbaren deutschen Text aus einem MissionObjectiveData.
/// Kein manuelles description-Feld nötig.
/// </summary>
public static class MissionObjectiveFormatter
{
    public static string Format(MissionObjectiveData obj)
    {
        int n = obj.requiredAmount;
        string plant = obj.targetPlantType != null ? obj.targetPlantType.plantName : null;
        string tool = ToolName(obj.targetTool);

        return obj.type switch
        {
            MissionObjectiveType.TillField =>
                n == 1 ? "Hacke 1 Feld um" : $"Hacke {n} Felder um",

            MissionObjectiveType.PlantCrop =>
                plant != null ? $"Pflanze {n}x {plant}" : $"Pflanze {n}x Samen",

            MissionObjectiveType.WaterCrop =>
                plant != null ? $"Gieße {n}x {plant}" : $"Gieße {n} Pflanze(n)",

            MissionObjectiveType.HarvestCrop =>
                plant != null ? $"Ernte {n}x {plant}" : $"Ernte {n} Pflanze(n)",

            MissionObjectiveType.SellCrop =>
                plant != null ? $"Verkaufe {n}x {plant}" : $"Verkaufe {n} Ernte",

            MissionObjectiveType.EarnMoney =>
                $"Verdiene {n} G",

            MissionObjectiveType.AcquireTool =>
                tool != null ? $"Kaufe {tool}"
                             : n == 1 ? "Kaufe 1 Werkzeug" : $"Kaufe {n} Werkzeuge",

            MissionObjectiveType.BuySeed =>
                plant != null ? $"Kaufe {n}x {plant} Samen" : $"Kaufe {n} Samen",

            MissionObjectiveType.SelectTool =>
                tool != null ? $"Wähle {tool} aus der Hotbar"
                             : "Wähle ein Werkzeug aus der Hotbar",

            MissionObjectiveType.UpgradeTool =>
                tool != null
                    ? (n == 1 ? $"Rüste {tool} auf" : $"Rüste {tool} {n}x auf")
                    : (n == 1 ? "Rüste ein Werkzeug auf" : $"Rüste {n}x ein Werkzeug auf"),

            MissionObjectiveType.UnlockZone =>
                n == 1 ? "Schalte eine neue Fläche frei" : $"Schalte {n} neue Flächen frei",

            MissionObjectiveType.QueueActions =>
                $"Reihe {n} Aktionen ein, während schon eine läuft",

            // n ist hier die Ziel-Stufe, kein Zähler.
            MissionObjectiveType.ToolLevelReached =>
                tool != null ? $"Bring {tool} auf Stufe {n}" : $"Bring ein Werkzeug auf Stufe {n}",

            MissionObjectiveType.BuyLicense =>
                n == 1 ? "Kaufe eine Lizenz" : $"Kaufe {n} Lizenzen",

            MissionObjectiveType.EnterBuildMode =>
                "Betrete den Baumodus (B)",

            MissionObjectiveType.ExitBuildMode =>
                "Verlasse den Baumodus (B)",

            MissionObjectiveType.PlaceFarmTile =>
                n == 1 ? "Konvertiere 1 Tile zu Farmland" : $"Konvertiere {n} Tiles zu Farmland",

            MissionObjectiveType.TravelToMarket =>
                "Fahre zum Marktplatz",

            MissionObjectiveType.TravelToFarm =>
                "Fahre zurück zur Farm",

            MissionObjectiveType.OpenBarn =>
                "Öffne die Scheune",

            MissionObjectiveType.FertilizeField =>
                n == 1 ? "Dünge 1 Feld" : $"Dünge {n} Felder",

            // "Früchte" statt "Mal": das Ziel zählt die Stücke, nicht die Würfe.
            MissionObjectiveType.CompostCrops => plant != null
                ? (n == 1 ? $"Kompostiere 1x {plant}" : $"Kompostiere {n}x {plant}")
                : (n == 1 ? "Kompostiere 1 Frucht" : $"Kompostiere {n} Früchte"),

            MissionObjectiveType.CollectFertilizer =>
                n == 1 ? "Hole 1 Dünger ab" : $"Hole {n} Dünger ab",

            MissionObjectiveType.PlaceStation =>
                n == 1 ? "Stelle eine Automations-Station auf" : $"Stelle {n} Automations-Stationen auf",

            MissionObjectiveType.InstallModule =>
                n == 1 ? "Baue 1 Modul in die Station ein" : $"Baue {n} Module in die Station ein",

            MissionObjectiveType.UpgradeStation =>
                n == 1 ? "Werte die Reichweite der Station auf" : $"Werte die Station {n}x auf",

            MissionObjectiveType.AutomationHarvest =>
                plant != null
                    ? (n == 1 ? $"Lass die Station 1x {plant} ernten" : $"Lass die Station {n}x {plant} ernten")
                    : (n == 1 ? "Lass die Station 1 Ernte einbringen" : $"Lass die Station {n} Ernten einbringen"),

            MissionObjectiveType.ManualHarvest =>
                n == 1 ? "Ernte von Hand 1 Pflanze" : $"Ernte von Hand {n} Pflanzen",

            MissionObjectiveType.StationLevelReached =>
                $"Bring die Station auf Stufe {n}",

            MissionObjectiveType.AllModulesMaxed =>
                "Alle vier Module auf Hoechststufe",

            MissionObjectiveType.ComposterLevelReached =>
                $"Bring den Komposter auf Stufe {n}",

            _ => $"Aufgabe ({n})"
        };
    }

    /// <summary>
    /// Deutscher Anzeigename. Bewusst nicht ToolData.displayName: die Assets sind auf
    /// Englisch ("Hoe", "Seeder") und der Formatter hat keinen Zugriff auf die Registry.
    /// </summary>
    private static string ToolName(ToolType tool) => tool switch
    {
        ToolType.Hoe        => "die Hacke",
        ToolType.WateringCan => "die Gießkanne",
        ToolType.Seed       => "den Seeder",
        ToolType.Scythe     => "die Sichel",
        ToolType.Fertilize  => "den Dünger",
        _ => null
    };
}

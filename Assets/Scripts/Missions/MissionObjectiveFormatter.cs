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

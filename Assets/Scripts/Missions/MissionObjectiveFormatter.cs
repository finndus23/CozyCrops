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
                n == 1 ? "Kaufe 1 Werkzeug" : $"Kaufe {n} Werkzeuge",

            MissionObjectiveType.BuySeed =>
                plant != null ? $"Kaufe {n}x {plant} Samen" : $"Kaufe {n} Samen",

            MissionObjectiveType.SelectTool =>
                "Wähle ein Werkzeug aus der Hotbar",

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
}

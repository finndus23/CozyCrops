using UnityEngine;

/// <summary>
/// Eine kaufbare Lizenz — der Meilenstein-Träger der Wirtschaft.
///
/// Lizenzen sind bewusst teuer und schalten dafür etwas frei, das den Durchsatz spürbar
/// hebt (neue Sorte, später Dünger und Automatik). Damit entsteht der gewollte Sägezahn:
/// gegen Ende zieht sich das Sparen, und der Kauf reißt die Progression wieder hoch.
///
/// Anlegen: Rechtsklick im Project → Create → CozyCrops → License
/// </summary>
[CreateAssetMenu(fileName = "NewLicense", menuName = "CozyCrops/License")]
public class LicenseData : ScriptableObject
{
    [Header("Identität")]
    [Tooltip("Eindeutige ID für Savegames. Nicht nachträglich ändern.")]
    public string licenseId;

    public string displayName;

    [TextArea(2, 4)]
    public string description;

    public Sprite icon;

    [Header("Kosten")]
    public int price = 200;

    [Header("Freischaltung")]
    [Tooltip("Pflanzen die mit dieser Lizenz im Saatgut-Handel auftauchen.")]
    public PlantType[] unlocksPlants;

    [Tooltip("Werkzeuge die mit dieser Lizenz überhaupt erst kaufbar werden. " +
             "None-Einträge werden ignoriert.")]
    public ToolType[] unlocksTools;

    [Header("Voraussetzungen")]
    [Tooltip("licenseIds die vorher gekauft sein müssen. Leer = sofort erhältlich.")]
    public string[] requiredLicenseIds;

    [Tooltip("missionId die abgeschlossen sein muss, bevor die Lizenz im Regal auftaucht. " +
             "Leer = keine Bedingung.")]
    public string requiredMissionId;

    /// <summary>Schaltet diese Lizenz die Pflanze frei?</summary>
    public bool Unlocks(PlantType plant)
    {
        if (plant == null || unlocksPlants == null) return false;

        foreach (var entry in unlocksPlants)
            if (entry == plant) return true;

        return false;
    }

    /// <summary>Schaltet diese Lizenz das Werkzeug frei?</summary>
    public bool Unlocks(ToolType tool)
    {
        if (tool == ToolType.None || unlocksTools == null) return false;

        foreach (var entry in unlocksTools)
            if (entry == tool) return true;

        return false;
    }
}

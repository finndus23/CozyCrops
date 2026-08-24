/// <summary>
/// Die platzierbaren Automatik-Geräte.
///
/// Bewusst ein EIGENES Enum statt vier neuer <see cref="ToolType"/>-Werte: ToolType wird als
/// Zahl in MissionData-Assets serialisiert und läuft durch ToolRegistry, BuildUpgradeRows,
/// AoEPreview.toolStyles und PromoteQueued — neue Werte würden dort überall Sonderfälle
/// erzeugen. Welche Aktion ein Gerät ausführt, steht stattdessen in
/// <see cref="AutomationDeviceData.executesTool"/>.
///
/// Die vier Geräte bilden zusammen einen geschlossenen Kreislauf:
/// ernten → hacken → säen → gießen → wächst → ernten. Der Pflug ist dabei nicht optional,
/// weil GridCell.Harvest() die Kachel auf ungehackt zurücksetzt — ohne ihn liefe der
/// Kreislauf nach einer Runde tot.
/// </summary>
public enum AutomationDeviceType
{
    None,
    Sprinkler,  // gießen  → ToolType.WateringCan
    Plow,       // hacken  → ToolType.Hoe
    Seeder,     // säen    → ToolType.Seed
    Harvester   // ernten  → ToolType.Scythe
}

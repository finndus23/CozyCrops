using UnityEngine;

/// <summary>
/// Definiert einen Meilenstein auf einem bestimmten Geräte-Level.
/// Alle nicht gesetzten Felder (0) werden ignoriert — nur Änderungen eintragen.
/// Muster wie <see cref="ToolMilestone"/>.
/// </summary>
[System.Serializable]
public class AutomationMilestone
{
    [Tooltip("Ab welchem Level greift dieser Meilenstein?")]
    public int level;

    [Tooltip("Neuer Reichweiten-Radius ab diesem Level (Chebyshev). 0 = keine Änderung.\n" +
             "1 = 3×3, 2 = 5×5, 3 = 7×7 …")]
    public int radius;

    [Tooltip("Faktor auf das Grundintervall ab diesem Level. 0 = keine Änderung.\n\n" +
             "Wirkt NICHT kumulativ: der letzte gültige Meilenstein gewinnt. Ein Gerät mit " +
             "0,75 auf Level 5 und 0,5 auf Level 15 läuft ab Level 15 mit 0,5 des " +
             "Grundintervalls, nicht mit 0,375.")]
    public float intervalMultiplier;

    [Tooltip("Wie viele Kacheln pro Takt bearbeitet werden. 0 = keine Änderung.\n\n" +
             "Nur für den Capstone gedacht: bei strikt einer Kachel pro Takt würde ein Gerät " +
             "durch Reichweiten-Upgrades SCHLECHTER — die Fläche wächst 9 → 25 → 49, das " +
             "Intervall lässt sich aber nur halbieren. Die Zeit zwischen zwei Besuchen " +
             "derselben Kachel stiege sonst von 72 s auf über 200 s.")]
    public int tilesPerTick;

    [Tooltip("Text der im Geräte-Popup angezeigt wird, z.B. 'Reichweite vergrößert!'")]
    public string unlockText;
}

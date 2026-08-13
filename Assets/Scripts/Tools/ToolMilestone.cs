using UnityEngine;

/// <summary>
/// Definiert einen Meilenstein auf einem bestimmten Tool-Level.
/// Alle nicht gesetzten Felder (0 / -1) werden ignoriert — nur Änderungen eintragen.
/// </summary>
[System.Serializable]
public class ToolMilestone
{
    [Tooltip("Ab welchem Level greift dieser Meilenstein?")]
    public int level;

    [Tooltip("Neue AoE-Größe ab diesem Level. 0 = keine Änderung. 1=1×1  2=2×2  3=3×3 …")]
    public int aoSize;

    [Tooltip("Bonus-Crops beim Ernten (nur Sichel sinnvoll). 0 = keine Änderung.")]
    public int yieldBonus;

    [Tooltip("Überschreibt die berechnete Duration hart. -1 = ignorieren.")]
    public float durationOverride = -1f;

    [Tooltip("Neue Warteschlangen-Größe für DIESES Werkzeug ab diesem Level. 0 = keine Änderung.\n" +
             "Wie viele Aktionen der Spieler vorausplanen kann, bevor er warten muss.")]
    public int queueSize;

    [Tooltip("Text der im Shop angezeigt wird, z.B. 'Fläche vergrößert!'")]
    public string unlockText;
}

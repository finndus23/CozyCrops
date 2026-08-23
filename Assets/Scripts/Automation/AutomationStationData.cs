using UnityEngine;

/// <summary>
/// Definiert die Automations-Station: das Gehäuse, in das bis zu vier Module wandern.
///
/// <b>Die Station besitzt die Reichweite, die Module besitzen das Tempo.</b> Das ist der
/// Kern der Bauform: alle Module teilen sich denselben Mittelpunkt, dadurch ist die
/// angezeigte Reichweite exakt die Fläche, auf der die ganze Kette läuft.
///
/// Vier einzeln stehende Geräte konnten das nicht. Bei Radius r liegt der Schnitt ihrer
/// vier Quadrate bei 2r × 2r Kacheln — und die enthalten genau die vier Gerätekacheln
/// selbst. Auf Stufe 0 (r=1) war die vollständig versorgte Ackerfläche also buchstäblich
/// null, und die Kette schloss sich frühestens, wenn alle vier Geräte Stufe 10 erreicht
/// hatten. Mit einem gemeinsamen Mittelpunkt entfällt das Problem konstruktiv.
/// </summary>
[CreateAssetMenu(fileName = "NewAutomationStationData", menuName = "CozyCrops/Automation Station Data")]
public class AutomationStationData : ScriptableObject
{
    [Header("Identität")]
    public string displayName = "Automations-Station";
    public Sprite icon;

    [Tooltip("Prefab des Stations-Gehäuses. Braucht einen Collider (WorldClickHandler " +
             "raycastet ohne LayerMask) und die AutomationDevice-Komponente.")]
    public GameObject worldPrefab;

    [Header("Upgrade-Limits")]
    public int maxLevel = 20;

    [Header("Reichweite")]
    [Tooltip("Reichweiten-Radius auf Level 0 (Chebyshev, quadratisch um die Station).\n" +
             "1 = 3×3, 2 = 5×5, 3 = 7×7 …\n\n" +
             "Die eigene Kachel bleibt draußen, die Station steht ja darauf — bei r=1 sind " +
             "das also 8 bearbeitete Kacheln.")]
    public int baseRadius = 1;

    [Header("Kosten")]
    [Tooltip("Kaufpreis der leeren Station. Module werden einzeln dazugekauft.")]
    public int buyPrice = 250;

    [Tooltip("Goldkosten für das erste Reichweiten-Upgrade (Level 0 → 1).")]
    public int baseCost = 80;

    [Tooltip("Zusätzliche Goldkosten pro Level.")]
    public int costScalingPerLevel = 20;

    [Header("Meilensteine")]
    [Tooltip("Nutzt vom Meilenstein nur das Feld 'radius'. Nach Level sortieren.")]
    public AutomationMilestone[] milestones;

    [Header("Modul-Anbauteile")]
    [Tooltip("Wo die Anbauteile der Module am Gehäuse sitzen, relativ zur Station.\n\n" +
             "Ein Eintrag pro Modul-Steckplatz. Sind zu wenige eingetragen, landen weitere " +
             "Module im Kreis um die Station.")]
    public Vector3[] moduleSlotOffsets =
    {
        new(0.28f, 0f, 0.28f),
        new(-0.28f, 0f, 0.28f),
        new(0.28f, 0f, -0.28f),
        new(-0.28f, 0f, -0.28f)
    };

    // ── Berechnete Werte ──────────────────────────────────────────────────────

    /// <summary>Goldkosten um von (level) auf (level+1) zu upgraden. -1 wenn schon max.</summary>
    public int GetUpgradeCost(int currentLevel)
    {
        if (currentLevel >= maxLevel) return -1;
        return baseCost + currentLevel * costScalingPerLevel;
    }

    /// <summary>Reichweiten-Radius für das gegebene Level. Letzter gültiger Meilenstein gewinnt.</summary>
    public int GetRadius(int level)
    {
        int radius = baseRadius;

        if (milestones != null)
        {
            for (int i = 0; i < milestones.Length; i++)
            {
                var m = milestones[i];
                if (m == null) continue;
                if (m.level > level) break;
                if (m.radius > 0) radius = m.radius;
            }
        }

        return Mathf.Max(0, radius);
    }

    /// <summary>Kantenlänge der Reichweite in Kacheln — für Anzeigetexte wie "5×5".</summary>
    public int GetSideLength(int level) => GetRadius(level) * 2 + 1;

    /// <summary>Wie viele Kacheln die Station bei diesem Level tatsächlich bearbeitet.</summary>
    public int GetTileCount(int level)
    {
        int side = GetSideLength(level);
        return side * side - 1;   // ohne die eigene Kachel
    }

    /// <summary>Gibt den Meilenstein für exakt dieses Level zurück, oder null.</summary>
    public AutomationMilestone GetMilestoneAt(int level)
    {
        if (milestones == null) return null;
        foreach (var m in milestones)
            if (m != null && m.level == level) return m;
        return null;
    }

    /// <summary>Position eines Anbauteils relativ zur Station.</summary>
    public Vector3 GetSlotOffset(int index)
    {
        if (moduleSlotOffsets != null && index >= 0 && index < moduleSlotOffsets.Length)
            return moduleSlotOffsets[index];

        // Fallback: gleichmäßig im Kreis, damit auch ohne gepflegte Liste nichts stapelt.
        float angle = index * 90f * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle) * 0.28f, 0f, Mathf.Sin(angle) * 0.28f);
    }
}

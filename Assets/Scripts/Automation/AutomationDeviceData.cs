using UnityEngine;

/// <summary>
/// Definiert ein Automatik-Gerät mit allen Upgrade-Stufen.
/// Lege pro Gerät ein eigenes Asset an: Rechtsklick → Create → CozyCrops → Automation Device Data
///
/// Aufbau exakt analog <see cref="ToolData"/>: alle Werte sind eine Funktion des Levels,
/// das Asset selbst hält keinen Zustand. Das Level lebt auf der einzelnen
/// <see cref="AutomationDevice"/>-Instanz — Upgrades laufen pro Gerät, nicht global pro Typ.
/// </summary>
[CreateAssetMenu(fileName = "NewAutomationDeviceData", menuName = "CozyCrops/Automation Device Data")]
public class AutomationDeviceData : ScriptableObject
{
    [Header("Identität")]
    public AutomationDeviceType deviceType;

    [Tooltip("Welche Werkzeug-Aktion dieses Gerät ausführt. Über dieses Feld — und nicht über " +
             "einen eigenen ToolType — hängt das Gerät an CanApplyTool und ApplyTool.")]
    public ToolType executesTool;

    public string displayName;
    public Sprite icon;

    [Tooltip("Prefab das beim Platzieren in die Welt gesetzt wird. Braucht einen Collider " +
             "(WorldClickHandler raycastet ohne LayerMask) und eine AutomationDevice-Komponente.")]
    public GameObject worldPrefab;

    [Header("Upgrade-Limits")]
    [Tooltip("Maximales Level das dieses Gerät erreichen kann.")]
    public int maxLevel = 20;

    [Header("Reichweite")]
    [Tooltip("Reichweiten-Radius auf Level 0 (Chebyshev, also quadratisch um das Gerät).\n" +
             "1 = 3×3, 2 = 5×5, 3 = 7×7 …")]
    public int baseRadius = 1;

    [Header("Takt (Sekunden)")]
    [Tooltip("Leerlauf ZWISCHEN zwei Jobs auf Level 0 — nicht die Dauer des Jobs selbst.")]
    public float baseInterval = 6f;

    [Tooltip("Um wie viel Sekunden das Intervall pro Level sinkt.")]
    public float intervalReductionPerLevel;

    [Tooltip("Minimales Intervall — wird nie unterschritten.")]
    public float minInterval = 2f;

    [Tooltip("Wie lange der Job selbst dauert. Bewusst level-unabhängig: die Progression " +
             "läuft über das Intervall, damit der Fortschrittsring auf der Kachel lesbar bleibt.\n\n" +
             "Wird STATT ToolRegistry.GetJobDuration verwendet — so läuft ein Gerät auch dann, " +
             "wenn der Spieler das zugehörige Werkzeug gar nicht besitzt.")]
    public float actionDuration = 2f;

    [Tooltip("Wie viele Kacheln pro Takt bearbeitet werden. Über Meilensteine steigerbar.")]
    public int baseTilesPerTick = 1;

    [Header("Kosten")]
    [Tooltip("Kaufpreis. Wird erst beim tatsächlichen Setzen abgezogen, nicht beim Auswählen.")]
    public int buyPrice = 200;

    [Tooltip("Goldkosten für das erste Upgrade (Level 0 → 1).")]
    public int baseCost = 60;

    [Tooltip("Zusätzliche Goldkosten pro Level.")]
    public int costScalingPerLevel = 15;

    [Header("Meilensteine")]
    [Tooltip("Besondere Effekte bei bestimmten Levels. Nach Level sortieren.")]
    public AutomationMilestone[] milestones;

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

    /// <summary>Leerlauf zwischen zwei Jobs für das gegebene Level.</summary>
    public float GetInterval(int level)
    {
        float interval = baseInterval - level * intervalReductionPerLevel;

        // Meilenstein-Faktor ist ein Override auf das Grundintervall, kein kumulativer
        // Multiplikator — sonst würden sich 0,75 und 0,5 zu 0,375 stapeln.
        float multiplier = 1f;
        if (milestones != null)
        {
            for (int i = 0; i < milestones.Length; i++)
            {
                var m = milestones[i];
                if (m == null) continue;
                if (m.level > level) break;
                if (m.intervalMultiplier > 0f) multiplier = m.intervalMultiplier;
            }
        }

        return Mathf.Max(interval * multiplier, minInterval);
    }

    /// <summary>Kacheln pro Takt für das gegebene Level. Mindestens 1.</summary>
    public int GetTilesPerTick(int level)
    {
        int tiles = baseTilesPerTick;

        if (milestones != null)
        {
            for (int i = 0; i < milestones.Length; i++)
            {
                var m = milestones[i];
                if (m == null) continue;
                if (m.level > level) break;
                if (m.tilesPerTick > 0) tiles = m.tilesPerTick;
            }
        }

        return Mathf.Max(1, tiles);
    }

    /// <summary>Kantenlänge der Reichweite in Kacheln — für Anzeigetexte wie "5×5".</summary>
    public int GetSideLength(int level) => GetRadius(level) * 2 + 1;

    /// <summary>Gibt den Meilenstein für exakt dieses Level zurück, oder null.</summary>
    public AutomationMilestone GetMilestoneAt(int level)
    {
        if (milestones == null) return null;
        foreach (var m in milestones)
            if (m != null && m.level == level) return m;
        return null;
    }
}

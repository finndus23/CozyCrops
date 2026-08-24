using UnityEngine;

/// <summary>
/// Definiert ein MODUL einer Automations-Station — eine Aktion der Kette.
/// Lege pro Modul ein eigenes Asset an: Rechtsklick → Create → CozyCrops → Automation Device Data
///
/// Aufbau exakt analog <see cref="ToolData"/>: alle Werte sind eine Funktion des Levels,
/// das Asset selbst hält keinen Zustand. Das Level lebt im eingesetzten
/// <see cref="AutomationModule"/> — aufgewertet wird pro Modul, nicht global pro Typ.
///
/// Die Reichweite steht hier bewusst NICHT. Sie gehört der Station
/// (<see cref="AutomationStationData"/>), damit alle Module denselben Mittelpunkt teilen und
/// die angezeigte Fläche auch die tatsächlich bearbeitete ist. Das Modul bestimmt nur, wie
/// schnell und wie viel auf dieser Fläche passiert.
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

    [Tooltip("Anbauteil, das am Gehäuse der Station erscheint, sobald dieses Modul " +
             "eingesetzt ist. Rein optisch — Collider und AutomationDevice-Komponente " +
             "braucht nur das Stations-Prefab. Optional: ohne Prefab funktioniert das " +
             "Modul, es ist nur nicht zu sehen.")]
    public GameObject worldPrefab;

    [Header("Upgrade-Limits")]
    [Tooltip("Maximales Level das dieses Gerät erreichen kann.")]
    public int maxLevel = 20;

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
    [Tooltip("Kaufpreis des Moduls. Wird beim Einsetzen in die Station abgebucht.")]
    public int buyPrice = 200;

    [Tooltip("Goldkosten für das erste Upgrade (Level 0 → 1).")]
    public int baseCost = 60;

    [Tooltip("Zusätzliche Goldkosten pro Level.")]
    public int costScalingPerLevel = 15;

    [Header("Meilensteine")]
    [Tooltip("Besondere Effekte bei bestimmten Levels. Nach Level sortieren. Für Module " +
             "zählen nur 'intervalMultiplier' und 'tilesPerTick' — ein 'radius' wird hier " +
             "ignoriert, der gehört auf das Stations-Asset.")]
    public AutomationMilestone[] milestones;

    // ── Berechnete Werte ──────────────────────────────────────────────────────

    /// <summary>Goldkosten um von (level) auf (level+1) zu upgraden. -1 wenn schon max.</summary>
    public int GetUpgradeCost(int currentLevel)
    {
        if (currentLevel >= maxLevel) return -1;
        return baseCost + currentLevel * costScalingPerLevel;
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

    /// <summary>Gibt den Meilenstein für exakt dieses Level zurück, oder null.</summary>
    public AutomationMilestone GetMilestoneAt(int level)
    {
        if (milestones == null) return null;
        foreach (var m in milestones)
            if (m != null && m.level == level) return m;
        return null;
    }
}

using UnityEngine;

/// <summary>
/// Definiert ein Tool mit allen Upgrade-Stufen.
/// Lege pro Tool ein eigenes Asset an: Rechtsklick → Create → CozyCrops → Tool Data
/// </summary>
[CreateAssetMenu(fileName = "NewToolData", menuName = "CozyCrops/Tool Data")]
public class ToolData : ScriptableObject
{
    [Header("Identität")]
    public ToolType toolType;
    public string displayName;
    public Sprite icon;

    [Header("Upgrade-Limits")]
    [Tooltip("Maximales Level das der Spieler erreichen kann.")]
    public int maxLevel = 30;

    [Header("AoE")]
    [Tooltip("Startgröße der Wirkungsfläche. 1 = 1×1, 2 = 2×2, 3 = 3×3 …")]
    public int baseAoSize = 1;

    [Header("Duration (Sekunden)")]
    [Tooltip("Wie lange dauert eine Aktion auf Level 0?")]
    public float baseDuration = 1f;

    [Tooltip("Um wie viel Sekunden sinkt die Duration pro Level?")]
    public float durationReductionPerLevel = 0.03f;

    [Tooltip("Minimale Duration — wird nie unterschritten.")]
    public float minDuration = 0.1f;

    [Header("Kosten")]
    [Tooltip("Goldkosten für das erste Upgrade (Level 0 → 1).")]
    public int baseCost = 50;

    [Tooltip("Zusätzliche Goldkosten pro Level.")]
    public int costScalingPerLevel = 10;

    [Header("Meilensteine")]
    [Tooltip("Besondere Effekte bei bestimmten Levels. Nach Level sortieren.")]
    public ToolMilestone[] milestones;

    // ── Berechnete Werte ──────────────────────────────────────────────────────

    /// <summary>Goldkosten um von (level) auf (level+1) zu upgraden.</summary>
    public int GetUpgradeCost(int currentLevel)
    {
        if (currentLevel >= maxLevel) return -1;
        return baseCost + currentLevel * costScalingPerLevel;
    }

    /// <summary>Aktuelle Duration in Sekunden für das gegebene Level.</summary>
    public float GetDuration(int level)
    {
        // Zuerst schauen ob ein Meilenstein die Duration überschreibt
        for (int i = milestones.Length - 1; i >= 0; i--)
        {
            var m = milestones[i];
            if (m.level <= level && m.durationOverride >= 0f)
                return m.durationOverride;
        }

        float calculated = baseDuration - level * durationReductionPerLevel;
        return Mathf.Max(calculated, minDuration);
    }

    /// <summary>Aktuelle AoE-Größe (Kantenlänge) für das gegebene Level.</summary>
    public int GetAoSize(int level)
    {
        int size = baseAoSize;

        for (int i = 0; i < milestones.Length; i++)
        {
            var m = milestones[i];
            if (m.level > level) break;
            if (m.aoSize > 0) size = m.aoSize;
        }

        return size;
    }

    /// <summary>Kumulierter Yield-Bonus (Sichel) für das gegebene Level.</summary>
    public int GetYieldBonus(int level)
    {
        int bonus = 0;

        for (int i = 0; i < milestones.Length; i++)
        {
            var m = milestones[i];
            if (m.level > level) break;
            bonus += m.yieldBonus;
        }

        return bonus;
    }

    /// <summary>Gibt den Meilenstein für exakt dieses Level zurück, oder null.</summary>
    public ToolMilestone GetMilestoneAt(int level)
    {
        foreach (var m in milestones)
            if (m.level == level) return m;
        return null;
    }
}

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

    [Tooltip("Zeit-Rabatt auf jedes ZUSÄTZLICHE Tile einer AoE-Aktion.\n\n" +
             "0 = jedes Tile kostet volle Zeit (9 Tiles = 9× so lang). Dann ist eine größere " +
             "Fläche nur Klick-Ersparnis, kein Tempo — ein AoE-Meilenstein fühlt sich damit " +
             "nicht wie ein Sprung an.\n" +
             "0,5 = jedes weitere Tile kostet die Hälfte (9 Tiles ≈ 5× statt 9×).\n" +
             "1 = alle Tiles in der Zeit von einem (sehr stark).")]
    [Range(0f, 1f)]
    public float aoeBatchDiscount = 0.5f;

    [Header("Warteschlange")]
    [Tooltip("Wie viele Aktionen mit DIESEM Werkzeug gleichzeitig eingeplant sein dürfen. " +
             "Über Meilensteine erhöhbar — eigener Progressionsstrang neben AoE und Tempo.")]
    public int baseQueueSize = 3;

    [Header("Duration (Sekunden)")]
    [Tooltip("Wie lange dauert eine Aktion auf Level 0?")]
    public float baseDuration = 1f;

    [Tooltip("Um wie viel Sekunden sinkt die Duration pro Level?")]
    public float durationReductionPerLevel = 0.03f;

    [Tooltip("Minimale Duration — wird nie unterschritten.")]
    public float minDuration = 0.1f;

    [Header("Kosten")]
    [Tooltip("Einmaliger Kaufpreis um das Tool freizuschalten.")]
    public int buyPrice = 50;

    [Tooltip("Goldkosten für das erste Upgrade (Level 0 → 1).")]
    public int baseCost = 50;

    [Tooltip("Zusätzliche Goldkosten pro Level.")]
    public int costScalingPerLevel = 10;

    [Header("Sound")]
    [Tooltip("Dauerton während die Aktion läuft (Wasserrauschen, Scharren). Optional.\n\n" +
             "Eine Aktion dauert auf Stufe 0 zwei Sekunden — kommt der Ton erst am Ende, " +
             "fühlen sich diese zwei Sekunden tot an. Derselbe Grund, aus dem es die " +
             "Castbar und den Fortschrittsring gibt.")]
    public AudioClip useLoop;

    [Tooltip("Wird abgespielt, wenn die Aktion fertig ist (Erde platscht, Sense schneidet).\n" +
             "Mehrere Varianten eintragen: es wird abwechselnd gezogen, nie zweimal " +
             "dieselbe hintereinander.")]
    public AudioClip[] impactClips;

    [Tooltip("Lautstärke der Werkzeug-Sounds, 0–1.")]
    [Range(0f, 1f)]
    public float sfxVolume = 1f;

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

    /// <summary>
    /// Gesamtdauer einer Aktion über <paramref name="tileCount"/> Tiles.
    ///
    /// Das erste Tile kostet volle Zeit, jedes weitere nur noch (1 - aoeBatchDiscount).
    /// Damit wird eine größere Wirkungsfläche zum echten Durchsatz-Sprung statt bloß
    /// Klicks zu sparen — sonst wäre der AoE-Meilenstein bei Stufe 10 zeitlich wirkungslos.
    /// </summary>
    public float GetJobDuration(int level, int tileCount)
    {
        float perTile = GetDuration(level);
        int n = Mathf.Max(1, tileCount);
        float extraFactor = 1f - Mathf.Clamp01(aoeBatchDiscount);

        return perTile * (1f + (n - 1) * extraFactor);
    }

    /// <summary>Warteschlangen-Größe dieses Werkzeugs für das gegebene Level.</summary>
    public int GetQueueSize(int level)
    {
        int size = baseQueueSize;

        for (int i = 0; i < milestones.Length; i++)
        {
            var m = milestones[i];
            if (m.level > level) break;
            if (m.queueSize > 0) size = m.queueSize;
        }

        return Mathf.Max(1, size);
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

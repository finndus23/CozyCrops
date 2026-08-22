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

    [Tooltip("Was jedes ZUSÄTZLICHE Tile einer AoE-Aktion kostet, gemessen an einem einzelnen.\n\n" +
             "1 = 9 Tiles dauern exakt so lang wie 9 einzelne Aktionen. Die Wirkungsfläche " +
             "spart dann nur Klicks, keine Zeit.\n" +
             "0,9 = jedes weitere Tile kostet 90 % — ein kleiner Bonus obendrauf, damit sich " +
             "die Aufwertung auch in der Zeit bemerkbar macht.\n" +
             "0,5 = jedes weitere Tile kostet die Hälfte (9 Tiles ≈ 5× statt 9×).\n\n" +
             "Niedrige Werte machen große Flächen sehr stark, aber einzelne Tiles im " +
             "Vergleich unattraktiv. Das eigentliche Tempo soll über die sinkende Base " +
             "Duration beim Leveln kommen, nicht hierüber.")]
    [Range(0.1f, 1f)]
    public float extraTileCost = 0.9f;

    [Header("Warteschlange")]
    [Tooltip("Wie viele Aktionen mit DIESEM Werkzeug gleichzeitig eingeplant sein dürfen. " +
             "Über Meilensteine erhöhbar — eigener Progressionsstrang neben AoE und Tempo.")]
    public int baseQueueSize = 3;

    [Tooltip("Wie viele Gießungen ein einzelner Einsatz auf Level 0 zählt (nur Gießkanne " +
             "sinnvoll). Über Meilensteine steigerbar.")]
    public int baseWateringPower = 1;

    [Tooltip("Zeitgewinn beim Mehrfach-Gießen, 0,5–1.\n\n" +
             "Eine Kanne mit Gießkraft 2 erledigt zwei Gießungen auf einmal — und braucht " +
             "dafür grundsätzlich auch die doppelte Zeit. Sonst würde der Meilenstein den " +
             "Gießbedarf einer Pflanze einfach halbieren, statt Arbeit zu bündeln.\n\n" +
             "Dieser Faktor gibt den Rabatt darauf: 1 = exakt so lang wie zwei einzelne " +
             "Gießungen, der Gewinn liegt allein im gesparten Klick. 0,85 = zusätzlich 15 % " +
             "schneller, damit sich die Aufwertung auch in der Zeit bemerkbar macht.")]
    [Range(0.5f, 1f)]
    public float multiWateringEfficiency = 0.85f;

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
    /// Wie viele Gießungen ein Einsatz auf dem gegebenen Level zählt. Mindestens 1.
    /// </summary>
    public int GetWateringPower(int level)
    {
        int power = Mathf.Max(1, baseWateringPower);

        for (int i = 0; i < milestones.Length; i++)
        {
            var m = milestones[i];
            if (m.level > level) break;
            if (m.wateringPower > 0) power = m.wateringPower;
        }

        return power;
    }

    /// <summary>
    /// Gesamtdauer einer Aktion über <paramref name="tileCount"/> Tiles.
    ///
    /// Das erste Tile kostet volle Zeit, jedes weitere <see cref="extraTileCost"/> davon.
    /// Bei einem Wert nahe 1 kostet die volle Fläche also ungefähr so viel wie die
    /// entsprechende Zahl einzelner Aktionen — die Wirkungsfläche spart dann vor allem
    /// Klickarbeit.
    ///
    /// Das ist Absicht: käme das Tempo aus dem Mengenrabatt, wäre ein einzelnes Tile
    /// dauerhaft die schlechteste Art zu arbeiten, und man würde Klicks aufsparen, bis die
    /// Fläche voll ist. Schneller wird die Arbeit stattdessen über die mit dem Level
    /// sinkende Zeit pro Tile — davon profitiert auch, wer nur eine Pflanze gießt.
    /// </summary>
    public float GetJobDuration(int level, int tileCount)
    {
        float perTile = GetDuration(level);
        int n = Mathf.Max(1, tileCount);
        float extraFactor = Mathf.Clamp(extraTileCost, 0.1f, 1f);

        float duration = perTile * (1f + (n - 1) * extraFactor);

        // Mehrfach-Gießen kostet auch mehrfach Zeit, abzüglich eines Rabatts. Käme die
        // zweite Gießung gratis, wäre der Meilenstein keine gebündelte Arbeit mehr, sondern
        // eine halbierte Anforderung — und Pflanzen mit hohem Gießbedarf verlören ihren
        // Charakter komplett.
        int power = GetWateringPower(level);
        if (power > 1)
            duration *= power * Mathf.Clamp(multiWateringEfficiency, 0.5f, 1f);

        return duration;
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

    [Header("Dünger-Ernte-Chance (nur beim Dünger-Werkzeug relevant)")]
    [Tooltip("Chance, dass die Ernte eines GEDÜNGTEN Feldes einen Extra-Crop abwirft — " +
             "linear mit dem Level dieses Werkzeugs (also dem Dünger-Tool), nicht mit dem " +
             "der Sichel. Ein RNG-Bonus statt eines garantierten Extra-Items, damit sich " +
             "Aufwerten lohnt, ohne dass jede einzelne Ernte planbar größer wird.")]
    [Range(0f, 1f)]
    public float baseFertilizedHarvestChance = 0.2f;

    [Tooltip("Zusätzliche Chance pro Level, bis zur Obergrenze unten.")]
    [Range(0f, 0.05f)]
    public float fertilizedHarvestChancePerLevel = 0.01f;

    [Tooltip("Obergrenze der Chance, egal wie hoch das Level.")]
    [Range(0f, 1f)]
    public float maxFertilizedHarvestChance = 0.5f;

    /// <summary>Chance auf den Dünger-Ernte-Bonus beim gegebenen Level dieses Werkzeugs.</summary>
    public float GetFertilizedHarvestChance(int level)
    {
        float chance = baseFertilizedHarvestChance + level * fertilizedHarvestChancePerLevel;
        return Mathf.Clamp(chance, 0f, maxFertilizedHarvestChance);
    }

    [Header("Sichel-Ernte-Bonus-Chance (nur bei der Sichel relevant)")]
    [Tooltip("Chance auf einen Extra-Crop bei JEDER Ernte (nicht nur gedüngte Felder) — " +
             "linear mit dem Level DIESES Werkzeugs (der Sichel). Ersetzt den alten " +
             "garantierten Meilenstein-Ertragsbonus (war bei Level 10+ dauerhaft +1, ab " +
             "Level 30 +3 — zu stark, weil planbar statt Glückssache). 0% auf Level 0, " +
             "steigt um 1%/Level bis zur Obergrenze.")]
    [Range(0f, 1f)]
    public float baseYieldBonusChance;

    [Tooltip("Zusätzliche Chance pro Level, bis zur Obergrenze unten.")]
    [Range(0f, 0.05f)]
    public float yieldBonusChancePerLevel = 0.01f;

    [Tooltip("Obergrenze der Chance, egal wie hoch das Level.")]
    [Range(0f, 1f)]
    public float maxYieldBonusChance = 0.3f;

    /// <summary>Chance auf den Sichel-Ernte-Bonus beim gegebenen Level dieses Werkzeugs.</summary>
    public float GetYieldBonusChance(int level)
    {
        float chance = baseYieldBonusChance + level * yieldBonusChancePerLevel;
        return Mathf.Clamp(chance, 0f, maxYieldBonusChance);
    }

    /// <summary>Gibt den Meilenstein für exakt dieses Level zurück, oder null.</summary>
    public ToolMilestone GetMilestoneAt(int level)
    {
        foreach (var m in milestones)
            if (m.level == level) return m;
        return null;
    }
}

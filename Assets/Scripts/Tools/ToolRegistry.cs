using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton — hält alle ToolData-Assets und den aktuellen Upgrade-Level pro Tool.
/// Gibt auf Anfrage die aktuellen Stats (Duration, AoSize, YieldBonus, Kosten) zurück.
/// </summary>
public class ToolRegistry : MonoBehaviour
{
    public static ToolRegistry Instance { get; private set; }

    [Header("Tool-Definitionen")]
    [Tooltip("Alle ToolData-Assets hierher ziehen — je eines pro Tool.")]
    [SerializeField] private ToolData[] tools;

    // Aktueller Level pro ToolType (0 = kein Upgrade)
    private readonly Dictionary<ToolType, int> levels = new();
    private readonly Dictionary<ToolType, ToolData> dataMap = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        foreach (var data in tools)
        {
            if (data == null) continue;
            dataMap[data.toolType] = data;
            levels[data.toolType]  = 0;
        }
    }

    // ── Level-Abfragen ────────────────────────────────────────────────────────

    public int GetLevel(ToolType tool) =>
        levels.TryGetValue(tool, out int lvl) ? lvl : 0;

    public ToolData GetData(ToolType tool) =>
        dataMap.TryGetValue(tool, out var d) ? d : null;

    // ── Stats für aktuelles Level ─────────────────────────────────────────────

    public float GetDuration(ToolType tool)
    {
        var data = GetData(tool);
        return data != null ? data.GetDuration(GetLevel(tool)) : 0f;
    }

    public int GetAoSize(ToolType tool)
    {
        var data = GetData(tool);
        return data != null ? data.GetAoSize(GetLevel(tool)) : 1;
    }

    public int GetYieldBonus(ToolType tool)
    {
        var data = GetData(tool);
        return data != null ? data.GetYieldBonus(GetLevel(tool)) : 0;
    }

    /// <summary>
    /// Kosten für das nächste Upgrade. Gibt -1 zurück wenn bereits MaxLevel.
    /// </summary>
    public int GetUpgradeCost(ToolType tool)
    {
        var data = GetData(tool);
        return data != null ? data.GetUpgradeCost(GetLevel(tool)) : -1;
    }

    public bool IsMaxLevel(ToolType tool)
    {
        var data = GetData(tool);
        return data != null && GetLevel(tool) >= data.maxLevel;
    }

    // ── Upgrade ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Erhöht den Level eines Tools um 1.
    /// Gibt false zurück wenn MaxLevel erreicht.
    /// Speichert automatisch.
    /// </summary>
    public bool TryUpgrade(ToolType tool)
    {
        if (IsMaxLevel(tool)) return false;
        if (!levels.ContainsKey(tool)) return false;

        levels[tool]++;

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();

        return true;
    }

    // ── Save / Load ───────────────────────────────────────────────────────────

    /// <summary>Wird vom FarmSaveManager beim Speichern aufgerufen.</summary>
    public List<ToolLevelSaveData> GetSaveData()
    {
        var list = new List<ToolLevelSaveData>();
        foreach (var kvp in levels)
            list.Add(new ToolLevelSaveData { toolType = kvp.Key.ToString(), level = kvp.Value });
        return list;
    }

    /// <summary>Wird vom FarmSaveManager beim Laden aufgerufen.</summary>
    public void ApplyLoadedData(List<ToolLevelSaveData> loaded)
    {
        if (loaded == null) return;

        foreach (var entry in loaded)
        {
            if (System.Enum.TryParse<ToolType>(entry.toolType, out var tool))
                levels[tool] = Mathf.Clamp(entry.level, 0, dataMap.TryGetValue(tool, out var d) ? d.maxLevel : 0);
        }

        Debug.Log($"[ToolRegistry] {loaded.Count} Tool-Level(s) geladen.");
    }
}

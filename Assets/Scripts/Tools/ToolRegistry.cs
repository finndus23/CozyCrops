using System.Collections.Generic;
using System.Linq;
using System.Text;
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

    [Header("Start-Level (Inspector)")]
    [Tooltip("Hier Level direkt setzen — werden beim Start angewendet. Nützlich zum Testen.")]
    [SerializeField] private ToolLevelEntry[] startLevels;

    [Header("Stats-Vorschau (Read-only)")]
    [Tooltip("ContextMenu → 'Stats anzeigen' um zu aktualisieren.")]
    [Multiline(20)]
    [SerializeField] private string statsPreview = "(Rechtsklick auf Komponente → Stats anzeigen)";

    // Aktueller Level pro ToolType (0 = kein Upgrade)
    private readonly Dictionary<ToolType, int> levels  = new();
    private readonly Dictionary<ToolType, ToolData> dataMap = new();
    private readonly HashSet<ToolType> ownedTools = new();

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

        // Inspector-Startwerte anwenden
        if (startLevels != null)
        {
            foreach (var entry in startLevels)
            {
                if (dataMap.ContainsKey(entry.toolType))
                    levels[entry.toolType] = Mathf.Clamp(entry.level, 0, dataMap[entry.toolType].maxLevel);
            }
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

    // ── Tool besitzen ─────────────────────────────────────────────────────────

    /// <summary>Feuert wenn owned Tools sich ändern (Kauf oder Save-Load).</summary>
    public event System.Action OnOwnedToolsChanged;

    public bool IsOwned(ToolType tool) => ownedTools.Contains(tool);

    public void OwnTool(ToolType tool)
    {
        ownedTools.Add(tool);
        OnOwnedToolsChanged?.Invoke();
        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();
    }

    // ── Upgrade ───────────────────────────────────────────────────────────────

    public bool TryUpgrade(ToolType tool)
    {
        if (IsMaxLevel(tool)) return false;
        if (!levels.ContainsKey(tool)) return false;

        levels[tool]++;

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();

        return true;
    }

    // ── Debug / Inspector ────────────────────────────────────────────────────

    [ContextMenu("Debug: Alle Tools +1 Level")]
    private void DebugLevelUpAll()
    {
        foreach (var tool in levels.Keys.ToList())
            TryUpgrade(tool);
        RefreshStatsPreview();
        Debug.Log("[ToolRegistry] Alle Tools +1 Level.");
    }

    [ContextMenu("Debug: Alle Tools auf Level 0 reset")]
    private void DebugResetAll()
    {
        foreach (var key in levels.Keys.ToList())
            levels[key] = 0;
        RefreshStatsPreview();
        Debug.Log("[ToolRegistry] Alle Tool-Level zurückgesetzt.");
    }

    [ContextMenu("Stats anzeigen")]
    private void RefreshStatsPreview()
    {
        var sb = new StringBuilder();

        foreach (var kvp in levels)
        {
            var data = GetData(kvp.Key);
            if (data == null) continue;

            int  lvl    = kvp.Value;
            int  ao     = data.GetAoSize(lvl);
            int  tiles  = ao * ao;

            sb.AppendLine($"── {kvp.Key}  (Lvl {lvl} / {data.maxLevel}) ──");
            sb.AppendLine($"  Duration/Tile : {data.GetDuration(lvl):F2}s");
            sb.AppendLine($"  AoE           : {ao}×{ao}  ({tiles} Tiles)");
            sb.AppendLine($"  Gesamt-Dauer  : {data.GetDuration(lvl) * tiles:F2}s");
            sb.AppendLine($"  Yield-Bonus   : +{data.GetYieldBonus(lvl)}");

            int cost = data.GetUpgradeCost(lvl);
            sb.AppendLine(cost >= 0 ? $"  Nächstes Lvl  : {cost} G" : "  MAX LEVEL");

            var milestone = data.GetMilestoneAt(lvl);
            if (milestone != null && !string.IsNullOrEmpty(milestone.unlockText))
                sb.AppendLine($"  ★ {milestone.unlockText}");

            sb.AppendLine();
        }

        statsPreview = sb.Length > 0 ? sb.ToString() : "(Keine Tools geladen)";
    }

    // ── Save / Load ───────────────────────────────────────────────────────────

    public List<ToolLevelSaveData> GetSaveData()
    {
        var list = new List<ToolLevelSaveData>();
        foreach (var kvp in levels)
            list.Add(new ToolLevelSaveData { toolType = kvp.Key.ToString(), level = kvp.Value });
        return list;
    }

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

    public List<string> GetOwnedToolsSaveData()
    {
        var list = new List<string>();
        foreach (var tool in ownedTools)
            list.Add(tool.ToString());
        return list;
    }

    public void ApplyOwnedToolsData(List<string> loaded)
    {
        if (loaded == null) return;
        ownedTools.Clear();
        foreach (var entry in loaded)
            if (System.Enum.TryParse<ToolType>(entry, out var tool))
                ownedTools.Add(tool);
        OnOwnedToolsChanged?.Invoke();
    }
}

[System.Serializable]
public class ToolLevelEntry
{
    public ToolType toolType;
    [Range(0, 99)] public int level;
}

using System;
using UnityEngine;

/// <summary>
/// Reiner State-Manager — kein Input, kein UI.
/// Input läuft über HotbarUI, visuals über HotbarSlotUI.
/// </summary>
public class Hotbar : MonoBehaviour
{
    public static Hotbar Instance { get; private set; }

    public ToolType ActiveTool { get; private set; } = ToolType.None;
    public PlantType SelectedSeed { get; private set; }

    public event Action<ToolType> OnToolChanged;
    public event Action<PlantType> OnSeedChanged;

    public static event Action<ToolType> OnToolSelectedStatic;

    void Awake() => Instance = this;

    public void SetTool(ToolType tool)
    {
        ActiveTool = tool;
        OnToolChanged?.Invoke(tool);
        if (tool != ToolType.None)
        {
            Debug.Log($"[Hotbar] SetTool({tool}) → OnToolSelectedStatic feuert");
            OnToolSelectedStatic?.Invoke(tool);
        }
        else
        {
            Debug.Log($"[Hotbar] SetTool(None) → kein Event");
        }
    }

    public void SetSeed(PlantType type)
    {
        SelectedSeed = type;
        OnSeedChanged?.Invoke(type);
    }
}

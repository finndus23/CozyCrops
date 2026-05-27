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

    void Awake() => Instance = this;

    public void SetTool(ToolType tool)
    {
        ActiveTool = tool;
        OnToolChanged?.Invoke(tool);
    }

    public void SetSeed(PlantType type)
    {
        SelectedSeed = type;
        OnSeedChanged?.Invoke(type);
    }
}

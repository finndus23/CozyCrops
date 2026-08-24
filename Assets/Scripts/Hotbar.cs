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

    // Überlebt den Szenenwechsel: Hotbar liegt in der Farm-Szene und entsteht bei jedem
    // Betreten neu — ActiveTool/SelectedSeed fielen sonst jedes Mal auf None/null zurück.
    // Static hält den zuletzt aktiven Stand für die ganze Session, genau wie
    // SoftlockHintTrigger.alreadyShown es für seinen eigenen Zweck schon tut.
    private static ToolType rememberedTool = ToolType.None;
    private static PlantType rememberedSeed;

    void Awake()
    {
        Instance = this;

        // Direkt zuweisen statt über SetTool()/SetSeed(): die Listener (HotbarUI etc.)
        // haben sich in diesem Frame noch nicht angemeldet, ein Event liefe ins Leere.
        // HotbarUI liest ActiveTool/SelectedSeed in seinem eigenen Start() ohnehin selbst
        // ab (UpdateHighlight/UpdateSeedSlot) — Awake läuft garantiert vor jedem Start.
        ActiveTool = rememberedTool;
        SelectedSeed = rememberedSeed;
    }

    public void SetTool(ToolType tool)
    {
        ActiveTool = tool;
        rememberedTool = tool;
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
        rememberedSeed = type;
        OnSeedChanged?.Invoke(type);
    }
}

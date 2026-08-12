using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class BuildModeManager : MonoBehaviour
{
    public static BuildModeManager Instance { get; private set; }
    public bool IsActive { get; private set; }
    public TileType SelectedTileType { get; private set; } = TileType.FarmPlot;

    public event Action<bool> OnBuildModeChanged;
    public event Action<TileType> OnSelectedTileChanged;

    public static event Action OnBuildModeEnteredStatic;
    public static event Action OnBuildModeExitedStatic;

    void Awake() => Instance = this;

    void Update()
    {
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen)
            return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.bKey.wasPressedThisFrame)
            Toggle();
    }

    public void Toggle()
    {
        if (TutorialManager.Instance?.IsBlocked(TutorialBlockedAction.BuildModeToggle) == true)
            return;

        SetActive(!IsActive);
    }

    public void SelectTile(TileType tileType)
    {
        if (SelectedTileType == tileType)
            return;

        SelectedTileType = tileType;
        OnSelectedTileChanged?.Invoke(tileType);
    }

    public void SetActive(bool active)
    {
        if (IsActive == active)
            return;

        IsActive = active;
        if (!active)
            SelectionManager.Instance.ClearSelection();
        OnBuildModeChanged?.Invoke(active);

        if (active) OnBuildModeEnteredStatic?.Invoke();
        else        OnBuildModeExitedStatic?.Invoke();
    }
}

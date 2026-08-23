using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class BuildModeManager : MonoBehaviour
{
    public static BuildModeManager Instance { get; private set; }
    public bool IsActive { get; private set; }
    public TileType SelectedTileType { get; private set; } = TileType.FarmPlot;

    /// <summary>
    /// Die Auswahl im Baumodus ist eine Union: entweder ein TileType zum Bemalen ODER die
    /// Automations-Station zum Setzen, nie beides.
    /// </summary>
    public bool IsStationSelected { get; private set; }

    public event Action<bool> OnBuildModeChanged;
    public event Action<TileType> OnSelectedTileChanged;
    public event Action<bool> OnStationSelectionChanged;

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
        // Kachel gewaehlt heisst: Station nicht mehr ausgewaehlt.
        ClearStationSelection();

        if (SelectedTileType == tileType)
            return;

        SelectedTileType = tileType;
        OnSelectedTileChanged?.Invoke(tileType);
    }

    /// <summary>
    /// Waehlt die Automations-Station zum Setzen. Feuert bewusst auch bei erneutem Klick —
    /// so startet ein zweiter Klick die Platzierung neu, statt wirkungslos zu sein.
    /// </summary>
    public void SelectStation()
    {
        IsStationSelected = true;
        OnStationSelectionChanged?.Invoke(true);
    }

    /// <summary>Hebt die Stations-Auswahl auf — z.B. wenn eine Platzierung abgebrochen wird.</summary>
    public void ClearStationSelection()
    {
        if (!IsStationSelected) return;

        IsStationSelected = false;
        OnStationSelectionChanged?.Invoke(false);
    }

    public void SetActive(bool active)
    {
        if (IsActive == active)
            return;

        IsActive = active;
        if (!active)
        {
            SelectionManager.Instance.ClearSelection();
            ClearStationSelection();
        }
        OnBuildModeChanged?.Invoke(active);

        if (active) OnBuildModeEnteredStatic?.Invoke();
        else        OnBuildModeExitedStatic?.Invoke();
    }
}

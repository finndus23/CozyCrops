using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class BuildModeManager : MonoBehaviour
{
    public static BuildModeManager Instance { get; private set; }
    public bool IsActive { get; private set; }
    public TileType SelectedTileType { get; private set; } = TileType.FarmPlot;

    /// <summary>
    /// Die Auswahl im Baumodus ist eine Union: entweder ein TileType zum Bemalen ODER ein
    /// Automatik-Geraet zum Setzen, nie beides. None heisst "Kachel-Auswahl aktiv".
    /// </summary>
    public AutomationDeviceType SelectedDeviceType { get; private set; } = AutomationDeviceType.None;

    public bool IsDeviceSelected => SelectedDeviceType != AutomationDeviceType.None;

    public event Action<bool> OnBuildModeChanged;
    public event Action<TileType> OnSelectedTileChanged;
    public event Action<AutomationDeviceType> OnSelectedDeviceChanged;

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
        // Kachel gewaehlt heisst: kein Geraet mehr ausgewaehlt.
        if (SelectedDeviceType != AutomationDeviceType.None)
        {
            SelectedDeviceType = AutomationDeviceType.None;
            OnSelectedDeviceChanged?.Invoke(AutomationDeviceType.None);
        }

        if (SelectedTileType == tileType)
            return;

        SelectedTileType = tileType;
        OnSelectedTileChanged?.Invoke(tileType);
    }

    /// <summary>
    /// Waehlt ein Automatik-Geraet zum Setzen. Feuert bewusst auch dann, wenn derselbe Typ
    /// erneut geklickt wird — so startet ein zweiter Klick die Platzierung neu, statt
    /// wirkungslos zu sein.
    /// </summary>
    public void SelectDevice(AutomationDeviceType deviceType)
    {
        SelectedDeviceType = deviceType;
        OnSelectedDeviceChanged?.Invoke(deviceType);
    }

    /// <summary>Hebt die Geraete-Auswahl auf — z.B. wenn eine Platzierung abgebrochen wird.</summary>
    public void ClearDeviceSelection()
    {
        if (SelectedDeviceType == AutomationDeviceType.None) return;

        SelectedDeviceType = AutomationDeviceType.None;
        OnSelectedDeviceChanged?.Invoke(AutomationDeviceType.None);
    }

    public void SetActive(bool active)
    {
        if (IsActive == active)
            return;

        IsActive = active;
        if (!active)
        {
            SelectionManager.Instance.ClearSelection();

            if (SelectedDeviceType != AutomationDeviceType.None)
            {
                SelectedDeviceType = AutomationDeviceType.None;
                OnSelectedDeviceChanged?.Invoke(AutomationDeviceType.None);
            }
        }
        OnBuildModeChanged?.Invoke(active);

        if (active) OnBuildModeEnteredStatic?.Invoke();
        else        OnBuildModeExitedStatic?.Invoke();
    }
}

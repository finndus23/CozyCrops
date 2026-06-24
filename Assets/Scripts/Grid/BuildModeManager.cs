using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class BuildModeManager : MonoBehaviour
{
    public static BuildModeManager Instance { get; private set; }
    public bool IsActive { get; private set; }

    public event Action<bool> OnBuildModeChanged;

    public static event Action OnBuildModeEnteredStatic;
    public static event Action OnBuildModeExitedStatic;

    void Awake() => Instance = this;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.bKey.wasPressedThisFrame)
        {
            if (TutorialManager.Instance?.IsBlocked(TutorialBlockedAction.BuildModeToggle) == true) return;
            SetActive(!IsActive);
        }
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        if (!active)
            SelectionManager.Instance.ClearSelection();
        OnBuildModeChanged?.Invoke(active);

        if (active) OnBuildModeEnteredStatic?.Invoke();
        else        OnBuildModeExitedStatic?.Invoke();
    }
}

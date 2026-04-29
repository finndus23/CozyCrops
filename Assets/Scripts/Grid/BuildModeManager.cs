using UnityEngine;
using UnityEngine.InputSystem;

public class BuildModeManager : MonoBehaviour
{
    public static BuildModeManager Instance { get; private set; }
    public bool IsActive { get; private set; }

    void Awake() => Instance = this;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.bKey.wasPressedThisFrame)
            SetActive(!IsActive);
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        if (!active)
            SelectionManager.Instance.ClearSelection();
    }
}

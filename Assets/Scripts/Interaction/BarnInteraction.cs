using UnityEngine;

/// <summary>
/// Auf das Scheune-3D-Objekt legen.
/// Klick → öffnet/schließt die Ernte-Übersicht.
/// Im Build-Modus blockiert.
/// </summary>
public class BarnInteraction : MonoBehaviour, IClickable
{
    public static event System.Action OnBarnOpenedStatic;

    public void OnClick()
    {
        if (BuildModeManager.Instance.IsActive) return;
        if (TutorialManager.Instance?.IsBlocked(TutorialBlockedAction.BarnAccess) == true) return;

        InventoryUI.Instance.Toggle();
        OnBarnOpenedStatic?.Invoke();
    }
}

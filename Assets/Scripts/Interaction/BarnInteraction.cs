using UnityEngine;

/// <summary>
/// Auf das Scheune-3D-Objekt legen.
/// Klick → öffnet/schließt die Ernte-Übersicht.
/// Im Build-Modus blockiert.
/// </summary>
public class BarnInteraction : MonoBehaviour, IClickable
{
    public static event System.Action OnBarnOpenedStatic;

    /// <summary>Aktive Scheune in der Szene (für "Crop fliegt in die Scheune"-Effekt).</summary>
    public static BarnInteraction Instance { get; private set; }

    [Tooltip("Zielpunkt, zu dem geerntete Crops fliegen. Leer = Position der Scheune selbst.")]
    [SerializeField] private Transform collectPoint;

    /// <summary>Weltposition, zu der geerntete Crops fliegen sollen.</summary>
    public Vector3 CollectPoint => collectPoint != null ? collectPoint.position : transform.position;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void OnClick()
    {
        if (BuildModeManager.Instance.IsActive) return;
        if (TutorialManager.Instance?.IsBlocked(TutorialBlockedAction.BarnAccess) == true) return;

        InventoryUI.Instance.Toggle();
        OnBarnOpenedStatic?.Invoke();
    }
}

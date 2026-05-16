using UnityEngine;

/// <summary>
/// Auf das Blocker-Prefab legen (Child der GridZone).
/// Findet die zugehörige GridZone automatisch im Parent.
/// Klick → versucht Zone freizuschalten.
/// </summary>
public class ZoneBlocker : MonoBehaviour, IClickable
{
    private GridZone zone;

    void Awake()
    {
        zone = GetComponentInParent<GridZone>();
        if (zone == null)
            Debug.LogWarning($"[ZoneBlocker] Kein GridZone im Parent von '{gameObject.name}' gefunden.");
    }

    public void OnClick()
    {
        if (zone == null) return;

        bool success = ZoneManager.Instance.TryUnlockZone(zone);
        if (!success)
            Debug.Log($"[ZoneBlocker] Zone '{zone.gameObject.name}' konnte nicht freigeschaltet werden " +
                      $"(zu wenig Gold oder bereits offen). Kosten: {zone.unlockCost} G");
    }
}

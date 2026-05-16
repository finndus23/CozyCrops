using System;
using UnityEngine;

/// <summary>
/// Auf ein leeres GameObject legen + BoxCollider hinzufügen.
/// Alle Kinder des GameObjects gelten als Blocker (Zaun, Felsen, …)
/// und werden bei Unlock() automatisch zerstört.
///
/// Tipp: BoxCollider immer auf ganzen Tile-Grenzen lassen (z.B. 5×5, 3×4 Units),
/// damit die Tile-Coverage sauber passt.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class GridZone : MonoBehaviour
{
    [Header("Konfiguration")]
    [Tooltip("Kosten in Gold um diese Zone freizuschalten.")]
    [SerializeField] public int unlockCost = 500;

    [Header("Gizmo")]
    [SerializeField] private Color lockedColor   = new Color(1f, 0.2f, 0.2f, 0.25f);
    [SerializeField] private Color unlockedColor = new Color(0.2f, 1f, 0.2f, 0.15f);

    public bool IsUnlocked { get; private set; }
    public event Action OnUnlocked;

    private BoxCollider zoneCollider;

    void Awake()
    {
        zoneCollider = GetComponent<BoxCollider>();
        zoneCollider.isTrigger = true; // Kein Physics — nur Bounds-Detection
    }

    /// <summary>Liegt der Tile-Mittelpunkt innerhalb dieser Zone?</summary>
    public bool ContainsTile(Vector3 worldPos) =>
        zoneCollider.bounds.Contains(worldPos);

    /// <summary>
    /// Versucht die Zone freizuschalten (zieht Gold ab).
    /// Gibt false zurück wenn bereits freigeschaltet oder zu wenig Gold.
    /// </summary>
    public bool TryUnlock()
    {
        if (IsUnlocked) return false;
        if (!PlayerInventory.Instance.TrySpendMoney(unlockCost)) return false;

        Unlock();
        return true;
    }

    /// <summary>Schaltet die Zone ohne Kosten frei (z.B. für Debug / Cutscenes).</summary>
    public void Unlock()
    {
        if (IsUnlocked) return;

        IsUnlocked = true;

        // Alle Kinder-Objekte (Blocker) zerstören
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        OnUnlocked?.Invoke();
        Debug.Log($"[GridZone] '{gameObject.name}' freigeschaltet.");
    }

    // ── Scene View Gizmo ──────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color  = IsUnlocked ? unlockedColor : lockedColor;
        Gizmos.DrawCube(col.center, col.size);

        // Rand etwas dunkler für bessere Sichtbarkeit
        Gizmos.color = IsUnlocked
            ? new Color(0.2f, 1f, 0.2f, 0.6f)
            : new Color(1f, 0.2f, 0.2f, 0.6f);
        Gizmos.DrawWireCube(col.center, col.size);
    }
}

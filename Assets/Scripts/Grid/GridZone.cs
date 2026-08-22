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
    [SerializeField] private Color lockedColor = new Color(1f, 0.2f, 0.2f, 0.25f);
    [SerializeField] private Color unlockedColor = new Color(0.2f, 1f, 0.2f, 0.15f);

    public bool IsUnlocked { get; private set; }

    [Header("Save")]
    [Tooltip("Eindeutige ID für Savegames. Leer lassen = GameObject-Name wird benutzt. Nicht später ändern.")]
    [SerializeField] private string saveId;
    public string SaveId => string.IsNullOrWhiteSpace(saveId) ? gameObject.name : saveId;

    public event Action OnUnlocked;

    /// <summary>
    /// Statisches Event fürs Missions-System. Feuert bewusst NUR in TryUnlock(), also wenn
    /// der Spieler die Zone selbst bezahlt hat — nicht bei Unlock() aus einem Missions-Reward
    /// oder beim Laden. Sonst würde eine geschenkte Zone das Ziel "schalte eine Zone frei"
    /// von selbst abhaken.
    /// </summary>
    public static event Action<string> OnZonePurchasedStatic;

    private BoxCollider zoneCollider;

    void Awake()
    {
        zoneCollider = GetComponent<BoxCollider>();
        zoneCollider.isTrigger = true; // Kein Physics — nur Bounds-Detection
    }

    /// <summary>Konfiguriert eine zur Laufzeit erzeugte Erweiterungszone.</summary>
    public void Configure(string id, int cost, Vector3 worldCenter, Vector3 worldSize)
    {
        saveId = id;
        unlockCost = Mathf.Max(0, cost);
        transform.position = worldCenter;

        zoneCollider ??= GetComponent<BoxCollider>();
        zoneCollider.center = Vector3.zero;
        zoneCollider.size = worldSize;
        zoneCollider.isTrigger = true;
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

        ZoneManager.Instance?.PrepareZonePurchase(this);
        Unlock();
        OnZonePurchasedStatic?.Invoke(SaveId);
        return true;
    }

    /// <summary>Schaltet die Zone ohne Kosten frei (z.B. für Debug / Cutscenes).</summary>
    public void Unlock()
    {
        if (IsUnlocked) return;

        IsUnlocked = true;

        // Alle Kinder-Objekte (Button, Vorschau und Naturdeko) sofort ausblenden und
        // anschließend zerstören. Destroy() allein würde sie noch bis zum Frame-Ende zeigen.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        OnUnlocked?.Invoke();
        // Erst nach dem Event deaktivieren: ZoneManager braucht die Bounds noch, um die
        // zugehörigen Tiles zu entsperren. Danach soll die unsichtbare Klickfläche keine
        // benachbarten bzw. überlappenden Erweiterungszonen mehr verdecken.
        zoneCollider.enabled = false;
        Debug.Log($"[GridZone] '{gameObject.name}' freigeschaltet.");

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();
    }

    /// <summary>
    /// Wird vom Load-System benutzt.
    /// Hinweis: Wenn eine Zone im selben Play Mode bereits unlocked wurde, sind ihre Blocker zerstört.
    /// Ein Load zurück auf locked kann die zerstörten Blocker nicht wiederherstellen.
    /// Nach einem Szenen-Neuladen ist das kein Problem, weil die Blocker wieder aus der Scene kommen.
    /// </summary>
    public void ApplyLoadedState(bool unlocked)
    {
        if (unlocked)
        {
            Unlock();
        }
        else
        {
            IsUnlocked = false;
            if (zoneCollider != null)
                zoneCollider.enabled = true;
        }
    }

    // ── Scene View Gizmo ──────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = IsUnlocked ? unlockedColor : lockedColor;
        Gizmos.DrawCube(col.center, col.size);

        // Rand etwas dunkler für bessere Sichtbarkeit
        Gizmos.color = IsUnlocked
            ? new Color(0.2f, 1f, 0.2f, 0.6f)
            : new Color(1f, 0.2f, 0.2f, 0.6f);
        Gizmos.DrawWireCube(col.center, col.size);
    }
}

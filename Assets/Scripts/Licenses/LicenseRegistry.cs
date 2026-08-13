using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hält alle Lizenzen und welche davon gekauft sind. Sitzt auf dem SaveSystem-Objekt
/// (DontDestroyOnLoad), analog zu ToolRegistry und MissionManager.
///
/// Bewusst getrennt vom Werkzeug-Upgrade: Upgrades sind viele kleine Schritte, Lizenzen
/// sind wenige große. Beides in einem Topf würde die Meilensteine verwischen.
/// </summary>
public class LicenseRegistry : MonoBehaviour
{
    public static LicenseRegistry Instance { get; private set; }

    [Header("Alle Lizenzen")]
    [Tooltip("Jedes LicenseData-Asset hier eintragen — auch die noch gesperrten.")]
    [SerializeField] private LicenseData[] licenses;

    private readonly HashSet<string> owned = new();

    /// <summary>Feuert nach jedem Kauf — UI und Shop hängen daran.</summary>
    public event Action OnLicensesChanged;

    /// <summary>Statisches Event fürs Missions-System.</summary>
    public static event Action<string> OnLicenseBoughtStatic;

    public IReadOnlyList<LicenseData> All => licenses ?? Array.Empty<LicenseData>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Abfragen ──────────────────────────────────────────────────────────────

    public bool IsOwned(string licenseId) =>
        !string.IsNullOrWhiteSpace(licenseId) && owned.Contains(licenseId);

    public bool IsOwned(LicenseData license) =>
        license != null && IsOwned(license.licenseId);

    public LicenseData Find(string licenseId)
    {
        if (string.IsNullOrWhiteSpace(licenseId) || licenses == null) return null;

        foreach (var license in licenses)
            if (license != null && license.licenseId == licenseId) return license;

        return null;
    }

    /// <summary>
    /// Ist die Pflanze durch irgendeine gekaufte Lizenz freigeschaltet?
    ///
    /// Gibt <c>true</c> zurück wenn GAR KEINE Lizenz sie erwähnt — Pflanzen ohne Lizenz
    /// sind frei verfügbar. Sonst müsste man für jede Standard-Sorte eine Dummy-Lizenz
    /// anlegen, nur damit sie im Regal liegt.
    /// </summary>
    public bool IsPlantUnlocked(PlantType plant)
    {
        if (plant == null || licenses == null) return true;

        bool gated = false;

        foreach (var license in licenses)
        {
            if (license == null || !license.Unlocks(plant)) continue;

            gated = true;
            if (IsOwned(license)) return true;
        }

        return !gated;
    }

    /// <summary>Analog für Werkzeuge.</summary>
    public bool IsToolUnlocked(ToolType tool)
    {
        if (tool == ToolType.None || licenses == null) return true;

        bool gated = false;

        foreach (var license in licenses)
        {
            if (license == null || !license.Unlocks(tool)) continue;

            gated = true;
            if (IsOwned(license)) return true;
        }

        return !gated;
    }

    /// <summary>
    /// Darf diese Lizenz aktuell überhaupt angeboten werden? Prüft Vorgänger-Lizenzen
    /// und den Story-Fortschritt — eine Lizenz, deren Mission noch aussteht, soll gar
    /// nicht erst im Regal liegen.
    /// </summary>
    public bool IsAvailable(LicenseData license)
    {
        if (license == null) return false;
        if (IsOwned(license)) return false;

        if (license.requiredLicenseIds != null)
        {
            foreach (var id in license.requiredLicenseIds)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!IsOwned(id)) return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(license.requiredMissionId))
        {
            // Kein MissionManager (Szene direkt gestartet) → nicht aussperren.
            if (MissionManager.Instance != null &&
                !MissionManager.Instance.IsMissionCompleted(license.requiredMissionId))
                return false;
        }

        return true;
    }

    /// <summary>Alle gerade kaufbaren Lizenzen — Grundlage für das Shop-Panel.</summary>
    public List<LicenseData> GetAvailable()
    {
        var list = new List<LicenseData>();
        if (licenses == null) return list;

        foreach (var license in licenses)
            if (IsAvailable(license)) list.Add(license);

        return list;
    }

    // ── Kauf ──────────────────────────────────────────────────────────────────

    /// <summary>Kauft die Lizenz und zieht das Gold ab.</summary>
    public bool TryBuy(LicenseData license)
    {
        if (!IsAvailable(license)) return false;
        if (PlayerInventory.Instance == null) return false;
        if (!PlayerInventory.Instance.TrySpendMoney(license.price)) return false;

        Grant(license.licenseId);
        return true;
    }

    /// <summary>Schaltet eine Lizenz ohne Kosten frei (Missions-Belohnung, Debug).</summary>
    public void Grant(string licenseId)
    {
        if (string.IsNullOrWhiteSpace(licenseId)) return;
        if (!owned.Add(licenseId)) return;

        OnLicensesChanged?.Invoke();
        OnLicenseBoughtStatic?.Invoke(licenseId);
        FarmSaveManager.Instance?.RequestSave();
    }

    // ── Save / Load ───────────────────────────────────────────────────────────

    public List<string> GetSaveData() => new(owned);

    public void ApplyLoadedData(List<string> loaded)
    {
        owned.Clear();

        if (loaded != null)
            foreach (var id in loaded)
                if (!string.IsNullOrWhiteSpace(id)) owned.Add(id);

        OnLicensesChanged?.Invoke();
    }

    [ContextMenu("Debug: Alle Lizenzen freischalten")]
    private void DebugGrantAll()
    {
        if (licenses == null) return;
        foreach (var license in licenses)
            if (license != null) Grant(license.licenseId);
    }
}

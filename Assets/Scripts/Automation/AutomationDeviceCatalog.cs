using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reines Data-Lookup Typ → <see cref="AutomationDeviceData"/>. Hält bewusst KEINEN Zustand:
/// keine Level, keinen Bestand.
///
/// Upgrades laufen pro Gerät und der Kauf platziert direkt — damit gibt es nichts, was ein
/// globales Registry verwalten müsste. Das Level lebt auf der AutomationDevice-Instanz und
/// wird mit ihr gespeichert.
///
/// Gehört im Editor auf dasselbe GameObject wie die ToolRegistry.
/// </summary>
public class AutomationDeviceCatalog : MonoBehaviour
{
    public static AutomationDeviceCatalog Instance { get; private set; }

    [Header("Geräte-Definitionen")]
    [Tooltip("Alle AutomationDeviceData-Assets hierher ziehen — je eines pro Gerät.")]
    [SerializeField] private AutomationDeviceData[] devices;

    private readonly Dictionary<AutomationDeviceType, AutomationDeviceData> dataMap = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        if (devices == null) return;
        foreach (var data in devices)
        {
            if (data == null) continue;
            dataMap[data.deviceType] = data;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Alle konfigurierten Geräte-Definitionen, in Inspector-Reihenfolge.</summary>
    public IReadOnlyList<AutomationDeviceData> All => devices ?? System.Array.Empty<AutomationDeviceData>();

    public AutomationDeviceData GetData(AutomationDeviceType type) =>
        dataMap.TryGetValue(type, out var data) ? data : null;

    /// <summary>Null-sicherer Zugriff — der Katalog lebt nur in der Farm-Szene.</summary>
    public static AutomationDeviceData Get(AutomationDeviceType type) =>
        Instance != null ? Instance.GetData(type) : null;
}

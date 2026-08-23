using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eine eingelagerte Station: Reichweiten-Level plus alle Module mit ihren eigenen Leveln,
/// An/Aus-Zustaenden und Sortenwahlen.
///
/// Einpacken ist bewusst KEIN Verkauf. In einer vollausgebauten Station stecken schnell
/// mehrere tausend Gold; sie beim Umstellen aufzuloesen waere ein Verlust, den niemand
/// erwartet. Zum Umsetzen gibt es ohnehin "Verschieben" — Einpacken ist fuer den Fall,
/// dass man sie erst mal ganz weghaben will.
/// </summary>
public class PackedStation
{
    public int level;
    public List<AutomationModule> modules = new();

    /// <summary>Wie viele Module eingelagert sind — fuer die Anzeige im Baumodus.</summary>
    public int ModuleCount => modules?.Count ?? 0;
}

/// <summary>
/// Hält alle platzierten Automatik-Geräte und bindet sie ans Gitter.
///
/// Lebt NUR in der Farm-Szene (gehört im Editor auf dasselbe GameObject wie der
/// PlantManager). Alles, was speichert oder auf Belegung prüft, muss deshalb über die
/// statischen, null-sicheren Helfer gehen — auf dem Marktplatz gibt es diesen Manager nicht.
///
/// Warum die Kachel-Bindung hier als Dictionary liegt und nicht als Feld auf GridCell:
/// GridCell ist heute eine reine C#-Klasse ohne Unity-Typen; ein MonoBehaviour-Feld dort
/// würde die Abhängigkeitsrichtung umdrehen. Und TileSaveData ist gitterförmig über ~1000
/// Zellen, während Geräte spärlich sind — ein Feld pro Tile-Record bliese jeden Save auf.
/// </summary>
public class AutomationDeviceManager : MonoBehaviour
{
    public static AutomationDeviceManager Instance { get; private set; }

    /// <summary>
    /// Eigener Eltern-Knoten für alle Geräte.
    ///
    /// Geräte dürfen NIEMALS ans Tile-GameObject gehängt werden: GridManager.ReplaceTile
    /// zerstört das Tile-Objekt beim Umbauen — und auch beim Laden. Geräte auf umgebauten
    /// Kacheln wären sonst nach jedem Ladevorgang kommentarlos verschwunden.
    /// </summary>
    private Transform deviceRoot;

    private readonly Dictionary<Vector2Int, AutomationDevice> byTile = new();

    /// <summary>Eingepackte Stationen, jeweils samt Modulen. Aeltester Eintrag zuerst.</summary>
    private readonly List<PackedStation> packedStations = new();

    /// <summary>Feuert, wenn sich der Lagerbestand aendert — der Baumodus-Slot haengt daran.</summary>
    public static event Action OnPackedChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private Transform DeviceRoot
    {
        get
        {
            if (deviceRoot == null)
            {
                var go = new GameObject("Automation");
                go.transform.SetParent(transform, false);
                deviceRoot = go.transform;
            }

            return deviceRoot;
        }
    }

    // ── Abfragen ──────────────────────────────────────────────────────────────

    /// <summary>Alle platzierten Geräte. Reihenfolge ist nicht garantiert.</summary>
    public IEnumerable<AutomationDevice> AllDevices => byTile.Values;

    public int Count => byTile.Count;

    public AutomationDevice GetAt(int x, int z) =>
        byTile.TryGetValue(new Vector2Int(x, z), out var device) ? device : null;

    /// <summary>Steht auf dieser Kachel ein Gerät? Statisch und null-sicher — der Manager
    /// existiert außerhalb der Farm-Szene nicht.</summary>
    public static bool IsOccupied(int x, int z) =>
        Instance != null && Instance.byTile.ContainsKey(new Vector2Int(x, z));

    /// <summary>Gerät auf dieser Kachel, oder null.</summary>
    public static AutomationDevice At(int x, int z) =>
        Instance != null ? Instance.GetAt(x, z) : null;

    // ── Platzieren und Entfernen ──────────────────────────────────────────────

    /// <summary>
    /// Erzeugt ein Gerät auf der gegebenen Kachel. Prüft NICHT auf Gold oder Untergrund —
    /// das macht der Aufrufer (Platzierungs-Controller bzw. der Ladepfad).
    /// Gibt null zurück, wenn die Kachel schon belegt ist oder das Prefab fehlt.
    /// </summary>
    public AutomationDevice Spawn(AutomationStationData data, int x, int z)
    {
        if (data == null)
        {
            Debug.LogWarning("[Automation] Spawn ohne AutomationStationData.");
            return null;
        }

        if (data.worldPrefab == null)
        {
            Debug.LogWarning($"[Automation] Am Stations-Asset '{data.name}' ist kein " +
                             "worldPrefab zugewiesen — ohne Prefab kann keine Station entstehen.", data);
            return null;
        }

        var tile = new Vector2Int(x, z);
        if (byTile.ContainsKey(tile))
        {
            Debug.LogWarning($"[Automation] Kachel ({x},{z}) ist bereits belegt.");
            return null;
        }

        var grid = GridManager.Instance;
        var position = grid != null ? grid.GridToWorld(x, z) : new Vector3(x, 0f, z);

        var go = Instantiate(data.worldPrefab, position, data.worldPrefab.transform.rotation, DeviceRoot);
        go.name = $"Automations-Station ({x},{z})";

        var device = go.GetComponent<AutomationDevice>();
        if (device == null) device = go.AddComponent<AutomationDevice>();

        device.SetData(data);
        device.SetTilePosition(x, z);

        byTile[tile] = device;
        return device;
    }

    /// <summary>Meldet ein Gerät auf einer neuen Kachel an. Für das Verschieben.</summary>
    public bool Move(AutomationDevice device, int x, int z)
    {
        if (device == null) return false;

        var target = new Vector2Int(x, z);
        if (byTile.TryGetValue(target, out var other) && other != device) return false;

        Unregister(device);
        device.SetTilePosition(x, z);
        byTile[target] = device;
        return true;
    }

    /// <summary>Entfernt eine Station aus der Welt und verwirft sie samt Modulen.</summary>
    public void Remove(AutomationDevice device)
    {
        if (device == null) return;

        Unregister(device);
        Destroy(device.gameObject);
    }

    // ── Einlagern ─────────────────────────────────────────────────────────────

    public int PackedCount => packedStations.Count;

    /// <summary>Naechste eingelagerte Station, ohne sie zu entnehmen. Null wenn leer.</summary>
    public PackedStation PeekPacked() => packedStations.Count > 0 ? packedStations[0] : null;

    /// <summary>
    /// Nimmt die Station aus der Welt und legt sie mitsamt Modulen ins Lager.
    /// Kein Gold zurueck — der Wert steckt weiter in der eingelagerten Station.
    /// </summary>
    public void Pack(AutomationDevice device)
    {
        if (device == null) return;

        var entry = new PackedStation
        {
            level = device.Level,
            modules = device.DetachModules()
        };

        packedStations.Add(entry);

        Unregister(device);
        Destroy(device.gameObject);

        OnPackedChanged?.Invoke();
    }

    /// <summary>
    /// Setzt die aelteste eingelagerte Station auf die Kachel — mit Level, Modulen und deren
    /// Leveln. Gibt null zurueck, wenn nichts eingelagert ist oder das Setzen scheitert.
    /// </summary>
    public AutomationDevice PlacePacked(AutomationStationData data, int x, int z)
    {
        if (packedStations.Count == 0) return null;

        var entry = packedStations[0];
        var station = Spawn(data, x, z);
        if (station == null) return null;

        packedStations.RemoveAt(0);

        station.SetLevel(entry.level);
        station.RestoreModules(entry.modules);

        OnPackedChanged?.Invoke();
        return station;
    }

    /// <summary>Fuer den Ladepfad: Lager komplett ersetzen.</summary>
    public void SetPackedStations(List<PackedStation> entries)
    {
        packedStations.Clear();
        if (entries != null) packedStations.AddRange(entries);

        OnPackedChanged?.Invoke();
    }

    public IReadOnlyList<PackedStation> PackedStations => packedStations;

    /// <summary>Räumt alle Geräte ab — für den Ladepfad, bevor der Save angewendet wird.</summary>
    public void Clear()
    {
        foreach (var device in byTile.Values)
            if (device != null) Destroy(device.gameObject);

        byTile.Clear();
        packedStations.Clear();

        OnPackedChanged?.Invoke();
    }

    /// <summary>Nimmt das Gerät aus der Kachel-Zuordnung, ohne es zu zerstören.</summary>
    private void Unregister(AutomationDevice device)
    {
        var current = device.TilePosition;
        if (byTile.TryGetValue(current, out var existing) && existing == device)
        {
            byTile.Remove(current);
            return;
        }

        // Fallback: die gemerkte Position passt nicht mehr — linear suchen, damit kein
        // verwaister Eintrag die Kachel für immer blockiert.
        foreach (var pair in byTile)
        {
            if (pair.Value != device) continue;
            byTile.Remove(pair.Key);
            return;
        }
    }
}

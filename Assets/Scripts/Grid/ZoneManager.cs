using UnityEngine;

/// <summary>
/// Findet beim Start alle GridZones in der Scene, setzt IsLocked auf betroffenen Tiles
/// und stellt die Unlock-API bereit.
///
/// Muss nach GridManager initialisiert werden → Script Execution Order beachten
/// oder einfach in Start() statt Awake() arbeiten.
/// </summary>
public class ZoneManager : MonoBehaviour
{
    public static ZoneManager Instance { get; private set; }

    private GridZone[] zones;

    void Awake() => Instance = this;

    void Start()
    {
        zones = FindObjectsByType<GridZone>(FindObjectsSortMode.None);

        // An JEDE Zone hängen statt nur in TryUnlockZone zu entsperren.
        // Vorher lag das Entsperren der Tiles allein in TryUnlockZone(): wer eine Zone
        // über zone.Unlock() öffnete — der Missions-Reward und das Load-System tun genau
        // das — bekam zwar die Blocker weg, die Tiles blieben aber für immer gesperrt.
        // Über das Event ist der Pfad egal.
        foreach (var zone in zones)
        {
            if (zone == null) continue;
            var captured = zone;
            captured.OnUnlocked += () => UnlockZoneTiles(captured);
        }

        LockAllZoneTiles();
        Debug.Log($"[ZoneManager] {zones.Length} Zone(n) gefunden und Tiles gesperrt.");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Versucht eine Zone freizuschalten (zieht Kosten ab, entsperrt Tiles).
    /// Gibt false zurück wenn kein Gold oder bereits freigeschaltet.
    /// </summary>
    public bool TryUnlockZone(GridZone zone)
    {
        if (zone == null) return false;
        return zone.TryUnlock(); // Tiles laufen über OnUnlocked
    }

    /// <summary>Zone per SaveId/Name suchen — für Missions-Belohnungen und -Ziele.</summary>
    public GridZone FindZone(string zoneId)
    {
        if (string.IsNullOrWhiteSpace(zoneId) || zones == null) return null;

        foreach (var zone in zones)
        {
            if (zone == null) continue;
            if (zone.SaveId == zoneId || zone.gameObject.name == zoneId) return zone;
        }

        return null;
    }

    // ── Interne Logik ─────────────────────────────────────────────────────────

    private void LockAllZoneTiles()
    {
        int w = GridManager.Instance.Width;
        int h = GridManager.Instance.Height;

        for (int x = 0; x < w; x++)
        {
            for (int z = 0; z < h; z++)
            {
                var cell = GridManager.Instance.GetCell(x, z);
                if (cell == null) continue;

                Vector3 tileCenter = GridManager.Instance.GridToWorld(x, z);

                foreach (var zone in zones)
                {
                    if (!zone.IsUnlocked && zone.ContainsTile(tileCenter))
                    {
                        cell.IsLocked = true;
                        break; // Tile muss nur von einer Zone gesperrt sein
                    }
                }
            }
        }
    }

    private void UnlockZoneTiles(GridZone zone)
    {
        int w = GridManager.Instance.Width;
        int h = GridManager.Instance.Height;

        for (int x = 0; x < w; x++)
        {
            for (int z = 0; z < h; z++)
            {
                var cell = GridManager.Instance.GetCell(x, z);
                if (cell == null || !cell.IsLocked) continue;

                Vector3 tileCenter = GridManager.Instance.GridToWorld(x, z);
                if (!zone.ContainsTile(tileCenter)) continue;

                // Prüfen ob eine andere (noch gesperrte) Zone dieses Tile beansprucht
                bool stillLocked = false;
                foreach (var other in zones)
                {
                    if (other == zone) continue;
                    if (!other.IsUnlocked && other.ContainsTile(tileCenter))
                    {
                        stillLocked = true;
                        break;
                    }
                }

                if (!stillLocked)
                    cell.IsLocked = false;
            }
        }
    }
}

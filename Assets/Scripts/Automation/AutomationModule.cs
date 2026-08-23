using System;
using UnityEngine;

/// <summary>
/// Ein in eine Station eingesetztes Modul — eine Aktion der Kette, mit eigenem Level,
/// eigenem Takt und eigenem Zustand.
///
/// Die Module einer Station arbeiten unabhängig voneinander: jedes hat seinen eigenen
/// Cooldown, seinen eigenen Round-Robin-Cursor und höchstens einen laufenden Job. Sie
/// teilen sich nur die Kachelliste der Station — und damit den Mittelpunkt, um den es bei
/// dieser Bauform geht.
/// </summary>
[Serializable]
public class AutomationModule
{
    [Tooltip("Definition dieses Moduls.")]
    public AutomationDeviceData data;

    [Tooltip("Level dieses Moduls. Wird pro Modul aufgewertet, nicht pro Station.")]
    public int level;

    [Tooltip("Ausgeschaltete Module ticken nicht, bleiben aber eingebaut.")]
    public bool enabled = true;

    [Tooltip("Nur für das Saat-Modul: welche Sorte gesät wird.")]
    public PlantType seed;

    // ── Laufzeit ──────────────────────────────────────────────────────────────
    // Bewusst NonSerialized: das sind Zustände eines laufenden Spiels, keine Einstellungen.
    // cooldown wandert über den Save (siehe AutomationModuleSaveData), der Rest nicht.

    [NonSerialized] public float cooldown;
    [NonSerialized] public int scanIndex;
    [NonSerialized] public ToolJob pendingJob;

    /// <summary>True, solange der letzte Versuch keine Arbeit gefunden hat.</summary>
    [NonSerialized] public bool idle;

    /// <summary>Instanziiertes Anbauteil am Gehäuse. Null, wenn das Modul kein Prefab hat.</summary>
    [NonSerialized] public GameObject attachment;

    public AutomationDeviceType Type => data != null ? data.deviceType : AutomationDeviceType.None;
    public ToolType ExecutesTool => data != null ? data.executesTool : ToolType.None;

    public float Interval => data != null ? data.GetInterval(level) : 1f;

    /// <summary>Fortschritt bis zum nächsten Takt bzw. des laufenden Jobs, 0–1.</summary>
    public float Progress
    {
        get
        {
            if (pendingJob != null) return pendingJob.Progress;

            float interval = Interval;
            if (interval <= 0f) return 1f;
            return Mathf.Clamp01(1f - cooldown / interval);
        }
    }

    /// <summary>Braucht dieses Modul eine Sortenwahl?</summary>
    public bool NeedsSeed => ExecutesTool == ToolType.Seed;
}

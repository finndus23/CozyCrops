using System.Collections.Generic;
using UnityEngine;

public enum ToolJobState
{
    Queued,
    Running,
    Finished,
    Cancelled
}

/// <summary>
/// Eine eingeplante Tool-Aktion auf einer (oder bei AoE mehreren) Tiles.
///
/// Wichtig sind die Snapshots: Tool, Saatgut und Yield-Bonus werden beim Einreihen
/// festgehalten, nicht erst beim Ausführen abgefragt. Vorher konnte das nicht
/// auseinanderfallen, weil immer nur genau eine Aktion lief. In einer Warteschlange
/// kann der Spieler zwischendurch das Tool oder die Saat wechseln — ohne Snapshot
/// würde dann auf Feld 1 plötzlich das gepflanzt, was beim Klick auf Feld 5 aktiv war.
/// </summary>
public class ToolJob
{
    private static int nextId = 1;

    public int Id { get; }
    public ToolType Tool { get; }
    public PlantType Seed { get; }
    public int YieldBonus { get; }

    /// <summary>Die Tile auf die geklickt wurde — Mittelpunkt der AoE.</summary>
    public Vector2Int Origin { get; }

    /// <summary>Alle betroffenen Tiles, bereits auf die anwendbaren gefiltert.</summary>
    public List<Vector2Int> Tiles { get; }

    public float Duration { get; }
    public float Elapsed { get; set; }
    public ToolJobState State { get; set; } = ToolJobState.Queued;

    public float Progress => Duration <= 0f ? 1f : Mathf.Clamp01(Elapsed / Duration);

    public ToolJob(ToolType tool, PlantType seed, int yieldBonus,
                   Vector2Int origin, List<Vector2Int> tiles, float duration)
    {
        Id = nextId++;
        Tool = tool;
        Seed = seed;
        YieldBonus = yieldBonus;
        Origin = origin;
        Tiles = tiles;
        Duration = duration;
    }

    public bool CoversTile(Vector2Int tile) => Tiles.Contains(tile);
}

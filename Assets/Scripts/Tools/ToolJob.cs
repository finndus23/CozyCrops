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
/// Wer diesen Job eingereiht hat.
///
/// Die Trennung ist nötig, weil sich Spieler und Automatik sonst gegenseitig die Plätze
/// wegnehmen: Warteschlangen-Kapazität, Parallel-Slots und der Werkzeug-Slot in
/// PromoteQueued sind allesamt knappe Ressourcen. Vier tickende Geräte würden die Queue
/// füllen, und jeder Spielerklick liefe danach stumm ins Leere.
/// </summary>
public enum ToolJobSource
{
    Player,
    Automation
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

    /// <summary>Spieler oder Automatik — trennt die beiden Spuren in der Warteschlange.</summary>
    public ToolJobSource Source { get; }

    /// <summary>
    /// Instanz-ID des Geräts, das diesen Job eingereiht hat. 0 = Spieler.
    ///
    /// Dient als Spur-Schlüssel in PromoteQueued: der Spieler behält pro Werkzeug exakt
    /// einen Slot, jedes Gerät bekommt seinen eigenen — und nie mehr als einen gleichzeitig.
    /// "Eine Kachel pro Takt" ist damit strukturell garantiert statt nur per Timer.
    /// </summary>
    public int OwnerId { get; }

    public ToolJob(ToolType tool, PlantType seed, int yieldBonus,
                   Vector2Int origin, List<Vector2Int> tiles, float duration,
                   ToolJobSource source = ToolJobSource.Player, int ownerId = 0)
    {
        Id = nextId++;
        Tool = tool;
        Seed = seed;
        YieldBonus = yieldBonus;
        Origin = origin;
        Tiles = tiles;
        Duration = duration;
        Source = source;
        OwnerId = ownerId;
    }

    public bool CoversTile(Vector2Int tile) => Tiles.Contains(tile);
}

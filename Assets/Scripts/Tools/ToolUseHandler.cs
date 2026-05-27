using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Verwaltet Tool-Aktionen mit Duration-Timer und AoE-Berechnung.
/// GridInput ruft TryStartUse() auf — der Rest läuft hier.
/// </summary>
public class ToolUseHandler : MonoBehaviour
{
    public static ToolUseHandler Instance { get; private set; }

    // ── Zustand ───────────────────────────────────────────────────────────────

    public bool IsCasting { get; private set; }

    /// <summary>Fortschritt des aktuellen Casts, 0–1.</summary>
    public float CastProgress { get; private set; }

    /// <summary>Tiles die vom aktuellen Cast betroffen sind (für Preview).</summary>
    public IReadOnlyList<Vector2Int> CastAoTiles => castAoTiles;

    // ── Events ────────────────────────────────────────────────────────────────

    public event Action<IReadOnlyList<Vector2Int>> OnCastStarted;
    public event Action<float>                     OnCastProgressChanged;
    public event Action                            OnCastCompleted;
    public event Action                            OnCastCancelled;

    // ── Private ───────────────────────────────────────────────────────────────

    private float           castTimer;
    private float           castDuration;
    private int             castX, castZ;
    private ToolType        castTool;
    private List<Vector2Int> castAoTiles = new();

    // Kamera schaut in +X+Z → bei geraden AoE-Größen sitzt der Cursor in der +X+Z-Ecke
    private static readonly Vector2Int EvenAoOffset = new(-1, -1);

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Startet einen Cast auf (x, z) mit dem gegebenen Tool.
    /// Gibt false zurück wenn gerade noch ein Cast läuft.
    /// </summary>
    public bool TryStartUse(int x, int z, ToolType tool)
    {
        if (IsCasting || tool == ToolType.None) return false;

        // Vorab prüfen ob die Aktion überhaupt Sinn ergibt — kein Cast auf ungültige Tiles
        if (!CanApplyTool(x, z, tool)) return false;

        int aoSize = ToolRegistry.Instance != null ? ToolRegistry.Instance.GetAoSize(tool) : 1;

        castX       = x;
        castZ       = z;
        castTool    = tool;
        castTimer   = 0f;
        CastProgress = 0f;
        IsCasting   = true;

        castAoTiles = CalculateAoTiles(x, z, aoSize);

        // Duration skaliert mit Tile-Anzahl: 2×2 = 4× Duration, 3×3 = 9× Duration
        float durationPerTile = ToolRegistry.Instance != null ? ToolRegistry.Instance.GetDuration(tool) : 0f;
        castDuration = durationPerTile * Mathf.Max(1, castAoTiles.Count);
        OnCastStarted?.Invoke(castAoTiles);

        // Duration 0 → sofort anwenden (kein visuelles Warten)
        if (castDuration <= 0f)
            CompleteCast();

        return true;
    }

    /// <summary>Bricht den laufenden Cast ab (z.B. bei Maus-Release).</summary>
    public void CancelCast()
    {
        if (!IsCasting) return;
        IsCasting    = false;
        CastProgress = 0f;
        OnCastCancelled?.Invoke();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!IsCasting) return;

        castTimer   += Time.deltaTime;
        CastProgress = Mathf.Clamp01(castTimer / castDuration);
        OnCastProgressChanged?.Invoke(CastProgress);

        if (castTimer >= castDuration)
            CompleteCast();
    }

    // ── Cast abschließen ──────────────────────────────────────────────────────

    private void CompleteCast()
    {
        IsCasting    = false;
        CastProgress = 0f;

        foreach (var tile in castAoTiles)
            ApplyTool(tile.x, tile.y, castTool);

        OnCastCompleted?.Invoke();
    }

    private void ApplyTool(int x, int z, ToolType tool)
    {
        switch (tool)
        {
            case ToolType.Hoe:
                PlantManager.Instance.TryTill(x, z);
                break;

            case ToolType.Seed:
                var seed = Hotbar.Instance.SelectedSeed;
                if (seed != null)
                    PlantManager.Instance.TryPlant(x, z, seed);
                break;

            case ToolType.WateringCan:
                PlantManager.Instance.TryWater(x, z);
                break;

            case ToolType.Scythe:
                int yieldBonus = ToolRegistry.Instance?.GetYieldBonus(tool) ?? 0;
                PlantManager.Instance.TryHarvest(x, z, yieldBonus);
                break;
        }
    }

    // ── Validierung ──────────────────────────────────────────────────────────

    /// <summary>Prüft ob das Tool auf dieser Tile überhaupt anwendbar ist.</summary>
    private bool CanApplyTool(int x, int z, ToolType tool)
    {
        var cell = GridManager.Instance?.GetCell(x, z);
        if (cell == null || cell.IsLocked) return false;

        return tool switch
        {
            ToolType.Hoe         => cell.Type == TileType.FarmPlot && !cell.IsTilled && !cell.HasPlant,
            ToolType.WateringCan => cell.HasPlant,
            ToolType.Scythe      => cell.HasPlant && cell.Plant != null && cell.Plant.IsFullyGrown,
            ToolType.Seed        => cell.IsTilled && !cell.HasPlant
                                    && Hotbar.Instance.SelectedSeed != null
                                    && PlayerInventory.Instance.GetSeedCount(Hotbar.Instance.SelectedSeed) > 0,
            _                    => false
        };
    }

    // ── AoE-Berechnung ────────────────────────────────────────────────────────

    public static List<Vector2Int> CalculateAoTiles(int cx, int cz, int aoSize)
    {
        var tiles = new List<Vector2Int>();

        if (aoSize <= 1)
        {
            tiles.Add(new Vector2Int(cx, cz));
            return tiles;
        }

        if (aoSize % 2 == 1)
        {
            // Ungerade: zentriert auf den Cursor
            int half = aoSize / 2;
            for (int dx = -half; dx <= half; dx++)
                for (int dz = -half; dz <= half; dz++)
                    tiles.Add(new Vector2Int(cx + dx, cz + dz));
        }
        else
        {
            // Gerade: Cursor sitzt in der +X+Z-Ecke, Tiles gehen Richtung -X-Z (zur Kamera)
            for (int dx = -(aoSize - 1); dx <= 0; dx++)
                for (int dz = -(aoSize - 1); dz <= 0; dz++)
                    tiles.Add(new Vector2Int(cx + dx, cz + dz));
        }

        return tiles;
    }
}

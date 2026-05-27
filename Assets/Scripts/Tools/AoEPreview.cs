using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zeigt beim Hovern und während eines Casts die betroffenen Tiles an.
///
/// Setup:
/// - overlayPrefab → flaches Quad-Prefab (Plane/Quad, kein Collider, transparentes Material)
///   Empfehlung: einfaches Quad mit einem Unlit/Color-Material, halbtransparent
/// - heightOffset  → wie weit über dem Grid die Overlays erscheinen (z.B. 0.02)
/// </summary>
public class AoEPreview : MonoBehaviour
{
    public static AoEPreview Instance { get; private set; }

    [Header("Referenzen")]
    [SerializeField] private GameObject overlayPrefab;

    [Header("Einstellungen")]
    [SerializeField] private float heightOffset = 0.02f;
    [Tooltip("Sollte der cellSize des GridManagers entsprechen.")]
    [SerializeField] private float tileSize = 1f;
    [SerializeField] private Color hoverColor = new(1f, 1f, 1f, 0.3f);
    [SerializeField] private Color castColor  = new(1f, 0.85f, 0.2f, 0.45f);

    private readonly List<GameObject> pool   = new();
    private readonly List<GameObject> active = new();

    private int   lastPreviewX = -999, lastPreviewZ = -999;
    private int   lastPreviewAo = -1;
    private bool  isCasting;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (ToolUseHandler.Instance == null) return;
        ToolUseHandler.Instance.OnCastStarted   += OnCastStarted;
        ToolUseHandler.Instance.OnCastCompleted += OnCastEnded;
        ToolUseHandler.Instance.OnCastCancelled += OnCastEnded;
    }

    void OnDestroy()
    {
        if (ToolUseHandler.Instance == null) return;
        ToolUseHandler.Instance.OnCastStarted   -= OnCastStarted;
        ToolUseHandler.Instance.OnCastCompleted -= OnCastEnded;
        ToolUseHandler.Instance.OnCastCancelled -= OnCastEnded;
    }

    void Update()
    {
        if (BuildModeManager.Instance.IsActive)
        {
            HideAll();
            return;
        }

        // Während eines Casts bleibt das Preview stehen — Update macht nichts
        if (isCasting) return;

        if (!GridInput.IsHoveringGrid)
        {
            HideAll();
            return;
        }

        ToolType tool  = Hotbar.Instance.ActiveTool;
        int x          = GridInput.HoveredX;
        int z          = GridInput.HoveredZ;
        // Kein Tool aktiv → nur die einzelne Tile zeigen
        int aoSize     = tool != ToolType.None && ToolRegistry.Instance != null
                            ? ToolRegistry.Instance.GetAoSize(tool)
                            : 1;

        // Nur neu berechnen wenn sich Position oder AoE-Größe geändert hat
        if (x == lastPreviewX && z == lastPreviewZ && aoSize == lastPreviewAo) return;

        lastPreviewX  = x;
        lastPreviewZ  = z;
        lastPreviewAo = aoSize;

        ShowTiles(ToolUseHandler.CalculateAoTiles(x, z, aoSize), hoverColor);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    private void OnCastStarted(IReadOnlyList<Vector2Int> tiles)
    {
        isCasting = true;
        ShowTiles(tiles, castColor);
    }

    private void OnCastEnded()
    {
        isCasting      = false;
        lastPreviewX   = -999;
        lastPreviewZ   = -999;
        lastPreviewAo  = -1;
        HideAll();
    }

    // ── Overlays verwalten ────────────────────────────────────────────────────

    private void ShowTiles(IReadOnlyList<Vector2Int> tiles, Color color)
    {
        HideAll();

        if (overlayPrefab == null || GridManager.Instance == null) return;

        foreach (var tile in tiles)
        {
            var cell = GridManager.Instance.GetCell(tile.x, tile.y);
            if (cell == null) continue;

            var go = GetFromPool();
            var worldPos = GridManager.Instance.GridToWorld(tile.x, tile.y);
            // Y absolut setzen — GridToWorld enthält die GridManager-Y die nicht der Boden-Y entspricht
            go.transform.position = new Vector3(worldPos.x, heightOffset, worldPos.z);

            // Farbe auf dem Renderer setzen
            var rend = go.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                // Material-Instanz damit sich Overlays nicht gegenseitig beeinflussen
                rend.material.color = color;
            }

            go.SetActive(true);
            active.Add(go);
        }
    }

    private void HideAll()
    {
        foreach (var go in active)
        {
            go.SetActive(false);
            pool.Add(go);
        }
        active.Clear();
    }

    private GameObject GetFromPool()
    {
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            if (pool[i] != null)
            {
                var go = pool[i];
                pool.RemoveAt(i);
                return go;
            }
        }

        var spawned = Instantiate(overlayPrefab, transform);
        spawned.transform.localScale = new Vector3(tileSize, tileSize, tileSize);
        return spawned;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zeigt über jeder Pflanze an, was gerade mit ihr los ist:
///
///  • <b>wächst</b>       — ein Bogen füllt sich, sonst nichts. Bewusst leise: das ist die
///                          Aufforderung "hier musst du gar nichts tun".
///  • <b>braucht Wasser</b> — Wassertropfen, der Bogen steht still.
///  • <b>erntereif</b>    — voller Bogen und ein Funkeln.
///
/// <b>Warum das nicht wie der Tile-Indikator aussieht:</b> AoEPreview zeichnet flache,
/// eckige Rahmen auf dem Boden — das ist die Sprache für "Werkzeug wirkt hier". Ein
/// zweites Element derselben Machart würde damit verschmelzen, und der Spieler müsste
/// erst hinsehen, um zu unterscheiden, ob dort eine Aktion läuft oder eine Pflanze
/// Aufmerksamkeit will. Deshalb hier: rund statt eckig, schwebend statt am Boden,
/// weich statt hart, und immer zur Kamera gedreht (Billboard im Vertex-Shader).
///
/// Setup: liegt in SampleScene auf dem GameObject "PlantManager",
/// <c>statusPrefab</c> → Prefab "Plant Status" (Quad mit CozyCrops/PlantStatus).
/// </summary>
public class PlantStatusOverlay : MonoBehaviour
{
    public static PlantStatusOverlay Instance { get; private set; }

    [Header("Referenzen")]
    [Tooltip("Quad mit dem CozyCrops/PlantStatus-Material.")]
    [SerializeField] private GameObject statusPrefab;

    [Header("Anzeige")]
    [Tooltip("Abstand über der Oberkante der Pflanze.")]
    [SerializeField] private float heightOffset = 0.35f;

    [Tooltip("Aus = wachsende Pflanzen bekommen gar nichts, nur Wasser- und Erntebedarf " +
             "werden angezeigt. Für ein ruhigeres Bild auf sehr großen Feldern.")]
    [SerializeField] private bool showGrowthArc = true;

    [Tooltip("Obergrenze gleichzeitig sichtbarer Anzeigen. Bei einem vollen Grid stünden " +
             "sonst hunderte Quads gleichzeitig im Bild — teuer und unlesbar.")]
    [SerializeField] private int maxVisible = 120;

    [Tooltip("Wie oft der Zustand aller Pflanzen neu bewertet wird. Der Bogen bewegt sich " +
             "langsam, das Wippen läuft ohnehin im Shader — jeden Frame prüfen wäre " +
             "verschenkte Rechenzeit.")]
    [SerializeField] private float refreshInterval = 0.1f;

    [Header("Farben")]
    [SerializeField] private Color growingColor = new(0.55f, 0.85f, 0.45f, 0.75f);
    [SerializeField] private Color thirstyColor = new(0.35f, 0.72f, 1f, 1f);
    [SerializeField] private Color readyColor   = new(1f, 0.83f, 0.30f, 1f);

    // Muss zu den Werten im Shader passen (0 = keins, 1 = Tropfen, 2 = Funkeln).
    private const float SymbolNone    = 0f;
    private const float SymbolDrop    = 1f;
    private const float SymbolSparkle = 2f;

    private readonly List<GameObject> pool = new();
    private readonly Dictionary<GridCell, GameObject> overlays = new();
    private readonly List<GridCell> staleKeys = new();
    private readonly List<GridCell> desired = new();

    private MaterialPropertyBlock propertyBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ProgressId  = Shader.PropertyToID("_Progress");
    private static readonly int SymbolId    = Shader.PropertyToID("_Symbol");

    private Camera cachedCamera;
    private float nextRefresh;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        propertyBlock = new MaterialPropertyBlock();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        if (Time.time < nextRefresh) return;
        nextRefresh = Time.time + Mathf.Max(0.02f, refreshInterval);

        Refresh();
    }

    private void Refresh()
    {
        var manager = PlantManager.Instance;

        if (statusPrefab == null || manager == null)
        {
            HideAll();
            return;
        }

        var cam = ResolveCamera();
        desired.Clear();

        var plants = manager.ActivePlants;

        for (int i = 0; i < plants.Count; i++)
        {
            if (desired.Count >= maxVisible) break;

            var cell = plants[i];
            if (cell == null || !cell.HasPlant || cell.Plant == null) continue;

            var plant = cell.Plant;

            bool thirsty = plant.NeedsWatering;
            bool ready   = plant.IsFullyGrown;

            // Wächst still vor sich hin und der Bogen ist abgeschaltet → nichts anzeigen.
            if (!thirsty && !ready && !showGrowthArc) continue;

            if (!TryResolvePosition(manager, cell, out var pos)) continue;
            if (!IsVisible(cam, pos)) continue;

            desired.Add(cell);
            Show(cell, pos, plant, thirsty, ready);
        }

        // Alles was diesmal nicht dabei war einsammeln — geerntet, aus dem Bild
        // gescrollt oder über der Obergrenze.
        staleKeys.Clear();
        foreach (var kvp in overlays)
            if (!desired.Contains(kvp.Key)) staleKeys.Add(kvp.Key);

        foreach (var key in staleKeys)
        {
            Release(overlays[key]);
            overlays.Remove(key);
        }
    }

    private void Show(GridCell cell, Vector3 pos, PlantInstance plant, bool thirsty, bool ready)
    {
        if (!overlays.TryGetValue(cell, out var go) || go == null)
        {
            go = GetFromPool();
            if (go == null) return;

            go.SetActive(true);
            overlays[cell] = go;
        }

        go.transform.position = pos;

        Color color;
        float symbol;

        if (ready)
        {
            color  = readyColor;
            symbol = SymbolSparkle;
        }
        else if (thirsty)
        {
            color  = thirstyColor;
            symbol = SymbolDrop;
        }
        else
        {
            color  = growingColor;
            symbol = SymbolNone;
        }

        var rend = go.GetComponentInChildren<Renderer>();
        if (rend == null) return;

        rend.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetFloat(ProgressId, ready ? 1f : plant.TotalProgress);
        propertyBlock.SetFloat(SymbolId, symbol);
        rend.SetPropertyBlock(propertyBlock);
    }

    /// <summary>
    /// Höhe aus den echten Renderer-Bounds der Pflanze — dieselbe Logik wie beim
    /// Tile-Indikator. Ein fester Welt-Y-Wert würde bei drei verschieden hohen
    /// Wachstumsstufen entweder in der Pflanze stecken oder darüber schweben.
    /// </summary>
    private bool TryResolvePosition(PlantManager manager, GridCell cell, out Vector3 pos)
    {
        pos = default;

        if (GridManager.Instance == null) return false;

        var basePos = GridManager.Instance.GridToWorld(cell.X, cell.Z);
        float y = basePos.y;

        var visual = manager.GetPlantVisual(cell);
        if (visual != null)
        {
            var rend = visual.GetComponentInChildren<Renderer>();
            if (rend != null) y = rend.bounds.max.y;
        }

        pos = new Vector3(basePos.x, y + heightOffset, basePos.z);
        return true;
    }

    /// <summary>
    /// Grobe Sichtprüfung über den Viewport. Ohne die hingen bei 1170 Tiles im
    /// Vollausbau hunderte Quads in der Szene, von denen die meisten außerhalb des
    /// Bildes liegen.
    /// </summary>
    private static bool IsVisible(Camera cam, Vector3 worldPos)
    {
        if (cam == null) return true;

        var vp = cam.WorldToViewportPoint(worldPos);
        if (vp.z < 0f) return false;

        const float margin = 0.08f;
        return vp.x > -margin && vp.x < 1f + margin
            && vp.y > -margin && vp.y < 1f + margin;
    }

    private Camera ResolveCamera()
    {
        if (cachedCamera != null && cachedCamera.isActiveAndEnabled) return cachedCamera;

        cachedCamera = Camera.main;
        return cachedCamera;
    }

    // ── Pool ──────────────────────────────────────────────────────────────────

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

        return Instantiate(statusPrefab, transform);
    }

    private void Release(GameObject go)
    {
        if (go == null) return;

        go.SetActive(false);
        pool.Add(go);
    }

    private void HideAll()
    {
        if (overlays.Count == 0) return;

        foreach (var kvp in overlays)
            Release(kvp.Value);

        overlays.Clear();
    }
}

using UnityEngine;

public enum FarmTileState { Dry, Tilled, Watered }

/// <summary>
/// Sitzt auf dem leeren FarmPlot-Anker-Prefab.
/// Tauscht das sichtbare Kind-Prefab je nach State aus.
/// Jeder State kann ein komplett eigenes 3D-Modell + Material haben.
/// </summary>
public class FarmTileVisual : MonoBehaviour
{
    [SerializeField] private GameObject dryPrefab;
    [SerializeField] private GameObject tilledPrefab;
    [SerializeField] private GameObject wateredPrefab;

    [Header("Dünger-Markierung")]
    [Tooltip("Lila Hue direkt auf die Tile-Textur (Multiply-Tint via MaterialPropertyBlock, " +
             "kein Material-Asset wird verändert), solange die Kachel gedüngt ist — kein " +
             "extra Prefab/Setup im Editor nötig. Probiert automatisch beide gängigen Farb-" +
             "Properties durch (_BaseColor für URP, _Color für Built-in/Legacy), je nachdem " +
             "was der Shader von Dry/Tilled/Watered tatsächlich hat.")]
    [SerializeField] private Color fertilizedTint = new(0.72f, 0.45f, 1f, 1f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId     = Shader.PropertyToID("_Color");

    public FarmTileState CurrentState { get; private set; } = FarmTileState.Dry;
    public bool IsFertilized { get; private set; }

    private GameObject currentChild;
    private MaterialPropertyBlock propertyBlock;

    void Awake()
    {
        ApplyState(FarmTileState.Dry);
    }

    public void SetState(FarmTileState state)
    {
        CurrentState = state;
        ApplyState(state);
    }

    /// <summary>
    /// Färbt die Tile-Textur lila ein, solange gedüngt ist. Von GridCell.IsFertilized aus
    /// aufgerufen überall dort, wo sich der Wert ändert — TryFertilize, Harvest() (setzt
    /// zurück) und beim Laden eines Spielstands.
    /// </summary>
    public void SetFertilized(bool value)
    {
        IsFertilized = value;
        ApplyTint();
    }

    /// <summary>
    /// Wird auch bei jedem ApplyState() (Dry/Tilled/Watered-Wechsel tauscht das komplette
    /// Kind-Prefab samt eigenem Renderer aus) neu aufgerufen — sonst ginge die Färbung beim
    /// nächsten State-Wechsel verloren, weil der alte eingefärbte Renderer zerstört wird.
    /// </summary>
    private void ApplyTint()
    {
        if (currentChild == null) return;

        propertyBlock ??= new MaterialPropertyBlock();
        Color tint = IsFertilized ? fertilizedTint : Color.white;

        foreach (var rend in currentChild.GetComponentsInChildren<Renderer>())
        {
            var mat = rend.sharedMaterial;
            if (mat == null) continue;

            rend.GetPropertyBlock(propertyBlock);
            if (mat.HasProperty(BaseColorId)) propertyBlock.SetColor(BaseColorId, tint);
            if (mat.HasProperty(ColorId))     propertyBlock.SetColor(ColorId, tint);
            rend.SetPropertyBlock(propertyBlock);
        }
    }

    private void ApplyState(FarmTileState state)
    {
        if (currentChild != null)
            Destroy(currentChild);

        var prefab = state switch
        {
            FarmTileState.Tilled  => tilledPrefab,
            FarmTileState.Watered => wateredPrefab,
            _                     => dryPrefab
        };

        if (prefab == null)
        {
            Debug.LogWarning($"[FarmTileVisual] Kein Prefab für State {state} auf {gameObject.name}");
            return;
        }

        currentChild = Instantiate(prefab, transform.position, transform.rotation, transform);
        ApplyTint();
    }
}

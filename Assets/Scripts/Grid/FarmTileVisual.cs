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
    [Tooltip("Dasselbe Prefab wie bei Pflanzen/Komposter (Plant Status). Optional — ohne " +
             "Zuweisung bleibt gedüngter Boden unmarkiert.\n\n" +
             "Unabhängig vom Dry/Tilled/Watered-Wechsel: sitzt direkt unter diesem " +
             "Transform, nicht unter dem ausgetauschten Kind-Prefab, und überlebt deshalb " +
             "jeden SetState()-Aufruf unverändert.")]
    [SerializeField] private GameObject fertilizedMarkerPrefab;

    [SerializeField] private float fertilizedMarkerHeightOffset = 0.12f;
    [SerializeField] private Color fertilizedMarkerColor = new(0.55f, 0.35f, 0.85f, 1f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ProgressId  = Shader.PropertyToID("_Progress");
    private static readonly int SymbolId    = Shader.PropertyToID("_Symbol");
    private const float SymbolNone = 0f;

    public FarmTileState CurrentState { get; private set; } = FarmTileState.Dry;
    public bool IsFertilized { get; private set; }

    private GameObject currentChild;
    private GameObject fertilizedMarker;
    private MaterialPropertyBlock fertilizedPropertyBlock;

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
    /// Zeigt oder versteckt die Dünger-Markierung. Von GridCell.IsFertilized aus aufgerufen
    /// überall dort, wo sich der Wert ändert — TryFertilize, Harvest() (setzt zurück) und
    /// beim Laden eines Spielstands.
    /// </summary>
    public void SetFertilized(bool value)
    {
        IsFertilized = value;

        if (fertilizedMarkerPrefab == null) return;

        if (value)
        {
            EnsureFertilizedMarker();
            fertilizedMarker.SetActive(true);
        }
        else if (fertilizedMarker != null)
        {
            fertilizedMarker.SetActive(false);
        }
    }

    private void EnsureFertilizedMarker()
    {
        if (fertilizedMarker != null) return;

        fertilizedMarker = Instantiate(fertilizedMarkerPrefab, transform);
        fertilizedMarker.transform.localPosition = new Vector3(0f, fertilizedMarkerHeightOffset, 0f);

        var rend = fertilizedMarker.GetComponentInChildren<Renderer>();
        if (rend == null) return;

        fertilizedPropertyBlock ??= new MaterialPropertyBlock();
        rend.GetPropertyBlock(fertilizedPropertyBlock);
        fertilizedPropertyBlock.SetColor(BaseColorId, fertilizedMarkerColor);
        // Voll gefüllter Ring, dauerhaft — hier gibt es keinen Fortschritt zu zeigen,
        // nur einen Zustand ("gedüngt" ja/nein).
        fertilizedPropertyBlock.SetFloat(ProgressId, 1f);
        fertilizedPropertyBlock.SetFloat(SymbolId, SymbolNone);
        rend.SetPropertyBlock(fertilizedPropertyBlock);
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
    }
}

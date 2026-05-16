using UnityEngine;

public enum FarmTileState { Dry, Tilled, Watered }

public class FarmTileVisual : MonoBehaviour
{
    [SerializeField] private Material dryMaterial;
    [SerializeField] private Material tilledMaterial;
    [SerializeField] private Material wateredMaterial;

    public FarmTileState CurrentState { get; private set; } = FarmTileState.Dry;

    private Renderer rend;

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        if (rend == null)
            Debug.LogWarning($"[FarmTileVisual] Kein Renderer gefunden auf {gameObject.name} oder Kindobjekten.");
    }

    public void SetState(FarmTileState state)
    {
        if (rend == null) { Debug.LogWarning($"[FarmTileVisual] rend ist null auf {gameObject.name}"); return; }

        CurrentState = state;
        ApplyMaterial(state);
    }

    /// <summary>Stellt das State-Material wieder her — z.B. nach einem Hover-Override.</summary>
    public void RestoreMaterial() => ApplyMaterial(CurrentState);

    private void ApplyMaterial(FarmTileState state)
    {
        rend.material = state switch
        {
            FarmTileState.Tilled  => tilledMaterial,
            FarmTileState.Watered => wateredMaterial,
            _                     => dryMaterial
        };
    }
}

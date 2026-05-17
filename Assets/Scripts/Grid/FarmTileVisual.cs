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

    public FarmTileState CurrentState { get; private set; } = FarmTileState.Dry;

    private GameObject currentChild;

    void Awake()
    {
        ApplyState(FarmTileState.Dry);
    }

    public void SetState(FarmTileState state)
    {
        CurrentState = state;
        ApplyState(state);
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

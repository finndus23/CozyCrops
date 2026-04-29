using System.Collections.Generic;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    [SerializeField] private GameObject selectionHighlightPrefab;

    private readonly HashSet<Vector2Int> selectedCells = new();
    private readonly HashSet<Vector2Int> dragProcessed = new();
    private readonly List<GameObject> highlightObjects = new();

    public IEnumerable<Vector2Int> SelectedCells => selectedCells;
    public bool HasSelection => selectedCells.Count > 0;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        foreach (var obj in highlightObjects)
            if (obj != null) Destroy(obj);
    }

    public void StartSelection(int x, int z)
    {
        dragProcessed.Clear();
        var cell = new Vector2Int(x, z);
        if (!GridManager.Instance.IsInBounds(x, z)) return;
        dragProcessed.Add(cell);

        if (selectedCells.Contains(cell))
            selectedCells.Remove(cell);
        else
            selectedCells.Add(cell);

        RefreshHighlights();
    }

    public void AddToSelection(int x, int z)
    {
        var cell = new Vector2Int(x, z);
        if (!GridManager.Instance.IsInBounds(x, z)) return;
        if (dragProcessed.Contains(cell)) return;
        dragProcessed.Add(cell);

        if (selectedCells.Contains(cell))
            selectedCells.Remove(cell);
        else
            selectedCells.Add(cell);

        RefreshHighlights();
    }

    public void ClearSelection()
    {
        selectedCells.Clear();
        RefreshHighlights();
    }

    private void RefreshHighlights()
    {
        foreach (var obj in highlightObjects)
            Destroy(obj);
        highlightObjects.Clear();

        if (selectionHighlightPrefab == null) return;

        foreach (var cell in selectedCells)
        {
            var obj = Instantiate(selectionHighlightPrefab,
                GridManager.Instance.GridToWorld(cell.x, cell.y), Quaternion.identity);
            highlightObjects.Add(obj);
        }
    }
}

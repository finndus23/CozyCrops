using System;
using System.Collections.Generic;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    /// <summary>
    /// Eine Zelle wurde markiert (true) oder abgewählt (false).
    ///
    /// Feuert beim Ziehen für jede neu berührte Zelle einmal — das ist gewollt, die
    /// Rückmeldung soll der Bewegung folgen. Dass bei schnellem Ziehen mehrere Zellen im
    /// selben Frame übereinanderklingen, fängt der Duplikatschutz im SfxManager ab.
    ///
    /// <see cref="ClearSelection"/> feuert bewusst nicht: das Aufräumen nach dem Bauen ist
    /// keine Spieleraktion und braucht keine eigene Rückmeldung.
    /// </summary>
    public static event Action<Vector2Int, bool> OnCellSelectionChangedStatic;

    [SerializeField] private GameObject selectionHighlightPrefab;

    private readonly HashSet<Vector2Int> selectedCells = new();
    private readonly HashSet<Vector2Int> dragProcessed = new();
    private readonly Dictionary<Vector2Int, GameObject> highlightObjects = new();

    public IEnumerable<Vector2Int> SelectedCells => selectedCells;
    public bool HasSelection => selectedCells.Count > 0;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        foreach (var obj in highlightObjects.Values)
            if (obj != null) Destroy(obj);
    }

    public void StartSelection(int x, int z)
    {
        dragProcessed.Clear();
        var cell = new Vector2Int(x, z);
        if (!GridManager.Instance.IsInBounds(x, z)) return;
        dragProcessed.Add(cell);

        bool selected = Toggle(cell);

        RefreshHighlights();
        OnCellSelectionChangedStatic?.Invoke(cell, selected);
    }

    public void AddToSelection(int x, int z)
    {
        var cell = new Vector2Int(x, z);
        if (!GridManager.Instance.IsInBounds(x, z)) return;
        if (dragProcessed.Contains(cell)) return;
        dragProcessed.Add(cell);

        bool selected = Toggle(cell);

        RefreshHighlights();
        OnCellSelectionChangedStatic?.Invoke(cell, selected);
    }

    /// <summary>Kippt den Markierungszustand einer Zelle. Gibt zurück, ob sie danach markiert ist.</summary>
    private bool Toggle(Vector2Int cell)
    {
        if (selectedCells.Remove(cell)) return false;

        selectedCells.Add(cell);
        return true;
    }

    public void ClearSelection()
    {
        selectedCells.Clear();
        RefreshHighlights();
    }

    private void RefreshHighlights()
    {
        if (selectionHighlightPrefab == null) return;

        // Abgewählte Zellen: nur deren Highlight entfernen (nicht alles wegwerfen).
        List<Vector2Int> toRemove = null;
        foreach (var kvp in highlightObjects)
        {
            if (selectedCells.Contains(kvp.Key)) continue;

            if (kvp.Value != null) Destroy(kvp.Value);
            (toRemove ??= new List<Vector2Int>()).Add(kvp.Key);
        }
        if (toRemove != null)
            foreach (var key in toRemove)
                highlightObjects.Remove(key);

        // Neu dazugekommene Zellen: Highlight spawnen + Hochflieg-Animation.
        // Bereits bestehende Highlights bleiben unangetastet (kein Re-Trigger beim Drag).
        foreach (var cell in selectedCells)
        {
            if (highlightObjects.ContainsKey(cell)) continue;

            var obj = Instantiate(selectionHighlightPrefab,
                GridManager.Instance.GridToWorld(cell.x, cell.y), Quaternion.identity);

            if (!obj.TryGetComponent<FloatInFx>(out _))
                obj.AddComponent<FloatInFx>();

            highlightObjects[cell] = obj;

            AnimateGroundTile(cell);
        }
    }

    /// <summary>
    /// Lässt das echte Boden-Tile unter der markierten Zelle synchron mit dem
    /// Highlight-Overlay hochhüpfen. Nur der Hop, kein Scale-Pop (siehe FloatInFx).
    /// </summary>
    private void AnimateGroundTile(Vector2Int cell)
    {
        var groundTile = GridManager.Instance.GetTileObject(cell.x, cell.y);
        if (groundTile == null) return;

        if (groundTile.TryGetComponent<FloatInFx>(out var fx))
        {
            fx.Play(); // Tile bereits mit Fx bestückt (erneut markiert) → nochmal hüpfen
        }
        else
        {
            fx = groundTile.AddComponent<FloatInFx>();
            fx.includeScalePop = false; // AddComponent → Start() spielt den Hop automatisch
        }
    }
}

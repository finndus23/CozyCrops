using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Zeigt beim Tool-Cast einen Kasten mit dem Tool-Icon am Cursor an.
/// Der Kasten füllt sich von unten nach oben bis der Cast abgeschlossen ist.
///
/// UI-Struktur (unter HUD-Canvas):
///   ToolCastbar (dieses Script)
///   └── Container          RectTransform, folgt dem Cursor
///       ├── Background     Image — dunkler Hintergrundkasten
///       ├── Fill           Image — Fill Method: Vertical, Fill Origin: Bottom
///       └── Icon           Image — Tool-Sprite
///
/// Farbe und Aussehen direkt auf den Image-Komponenten in Unity setzen.
/// </summary>
public class ToolCastbar : MonoBehaviour
{
    [Header("Referenzen")]
    [SerializeField] private RectTransform container;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image fillImage;

    [Header("Position")]
    [Tooltip("Versatz in Pixeln relativ zum Cursor.")]
    [SerializeField] private Vector2 offset = new(32f, 32f);

    void Awake()
    {
        if (fillImage != null)
            fillImage.fillAmount = 0f;

        SetVisible(false);
    }

    void Start()
    {
        if (ToolUseHandler.Instance == null) return;
        ToolUseHandler.Instance.OnCastStarted         += OnCastStarted;
        ToolUseHandler.Instance.OnCastProgressChanged += OnProgress;
        ToolUseHandler.Instance.OnCastCompleted       += Hide;
        ToolUseHandler.Instance.OnCastCancelled       += Hide;
    }

    void OnDestroy()
    {
        if (ToolUseHandler.Instance == null) return;
        ToolUseHandler.Instance.OnCastStarted         -= OnCastStarted;
        ToolUseHandler.Instance.OnCastProgressChanged -= OnProgress;
        ToolUseHandler.Instance.OnCastCompleted       -= Hide;
        ToolUseHandler.Instance.OnCastCancelled       -= Hide;
    }

    void Update()
    {
        if (container == null || !container.gameObject.activeSelf) return;
        if (Mouse.current == null) return;
        container.position = (Vector3)Mouse.current.position.ReadValue() + (Vector3)offset;
    }

    // ── Events ────────────────────────────────────────────────────────────────

    private void OnCastStarted(System.Collections.Generic.IReadOnlyList<UnityEngine.Vector2Int> _)
    {
        UpdateIcon();
        if (fillImage != null) fillImage.fillAmount = 0f;
        SetVisible(true);
    }

    private void OnProgress(float progress)
    {
        if (fillImage != null)
            fillImage.fillAmount = progress;
    }

    private void Hide()
    {
        SetVisible(false);
        if (fillImage != null) fillImage.fillAmount = 0f;
    }

    // ── Hilfsmethoden ────────────────────────────────────────────────────────

    private void UpdateIcon()
    {
        if (iconImage == null) return;

        ToolType tool = Hotbar.Instance.ActiveTool;

        if (tool == ToolType.Seed)
        {
            iconImage.sprite = Hotbar.Instance.SelectedSeed?.icon;
            return;
        }

        iconImage.sprite = ToolRegistry.Instance?.GetData(tool)?.icon;
    }

    private void SetVisible(bool visible)
    {
        if (container != null)
            container.gameObject.SetActive(visible);
    }
}

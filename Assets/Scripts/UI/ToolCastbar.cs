using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Ersetzt den Systemcursor durch das aktuell gewaehlte Werkzeug und zeigt
/// waehrend einer Werkzeugaktion einen radialen Fortschrittsring darum an.
/// </summary>
public class ToolCastbar : MonoBehaviour
{
    [Header("Referenzen")]
    [SerializeField] private RectTransform container;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image fillImage;
    [SerializeField] private Sprite carrotSeedSprite;
    [SerializeField] private Sprite cauliflowerSeedSprite;
    [SerializeField] private Sprite sunflowerSeedSprite;

    [Header("Position")]
    [Tooltip("Versatz in Pixeln relativ zum Cursor.")]
    [SerializeField] private Vector2 offset = new(22f, -22f);

    [Header("Darstellung")]
    [Tooltip("Versteckt den System-Cursor, solange ein Werkzeug aktiv ist.\n" +
             "Aus lassen: das Tool-Icon sitzt per Offset NEBEN der Mausposition, ohne " +
             "sichtbaren Cursor fehlt dann der exakte Zielpunkt.")]
    [SerializeField] private bool hideSystemCursorWithTool;

    [Header("Layering")]
    [Tooltip("Sorting Order des Cursor-Canvas. Muss über allen anderen UI-Canvas liegen, " +
             "aber unter dem Ladebildschirm (short.MaxValue).")]
    [SerializeField] private int cursorSortingOrder = 30000;

    private bool isCasting;

    private void Awake()
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
            fillImage.gameObject.SetActive(false);
        }

        EnsureTopmostCursor();
    }

    /// <summary>
    /// Der Cursor lag bisher unter Dialogen: alle Canvas im Projekt stehen auf sortingOrder 0,
    /// und DialogueUI/MissionsUI hängen an einem DontDestroyOnLoad-Canvas. Bei gleichem
    /// sortingOrder entscheidet die Instanziierungs-Reihenfolge — nach einem Szenenwechsel
    /// landet der HUD-Canvas dann hinter dem persistenten UI.
    ///
    /// Der Container bekommt deshalb einen eigenen (verschachtelten) Canvas mit
    /// overrideSorting. Alle Canvas laufen in Screen Space Overlay, dort sortiert Unity
    /// global nach sortingOrder — der Cursor liegt damit garantiert oben.
    /// </summary>
    private void EnsureTopmostCursor()
    {
        if (container == null) return;

        var cursorCanvas = container.GetComponent<Canvas>();
        if (cursorCanvas == null)
            cursorCanvas = container.gameObject.AddComponent<Canvas>();

        cursorCanvas.overrideSorting = true;
        cursorCanvas.sortingOrder = cursorSortingOrder;

        // Der Cursor klebt an der Maus — ohne das fängt er jeden Klick ab, der eigentlich
        // an Buttons oder die Welt gehen soll.
        var group = container.GetComponent<CanvasGroup>();
        if (group == null)
            group = container.gameObject.AddComponent<CanvasGroup>();

        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private void Start()
    {
        if (ToolUseHandler.Instance != null)
        {
            ToolUseHandler.Instance.OnCastStarted += OnCastStarted;
            ToolUseHandler.Instance.OnCastProgressChanged += OnProgress;
            ToolUseHandler.Instance.OnCastCompleted += HideProgress;
            ToolUseHandler.Instance.OnCastCancelled += HideProgress;
        }

        if (Hotbar.Instance != null)
        {
            Hotbar.Instance.OnToolChanged += OnToolChanged;
            Hotbar.Instance.OnSeedChanged += OnSeedChanged;
        }

        RefreshCursor();
    }

    private void OnDestroy()
    {
        if (ToolUseHandler.Instance != null)
        {
            ToolUseHandler.Instance.OnCastStarted -= OnCastStarted;
            ToolUseHandler.Instance.OnCastProgressChanged -= OnProgress;
            ToolUseHandler.Instance.OnCastCompleted -= HideProgress;
            ToolUseHandler.Instance.OnCastCancelled -= HideProgress;
        }

        if (Hotbar.Instance != null)
        {
            Hotbar.Instance.OnToolChanged -= OnToolChanged;
            Hotbar.Instance.OnSeedChanged -= OnSeedChanged;
        }

        Cursor.visible = true;
    }

    private void Update()
    {
        if (container == null || !container.gameObject.activeSelf || Mouse.current == null)
            return;

        container.position = (Vector3)Mouse.current.position.ReadValue() + (Vector3)offset;
    }

    private void OnToolChanged(ToolType _)
    {
        HideProgress();
        RefreshCursor();
    }

    private void OnSeedChanged(PlantType _)
    {
        UpdateIcon();
    }

    private void OnCastStarted(IReadOnlyList<Vector2Int> _)
    {
        isCasting = true;
        UpdateIcon();

        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
            fillImage.gameObject.SetActive(IsToolSelected());
        }
    }

    private void OnProgress(float progress)
    {
        if (fillImage != null)
            fillImage.fillAmount = Mathf.Clamp01(progress);
    }

    private void HideProgress()
    {
        isCasting = false;

        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
            fillImage.gameObject.SetActive(false);
        }
    }

    private void RefreshCursor()
    {
        bool showToolCursor = IsToolSelected();

        if (container != null)
            container.gameObject.SetActive(showToolCursor);

        Cursor.visible = !(showToolCursor && hideSystemCursorWithTool);

        if (showToolCursor)
            UpdateIcon();

        if (fillImage != null)
            fillImage.gameObject.SetActive(showToolCursor && isCasting);
    }

    private bool IsToolSelected()
    {
        return Hotbar.Instance != null && Hotbar.Instance.ActiveTool != ToolType.None;
    }

    private void UpdateIcon()
    {
        if (iconImage == null || Hotbar.Instance == null)
            return;

        ToolType tool = Hotbar.Instance.ActiveTool;
        Sprite specificSeedSprite = tool == ToolType.Seed
            ? GetSeedUiSprite(Hotbar.Instance.SelectedSeed)
            : null;

        Sprite sprite = tool == ToolType.Seed
            ? specificSeedSprite
                ?? Hotbar.Instance.SelectedSeed?.icon
                ?? ToolRegistry.Instance?.GetData(ToolType.Seed)?.icon
            : ToolRegistry.Instance?.GetData(tool)?.icon;

        iconImage.sprite = sprite;
        iconImage.color = sprite != null ? Color.white : Color.clear;
        iconImage.preserveAspect = true;
        iconImage.rectTransform.localScale = specificSeedSprite != null
            ? new Vector3(1f, 0.9f, 1f)
            : Vector3.one;
    }

    private Sprite GetSeedUiSprite(PlantType seed)
    {
        if (seed == null)
            return null;

        return seed.plantName switch
        {
            "Carrot" => carrotSeedSprite,
            "Cauliflower" => cauliflowerSeedSprite,
            "Sunflower" => sunflowerSeedSprite,
            _ => null
        };
    }
}

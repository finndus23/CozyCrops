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

    private bool isCasting;

    private void Awake()
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
            fillImage.gameObject.SetActive(false);
        }
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

        Cursor.visible = !showToolCursor;

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

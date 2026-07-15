using UnityEngine;
using UnityEngine.UI;

public class TileContextMenu : MonoBehaviour
{
    public static TileContextMenu Instance { get; private set; }

    public static event System.Action OnFarmTilePlacedStatic;

    [SerializeField] private Button farmLandButton;
    [SerializeField] private Button pathButton;
    [SerializeField] private Button grassButton;

    private RectTransform rectTransform;
    private Canvas canvas;

    void Awake()
    {
        Instance = this;
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

        farmLandButton.onClick.AddListener(() => Apply(TileType.FarmPlot));
        pathButton.onClick.AddListener(() => Apply(TileType.Path));
        grassButton.onClick.AddListener(() => Apply(TileType.Grass));

        gameObject.SetActive(false);
    }

    public void Show(Vector2 screenPos)
    {
        if (!SelectionManager.Instance.HasSelection) return;

        gameObject.SetActive(true);

        var canvasRect = canvas.GetComponent<RectTransform>();
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, cam, out Vector2 localPos);
        rectTransform.localPosition = localPos;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Apply(TileType type)
    {
        GridManager.Instance.ApplyToSelection(type);
        Hide();

        NotifyTilePlaced(type);
    }

    public static void NotifyTilePlaced(TileType type)
    {
        if (type == TileType.FarmPlot)
            OnFarmTilePlacedStatic?.Invoke();
    }
}

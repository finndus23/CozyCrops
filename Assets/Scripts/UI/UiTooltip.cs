using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Einfacher Hover-Tooltip für UI-Elemente. Baut sich beim ersten Gebrauch selbst auf —
/// kein Prefab nötig, gleiches Prinzip wie das Confirmation-Popup in
/// <see cref="GameSceneMenuController"/>. Ein Singleton reicht: es ist ohnehin nie mehr
/// als ein Tooltip gleichzeitig sichtbar.
///
/// Folgt dem Mauszeiger statt sich einmalig exakt an die Ecke des gehoverten Elements zu
/// hängen — braucht dadurch nur den Canvas als Referenz, keine Welt-Koordinaten-Umrechnung
/// vom Anchor. Weniger Annahmen, weniger Fehlerquellen.
/// </summary>
public class UiTooltip : MonoBehaviour
{
    private static UiTooltip instance;

    private RectTransform selfRect;
    private RectTransform panelRect;
    private TextMeshProUGUI label;
    private Canvas hostCanvas;
    private bool visible;

    public static void Show(RectTransform anchor, string text)
    {
        if (anchor == null || string.IsNullOrEmpty(text))
            return;

        Canvas canvas = anchor.GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        // Immer am äußersten Canvas aufhängen, nie an einem verschachtelten Sub-Canvas —
        // dessen RectTransform kann kleiner sein oder einen anderen Pivot haben, und dann
        // rechnet sich der Tooltip aus dem Bild raus statt daneben zu erscheinen.
        canvas = canvas.rootCanvas;

        UiTooltip tooltip = GetOrCreate(canvas);
        tooltip.ShowInternal(text);
    }

    public static void Hide()
    {
        if (instance == null)
            return;

        instance.visible = false;
        instance.gameObject.SetActive(false);
    }

    private static UiTooltip GetOrCreate(Canvas canvas)
    {
        if (instance != null)
        {
            if (instance.hostCanvas != canvas)
            {
                instance.transform.SetParent(canvas.transform, false);
                instance.hostCanvas = canvas;
            }
            return instance;
        }

        GameObject go = new GameObject("UiTooltip (auto)", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        instance = go.AddComponent<UiTooltip>();
        instance.Build(canvas);
        return instance;
    }

    private void Build(Canvas canvas)
    {
        hostCanvas = canvas;
        selfRect = (RectTransform)transform;

        // Anker MUSS am Pivot des Canvas selbst sitzen (Mitte, Standard für ein Root-Canvas) —
        // ScreenPointToLocalPointInRectangle() liefert nämlich einen Punkt relativ zu genau
        // diesem Pivot. Ein Anker in der Ecke (z.B. (0,1)) hätte einen eigenen Nullpunkt,
        // und die beiden Koordinatensysteme addieren sich dann zu einem konstanten Versatz
        // von rund der halben Bildschirmgröße — genau das Symptom "Tooltip weit daneben".
        selfRect.anchorMin = selfRect.anchorMax = new Vector2(0.5f, 0.5f);

        // Pivot oben-links am eigenen Panel: die Box wächst vom Cursor aus nach rechts unten,
        // zusammen mit dem (+18,-18)-Offset in UpdatePosition().
        selfRect.pivot = new Vector2(0f, 1f);

        Canvas ownCanvas = gameObject.AddComponent<Canvas>();
        ownCanvas.overrideSorting = true;
        ownCanvas.sortingOrder = 500; // über allem anderen Shop-UI, auch über dem Confirm-Popup
        gameObject.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.06f, 0.04f, 0.94f);
        bg.raycastTarget = false;

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter panelFitter = panel.AddComponent<ContentSizeFitter>();
        panelFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(panel.transform, false);
        label = textGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 16f;
        label.color = new Color(0.96f, 0.9f, 0.8f, 1f);
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        textGo.AddComponent<LayoutElement>();

        gameObject.SetActive(false);
    }

    private void ShowInternal(string text)
    {
        label.text = text;
        gameObject.SetActive(true);
        visible = true;
        transform.SetAsLastSibling();
        UpdatePosition();
    }

    private void Update()
    {
        // Läuft nur während sichtbar — folgt dem Mauszeiger, damit der Tooltip auch dann
        // sichtbar bleibt, wenn die einmalige Positionierung beim Show() aus irgendeinem
        // Grund daneben lag (z.B. Canvas-Layout, das erst einen Frame später steht).
        if (visible)
            UpdatePosition();
    }

    private void UpdatePosition()
    {
        Vector2 screenPos = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;

        RectTransform canvasRect = (RectTransform)hostCanvas.transform;
        Camera cam = hostCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hostCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, cam, out Vector2 localPoint))
            return;

        Vector2 pos = localPoint + new Vector2(18f, -18f);
        ClampToCanvas(canvasRect, ref pos);

        selfRect.anchoredPosition = pos;
    }

    /// <summary>Hält den Tooltip innerhalb der Canvas-Grenzen, egal welchen Pivot sie hat.</summary>
    private void ClampToCanvas(RectTransform canvasRect, ref Vector2 pos)
    {
        Canvas.ForceUpdateCanvases();

        Rect bounds = canvasRect.rect;
        Vector2 panelSize = panelRect.rect.size;

        float minX = bounds.xMin;
        float maxX = Mathf.Max(minX, bounds.xMax - panelSize.x);
        float minY = Mathf.Min(bounds.yMax, bounds.yMin + panelSize.y);
        float maxY = bounds.yMax;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
    }
}

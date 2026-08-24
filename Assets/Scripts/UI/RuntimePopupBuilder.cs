using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gemeinsame Bausteine für Popups, die zur Laufzeit aus Code entstehen statt aus einem
/// Prefab — Komposter-Fenster und Automatik-Geräte-Popup.
///
/// Vorher lagen diese drei Helfer als statische Methoden im ComposterInteraction. Sie sind
/// aber nicht komposter-spezifisch, und ein zweiter Nutzer hätte sie sonst kopiert.
/// </summary>
public static class RuntimePopupBuilder
{
    /// <summary>
    /// Findet den EINEN richtigen Root-Canvas statt blind irgendeinen zu nehmen — die Szene
    /// hat mehrere (PersistentUI, UI, HUD). HotbarUI hängt nachweislich im richtigen Baum
    /// (HotbarPanel → HUD → UI), darüber lässt sich der Wurzel-Canvas zuverlässig finden,
    /// ohne dass jemand ihn von Hand im Inspector verdrahten muss.
    /// </summary>
    /// <param name="preferred">Optional im Inspector gesetzter Canvas. Gewinnt, wenn vorhanden.</param>
    public static Canvas ResolveHudCanvas(Canvas preferred = null)
    {
        if (preferred != null) return preferred;

        if (HotbarUI.Instance != null)
        {
            var root = HotbarUI.Instance.transform.root.GetComponent<Canvas>();
            if (root != null) return root;
        }

        return Object.FindFirstObjectByType<Canvas>();
    }

    public static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject go = new(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    public static Button CreateButton(Transform parent, string objectName, Vector2 position, Vector2 size,
        string label, Sprite sprite, UnityEngine.Events.UnityAction action)
    {
        GameObject go = CreateUiObject(objectName, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = sprite != null ? Color.white : new Color(0.96f, 0.78f, 0.4f, 1f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        colors.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        button.colors = colors;
        button.onClick.AddListener(action);

        GameObject textObj = CreateUiObject("Text", go.transform);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 18f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.22f, 0.12f, 0.06f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        return button;
    }

    /// <summary>Beschriftung ohne Knopf — Kopfzeilen und Statustexte im Popup.</summary>
    public static TextMeshProUGUI CreateLabel(Transform parent, string objectName, Vector2 position,
        Vector2 size, string content, float fontSize = 18f, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        GameObject go = CreateUiObject(objectName, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.color = new Color(0.22f, 0.12f, 0.06f, 1f);
        text.alignment = alignment;
        text.raycastTarget = false;

        return text;
    }

    /// <summary>Hintergrundfläche eines Popups, zentriert im übergebenen Canvas.</summary>
    public static GameObject CreatePanel(Transform parent, string objectName, Vector2 size, Sprite sprite)
    {
        GameObject go = CreateUiObject(objectName, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = sprite != null ? Color.white : new Color(0.99f, 0.93f, 0.78f, 0.98f);

        return go;
    }

    /// <summary>
    /// Horizontaler Fortschrittsbalken: Hintergrund + Fuellflaeche (Image.Type.Filled,
    /// FillMethod.Horizontal). Gibt die Fuellflaeche zurueck, deren fillAmount (0..1)
    /// den Aufrufer steuern laesst — kein eigener State hier, der Aufrufer kennt den
    /// aktuellen Fortschritt ohnehin schon.
    /// </summary>
    public static Image CreateProgressBar(Transform parent, string objectName, Vector2 position,
        Vector2 size, Sprite backgroundSprite, Sprite fillSprite, Color backgroundColor, Color fillColor)
    {
        GameObject go = CreateUiObject(objectName, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image background = go.AddComponent<Image>();
        background.sprite = backgroundSprite;
        background.type = backgroundSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        background.color = backgroundColor;

        GameObject fillObj = CreateUiObject("Fill", go.transform);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

        Image fill = fillObj.AddComponent<Image>();
        fill.sprite = fillSprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.color = fillColor;
        fill.fillAmount = 0f;

        return fill;
    }
}

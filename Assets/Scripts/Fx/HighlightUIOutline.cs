using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quest-Hervorhebung für UI-Elemente (Hotbar-Slot, Button, Panel).
///
/// Die Weltobjekt-Kontur kommt aus einer Kamera-Maske — ein Canvas-Overlay taucht darin
/// überhaupt nicht auf, UI braucht also zwingend einen eigenen Weg. Statt eines zweiten
/// Shaders wird hier einfach ein farbiges Rechteck HINTER das Element gelegt, das etwas
/// größer ist als das Element selbst. Was drüberhinausschaut, liest sich als Rahmen.
///
/// Bewusst ohne Sprite-Abhängigkeit: ein 9-Slice-Rahmen sähe feiner aus, würde aber ein
/// Art-Asset erzwingen, das es noch nicht gibt. Wenn später eins existiert, einfach in
/// <see cref="borderSprite"/> hängen — der Rest bleibt gleich.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class HighlightUIOutline : MonoBehaviour, IHighlightVisual
{
    [SerializeField] private Color outlineColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Tooltip("Wie weit der Rahmen über das Element hinausragt (Pixel).")]
    [SerializeField] private float padding = 6f;

    [Tooltip("Optional: 9-Slice-Sprite für den Rahmen. Leer = einfarbige Fläche.")]
    [SerializeField] private Sprite borderSprite;

    [Header("Puls")]
    [SerializeField] private float pulseSpeed = 1.6f;
    [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.45f;

    private RectTransform frame;
    private Image frameImage;
    private bool built;

    public bool IsHighlighted { get; private set; }

    private void Awake() => Build();

    public void SetHighlighted(bool on)
    {
        if (!built) Build();
        IsHighlighted = on;

        if (frame != null) frame.gameObject.SetActive(on);
    }

    private void Update()
    {
        if (!IsHighlighted || frameImage == null) return;

        // Puls auf dem Alpha. Bei UI ist das unkritischer als bei der Weltkontur: hier
        // gibt es keine Nachbarpixel, die durch eine schwankende Breite flimmern könnten.
        float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        var c = outlineColor;
        c.a = outlineColor.a * Mathf.Lerp(1f - pulseAmount, 1f, t);
        frameImage.color = c;
    }

    private void Build()
    {
        if (built) return;
        built = true;

        var go = new GameObject("HighlightFrame", typeof(RectTransform), typeof(Image));
        frame = go.GetComponent<RectTransform>();
        frame.SetParent(transform, false);

        // Hinter den Inhalt des Slots, sonst verdeckt der Rahmen Icon und Zähler.
        frame.SetAsFirstSibling();

        // An allen vier Seiten am Elternteil andocken und nach außen aufblasen —
        // dadurch passt sich der Rahmen jeder Slot-Größe an, ohne feste Werte.
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = new Vector2(-padding, -padding);
        frame.offsetMax = new Vector2(padding, padding);

        frameImage = go.GetComponent<Image>();
        frameImage.color = outlineColor;
        frameImage.raycastTarget = false; // darf Klicks auf den Slot nicht abfangen

        if (borderSprite != null)
        {
            frameImage.sprite = borderSprite;
            frameImage.type = Image.Type.Sliced;
        }

        go.SetActive(false);
    }
}

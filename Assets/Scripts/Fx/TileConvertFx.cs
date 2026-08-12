using DG.Tweening;
using UnityEngine;

/// <summary>
/// Feel-Good-Effekt für Boden-Tiles, die ihren Typ wechseln (Gras ↔ Farmland ↔ Weg)
/// und für Treffer-Feedback bei Tool-Aktionen.
///
/// Der Trick: die Tiles werden NICHT nach oben bewegt, sondern von unten verankert
/// in die Höhe gestreckt.
///
/// Grund: die Boden-Tiles sind nur 0.1 dick, springen aber deutlich höher als das.
/// Bei einer echten Verschiebung verlassen sie den Boden und man sieht die Lücke
/// darunter — es liegt ja nichts unter dem Grid. Beim Strecken bleibt die Unterkante
/// exakt liegen und nur die Oberfläche hebt sich. Die Amplitude und das Easing sind
/// dieselben, es "hüpft" also weiterhin, aber es kann konstruktionsbedingt keine
/// Lücke geben — egal wie hoch man geht oder wie dünn die Tiles später werden.
///
/// Optischer Nebeneffekt: es liest sich wie aufgeworfene Erde statt wie ein
/// schwebender Klotz, was für ein Farming-Spiel eher besser passt.
/// </summary>
public class TileConvertFx : MonoBehaviour
{
    [Header("Umwandeln")]
    [Tooltip("Starthöhe beim Umwandeln, als Faktor der normalen Dicke. Klein = flach gedrückt.")]
    [SerializeField] private float convertStartFactor = 0.15f;
    [Tooltip("Wie weit die Höhe überschwingt, bevor sie sich einpendelt.")]
    [SerializeField] private float convertOvershoot = 1.7f;
    [SerializeField] private float convertDuration = 0.3f;

    [Header("Nachbar-Hüpfer / Treffer")]
    [Tooltip("Wie hoch sich das Tile streckt, als Faktor der normalen Dicke.")]
    [SerializeField] private float nudgeFactor = 2.2f;
    [SerializeField] private float nudgeDuration = 0.12f;
    [Tooltip("Leichtes Zusammendrücken beim Zurückfedern — gibt dem Hüpfer Gewicht.")]
    [SerializeField] private float nudgeSquash = 0.85f;

    private Vector3 basePos;
    private Vector3 baseScale;

    /// <summary>
    /// Abstand vom Pivot zur Unterkante. Damit lässt sich beim Skalieren die Unterkante
    /// festhalten, auch wenn der Pivot nicht mittig im Mesh sitzt.
    /// </summary>
    private float pivotToBottom;

    private Sequence activeSequence;
    private bool played;

    void Awake()
    {
        basePos = transform.position;
        baseScale = transform.localScale;

        var rend = GetComponentInChildren<Renderer>();
        pivotToBottom = rend != null
            ? basePos.y - rend.bounds.min.y
            : baseScale.y * 0.5f;

        // Entartete Werte abfangen (Renderer noch ohne Bounds o.ä.)
        if (pivotToBottom <= 0.0001f)
            pivotToBottom = Mathf.Max(baseScale.y * 0.5f, 0.0001f);
    }

    void Start()
    {
        // Fallback, falls die Komponente im Inspector auf ein Tile gelegt wurde
        // statt per Code mit explizitem Play-Aufruf.
        if (!played) PlayConvert(0f);
    }

    void OnDestroy()
    {
        activeSequence?.Kill();
    }

    /// <summary>Tile wächst flachgedrückt aus dem Boden auf seine Höhe.</summary>
    public void PlayConvert(float delay)
    {
        played = true;
        activeSequence?.Kill();

        SetHeightFactor(convertStartFactor);

        activeSequence = DOTween.Sequence();
        if (delay > 0f) activeSequence.PrependInterval(delay);

        activeSequence.Append(
            DOVirtual.Float(convertStartFactor, convertOvershoot, convertDuration * 0.45f, SetHeightFactor)
                     .SetEase(Ease.OutQuad));
        activeSequence.Append(
            DOVirtual.Float(convertOvershoot, 1f, convertDuration * 0.55f, SetHeightFactor)
                     .SetEase(Ease.OutBack));

        activeSequence.OnKill(() => activeSequence = null);
    }

    /// <summary>
    /// Kurzer Hüpfer. Für Nachbar-Tiles einer Umwandlung und als Treffer-Feedback,
    /// wenn ein Tool auf dieser Tile fertig wird.
    /// </summary>
    public void PlayNudge(float delay)
    {
        played = true;
        activeSequence?.Kill();

        SetHeightFactor(1f);

        activeSequence = DOTween.Sequence();
        if (delay > 0f) activeSequence.PrependInterval(delay);

        activeSequence.Append(
            DOVirtual.Float(1f, nudgeFactor, nudgeDuration, SetHeightFactor)
                     .SetEase(Ease.OutQuad));
        // Unter die Ruhehöhe zurückfedern und erst dann einpendeln — ohne das
        // wirkt der Hüpfer oben abgeschnitten statt federnd.
        activeSequence.Append(
            DOVirtual.Float(nudgeFactor, nudgeSquash, nudgeDuration * 1.1f, SetHeightFactor)
                     .SetEase(Ease.InQuad));
        activeSequence.Append(
            DOVirtual.Float(nudgeSquash, 1f, nudgeDuration * 1.4f, SetHeightFactor)
                     .SetEase(Ease.OutBack));

        activeSequence.OnKill(() => activeSequence = null);
    }

    /// <summary>
    /// Setzt die Höhe auf ein Vielfaches der Ausgangsdicke und verschiebt den Pivot so,
    /// dass die Unterkante an Ort und Stelle bleibt. X und Z werden nie angefasst —
    /// horizontales Skalieren würde Lücken zwischen benachbarten Tiles aufreißen.
    /// </summary>
    private void SetHeightFactor(float factor)
    {
        transform.localScale = new Vector3(baseScale.x, baseScale.y * factor, baseScale.z);

        var pos = transform.position;
        pos.y = basePos.y + pivotToBottom * (factor - 1f);
        transform.position = pos;
    }

    /// <summary>
    /// Holt (oder erzeugt) die Komponente auf einem Tile. basePos wird in Awake
    /// eingefroren — deshalb darf das Tile beim ersten Aufruf nicht mitten in einer
    /// anderen Animation stehen.
    /// </summary>
    public static TileConvertFx Ensure(GameObject tile)
    {
        if (tile == null) return null;
        return tile.TryGetComponent<TileConvertFx>(out var fx)
            ? fx
            : tile.AddComponent<TileConvertFx>();
    }
}

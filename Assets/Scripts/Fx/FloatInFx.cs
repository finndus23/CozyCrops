using DG.Tweening;
using UnityEngine;

/// <summary>
/// "Aufploppen + Hochspringen" für Selection-Feedback.
/// Kombiniert einen optionalen Scale-Pop mit einem vertikalen Hop.
///
/// Warum der Scale-Pop optional ist:
/// - Das flache Highlight-Overlay braucht ihn, sonst ist es bei der isometrischen
///   Kamera kaum zu sehen (vertikale Bewegung projiziert auf wenige Pixel).
/// - Die soliden Boden-Tiles wollen NUR den Hop — ein Scale-Pop würde den Boden
///   kurz schrumpfen lassen und Lücken zwischen den Tiles aufreißen.
///
/// includeScalePop kann direkt nach AddComponent gesetzt werden; Play() läuft erst
/// in Start() (nächster Frame) und respektiert den Wert dann.
/// </summary>
public class FloatInFx : MonoBehaviour
{
    [Header("Hop nach oben")]
    [SerializeField] private float riseHeight = 0.6f;
    [SerializeField] private float riseDuration = 0.14f;
    [SerializeField] private float settleDuration = 0.18f;

    [Header("Scale-Pop (nur fürs Overlay sinnvoll)")]
    public bool includeScalePop = true;
    [SerializeField] private float startScale = 0.5f;
    [SerializeField] private float scaleDuration = 0.22f;
    [SerializeField] private Ease scaleEase = Ease.OutBack;

    private Vector3 targetPos;
    private Vector3 targetScale;
    private Sequence activeSequence;

    void Awake()
    {
        targetPos = transform.position;
        targetScale = transform.localScale;
    }

    void Start()
    {
        Play();
    }

    void OnDestroy()
    {
        activeSequence?.Kill();
    }

    public void Play()
    {
        activeSequence?.Kill();

        transform.position = targetPos;

        activeSequence = DOTween.Sequence();

        if (includeScalePop)
        {
            transform.localScale = targetScale * startScale;
            activeSequence.Join(transform.DOScale(targetScale, scaleDuration).SetEase(scaleEase));
        }

        // Hoch und wieder runter.
        activeSequence.Join(transform.DOMoveY(targetPos.y + riseHeight, riseDuration).SetEase(Ease.OutQuad));
        activeSequence.Insert(riseDuration,
            transform.DOMoveY(targetPos.y, settleDuration).SetEase(Ease.InQuad));
    }
}

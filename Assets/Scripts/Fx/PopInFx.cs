using DG.Tweening;
using UnityEngine;

/// <summary>
/// Generischer Pop-In-Effekt: Objekt skaliert beim Spawnen von 0 auf seine
/// ursprüngliche Größe hoch, mit leichtem Überschwingen (OutBack-Ease).
/// Für alles was "erscheint" und sich lebendig anfühlen soll —
/// Selection-Highlights, Tile-Swaps (Farmland/Path/Gras), etc.
/// </summary>
public class PopInFx : MonoBehaviour
{
    [SerializeField] private float duration = 0.22f;
    [SerializeField] private Ease ease = Ease.OutBack;

    private Vector3 targetScale;
    private Tween activeTween;

    void Awake()
    {
        targetScale = transform.localScale;
    }

    void Start()
    {
        Play();
    }

    void OnDestroy()
    {
        activeTween?.Kill();
    }

    public void Play()
    {
        activeTween?.Kill();
        transform.localScale = Vector3.zero;
        activeTween = transform.DOScale(targetScale, duration).SetEase(ease);
    }
}

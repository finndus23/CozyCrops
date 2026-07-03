using DG.Tweening;
using UnityEngine;

/// <summary>
/// Feel-Good-Polish für Pflanzen-Visuals.
/// Wird von PlantManager automatisch auf jedes gespawnte Pflanzen-GameObject gesetzt.
/// - Beim Spawnen/Stage-Wechsel: Pop-In (Squash & Stretch via OutBack-Ease)
/// - Bei der Ernte: kurzer Squash-Bounce, dann entweder in die Scheune fliegen
///   (PlayHarvestFlyTo) oder an Ort und Stelle wegschrumpfen (PlayHarvestAndDestroy).
/// </summary>
public class PlantGrowthFx : MonoBehaviour
{
    [Header("Grow-In (Spawn / Stage-Wechsel)")]
    [SerializeField] private float growInDuration = 0.35f;
    [SerializeField] private Ease growInEase = Ease.OutBack;

    [Header("Ernte")]
    [SerializeField] private float harvestSquashDuration = 0.08f;
    [SerializeField] private float harvestShrinkDuration = 0.22f;
    [SerializeField] private Ease harvestEase = Ease.InBack;

    [Header("Flug zur Scheune")]
    [Tooltip("Höhe des Flugbogens zur Scheune.")]
    [SerializeField] private float flyJumpPower = 2f;
    [SerializeField] private float flyDuration = 0.6f;
    [Tooltip("Auf welche Größe die Crop während des Flugs schrumpft (relativ zur Originalgröße).")]
    [SerializeField] private float flyEndScaleFactor = 0.2f;

    private Vector3 targetScale;
    private Sequence activeSequence;

    void Awake()
    {
        targetScale = transform.localScale;
    }

    void Start()
    {
        PlayGrowIn();
    }

    void OnDestroy()
    {
        activeSequence?.Kill();
    }

    /// <summary>Pop-In-Animation: startet bei Skalierung 0, schwingt kurz über die Zielgröße hinaus.</summary>
    public void PlayGrowIn()
    {
        activeSequence?.Kill();
        transform.localScale = Vector3.zero;
        activeSequence = DOTween.Sequence()
            .Append(transform.DOScale(targetScale, growInDuration).SetEase(growInEase));
    }

    /// <summary>
    /// Kurzer Squash-Bounce (leicht größer), dann Shrink auf 0, danach wird das GameObject zerstört.
    /// Fallback wenn keine Scheune in der Szene ist.
    /// </summary>
    public void PlayHarvestAndDestroy()
    {
        activeSequence?.Kill();
        activeSequence = DOTween.Sequence()
            .Append(transform.DOScale(targetScale * 1.15f, harvestSquashDuration).SetEase(Ease.OutQuad))
            .Append(transform.DOScale(Vector3.zero, harvestShrinkDuration).SetEase(harvestEase))
            .OnComplete(() => Destroy(gameObject));
    }

    /// <summary>
    /// Kurzer Squash-Pop, dann fliegt die Crop in einem Bogen zur Scheune und schrumpft
    /// dabei wie ein eingesammeltes Item, danach wird das GameObject zerstört.
    /// </summary>
    public void PlayHarvestFlyTo(Vector3 target)
    {
        activeSequence?.Kill();

        // Falls das Visual einen Collider hat: während des Flugs deaktivieren,
        // damit es keine Klick-Raycasts (WorldClickHandler) blockiert.
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        activeSequence = DOTween.Sequence()
            .Append(transform.DOScale(targetScale * 1.15f, harvestSquashDuration).SetEase(Ease.OutQuad))
            .Append(transform.DOJump(target, flyJumpPower, 1, flyDuration).SetEase(Ease.InOutQuad))
            .Join(transform.DOScale(targetScale * flyEndScaleFactor, flyDuration).SetEase(Ease.InQuad))
            .OnComplete(() => Destroy(gameObject));
    }
}

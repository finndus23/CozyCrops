using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Timing und Optik des Münzflugs. Liegt als eigene Klasse vor, damit sich das Gefühl
/// im Inspector einstellen lässt statt im Code zu stecken.
/// </summary>
[Serializable]
public class CoinFlightSettings
{
    [Header("Menge")]
    [Tooltip("Untergrenze — auch eine kleine Belohnung soll nach etwas aussehen.")]
    public int minCoins = 8;

    [Tooltip("Obergrenze, damit eine große Belohnung nicht den Bildschirm zuschüttet.")]
    public int maxCoins = 26;

    [Tooltip("Ein Münze je X Gold, zusätzlich zu minCoins.")]
    public int goldPerExtraCoin = 15;

    [Header("Timing")]
    [Tooltip("Versatz zwischen zwei Münzen. Größer = der Schwarm zieht sich länger.")]
    public float perCoinDelay = 0.055f;

    [Tooltip("Auseinanderstieben aus der Karte.")]
    public float scatterDuration = 0.3f;

    [Tooltip("Flug zur Geldanzeige. Der bestimmt das Tempo — hoch = gut sichtbar.")]
    public float flyDuration = 0.85f;

    public float popInDuration = 0.22f;

    [Header("Optik")]
    public float coinSize = 44f;

    [Tooltip("Wie weit die Münzen erst ausbrechen, bevor sie eingesammelt werden.")]
    public float scatterRadius = 80f;

    [Tooltip("Größe beim Aufschlag — leicht schrumpfend wirkt wie Tiefe.")]
    public float arriveScale = 0.6f;
}

/// <summary>
/// Münzflug beim Einsammeln einer Missions-Belohnung: ein Schwung Münzen stiebt aus der
/// Abhol-Karte, sammelt sich und fliegt zur Geldanzeige. Erst wenn die erste Münze
/// ankommt, wird die Belohnung tatsächlich gutgeschrieben — dadurch fällt das Hochzählen
/// des Zählers mit dem Einschlag zusammen statt vorher zu passieren.
///
/// Rein kosmetisch und ohne Zustand: geht der Flug verloren (Szenenwechsel), bleibt die
/// Belohnung im MissionManager liegen und kann erneut abgeholt werden.
/// </summary>
public static class RewardCollectFx
{
    /// <summary>
    /// Lässt Münzen von <paramref name="from"/> zur Geldanzeige fliegen.
    /// </summary>
    /// <param name="from">Startpunkt, üblicherweise die Abhol-Karte.</param>
    /// <param name="coinCount">Anzahl Münzen — nicht der Geldbetrag, nur Optik.</param>
    /// <param name="onFirstArrival">Wird ausgeführt sobald die erste Münze ankommt.</param>
    /// <param name="settings">Timing/Optik. Null = Standardwerte.</param>
    public static void PlayCoinFlight(RectTransform from, int coinCount, Action onFirstArrival,
                                      CoinFlightSettings settings = null)
    {
        settings ??= new CoinFlightSettings();

        var money = MoneyDisplay.Instance;

        // Ohne Geldanzeige (z.B. Marktplatz-Szene) trotzdem die Belohnung gutschreiben.
        // Der Effekt darf nie zur Voraussetzung für den Spielfortschritt werden.
        if (money == null || from == null || money.CoinSprite == null)
        {
            onFirstArrival?.Invoke();
            return;
        }

        RectTransform target = money.CoinAnchor;
        Canvas canvas = target.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            onFirstArrival?.Invoke();
            return;
        }

        Transform parent = canvas.rootCanvas != null ? canvas.rootCanvas.transform : canvas.transform;

        int count = Mathf.Clamp(coinCount, 1, Mathf.Max(1, settings.maxCoins));
        bool fired = false;

        for (int i = 0; i < count; i++)
        {
            var coin = CreateCoin(parent, money.CoinSprite, from.position, settings.coinSize);
            float delay = i * settings.perCoinDelay;

            // Erst auseinanderstieben, dann einsammeln — ohne den Ausbruch sieht es aus
            // als würde nur ein Sprite verschoben.
            Vector3 scatter = from.position + (Vector3)(UnityEngine.Random.insideUnitCircle * settings.scatterRadius);

            // Leicht gestreute Flugdauer: ein exakt gleichzeitig ankommender Schwarm
            // wirkt mechanisch.
            float fly = settings.flyDuration * UnityEngine.Random.Range(0.9f, 1.1f);

            Sequence seq = DOTween.Sequence();
            seq.AppendInterval(delay);
            seq.Append(coin.transform.DOScale(1f, settings.popInDuration).From(0.2f).SetEase(Ease.OutBack));
            seq.Join(coin.transform.DOMove(scatter, settings.scatterDuration).SetEase(Ease.OutQuad));
            seq.Append(coin.transform.DOMove(target.position, fly).SetEase(Ease.InBack));
            seq.Join(coin.transform.DOScale(settings.arriveScale, fly).SetEase(Ease.InQuad));
            seq.OnComplete(() =>
            {
                if (!fired)
                {
                    fired = true;
                    onFirstArrival?.Invoke();
                }

                money.PunchCoin();
                UnityEngine.Object.Destroy(coin);
            });
        }
    }

    private static GameObject CreateCoin(Transform parent, Sprite sprite, Vector3 worldPos, float size)
    {
        var go = new GameObject("RewardCoin", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(size, size);
        rect.position = worldPos;
        rect.SetAsLastSibling();

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        return go;
    }
}

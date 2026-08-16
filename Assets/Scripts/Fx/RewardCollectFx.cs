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

    [Header("Klang")]
    [Tooltip("Münz-Klang beim Eintreffen. Der Clip kommt aus der UiSfxLibrary, damit er " +
             "überall gleich ist.")]
    public bool playSound = true;

    [Tooltip("An = jede einzelne ankommende Münze klingt, nicht nur die erste.\n\n" +
             "Ergibt das klassische Münzprasseln und fühlt sich bei großen Beträgen " +
             "deutlich fetter an. Braucht aber einen sehr kurzen, leisen Clip — sonst wird " +
             "aus zwanzig Münzen ein einziger Krach.")]
    public bool soundPerCoin = false;

    [Tooltip("Lautstärke der Münzen, wenn soundPerCoin an ist. Deutlich runter, es kommen " +
             "viele.")]
    [Range(0f, 1f)]
    public float perCoinVolume = 0.35f;

    /// <summary>
    /// Ungefähre Zeit, bis die erste Münze ankommt. Die erste Münze hat keinen Startversatz,
    /// also nur Ausbrechen plus Flug.
    ///
    /// Nur ein Richtwert — die Flugdauer wird pro Münze leicht gestreut. Für das Timing des
    /// Geldzählers reicht das; auf den Frame genau muss es nicht sein.
    /// </summary>
    public float FirstArrivalTime => scatterDuration + flyDuration;
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
    /// <summary>
    /// Münzflug für einen Verkauf. Im Gegensatz zur Missions-Belohnung ist das Geld hier
    /// bereits gutgeschrieben, wenn der Flug startet — der Handel darf nicht davon abhängen,
    /// dass eine Animation zu Ende läuft. Deshalb ohne Rückruf: rein kosmetisch.
    /// </summary>
    /// <param name="from">Startpunkt, üblicherweise die Shop-Zeile.</param>
    /// <param name="gold">Verdienter Betrag — bestimmt nur, wie viele Münzen fliegen.</param>
    /// <param name="onFirstArrival">
    /// Läuft beim Eintreffen der ersten Münze. Hier gehört der Verkaufsklang hin: er soll
    /// den Einschlag begleiten, nicht den Klick. Auch wenn kein Flug zustande kommt, wird
    /// er ausgeführt — sonst bliebe der Verkauf in Szenen ohne Geldanzeige stumm.
    /// </param>
    public static void PlaySale(RectTransform from, int gold, CoinFlightSettings settings = null,
                                Action onFirstArrival = null)
    {
        settings ??= new CoinFlightSettings();
        PlayCoinFlight(from, CoinCountForGold(gold, settings), onFirstArrival, settings);
    }

    /// <summary>
    /// Wie viele Münzen ein Betrag wert ist. Bewusst gedeckelt: der Schwarm soll die Größe
    /// des Ertrags andeuten, nicht abbilden — bei 900 Gold will niemand 900 Sprites sehen.
    /// </summary>
    public static int CoinCountForGold(int gold, CoinFlightSettings settings = null)
    {
        settings ??= new CoinFlightSettings();

        int min = Mathf.Max(1, settings.minCoins);
        int max = Mathf.Max(min, settings.maxCoins);

        if (gold <= 0) return min;

        int perCoin = Mathf.Max(1, settings.goldPerExtraCoin);
        return Mathf.Clamp(min + gold / perCoin, min, max);
    }

    public static void PlayCoinFlight(RectTransform from, int coinCount, Action onFirstArrival,
                                      CoinFlightSettings settings = null)
    {
        settings ??= new CoinFlightSettings();

        var money = MoneyDisplay.Instance;

        // Ohne Geldanzeige (z.B. Marktplatz-Szene) trotzdem die Belohnung gutschreiben.
        // Der Effekt darf nie zur Voraussetzung für den Spielfortschritt werden.
        if (money == null || from == null || money.CoinSprite == null)
        {
            Arrive(onFirstArrival, settings);
            return;
        }

        RectTransform target = money.CoinAnchor;
        Canvas canvas = target.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Arrive(onFirstArrival, settings);
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
                    Arrive(onFirstArrival, settings);
                }
                else if (settings.playSound && settings.soundPerCoin)
                {
                    // Nur die Folgemünzen: die erste hat ihren Klang schon über Arrive()
                    // bekommen, und zwar in voller Lautstärke.
                    UiSfx.CoinFlightTick(settings.perCoinVolume);
                }

                money.PunchCoin();
                UnityEngine.Object.Destroy(coin);
            });
        }
    }

    /// <summary>
    /// Die Münzen sind da: Klang abspielen und den Rückruf auslösen.
    ///
    /// Beides zusammengefasst, weil beides denselben Moment meint — und weil die
    /// Abbruchpfade weiter oben sonst leicht den Klang vergessen. Genau dort wäre es am
    /// schlimmsten: ohne Geldanzeige fliegt gar nichts, und dann ist der Klang die einzige
    /// Rückmeldung, dass etwas angekommen ist.
    /// </summary>
    private static void Arrive(Action onFirstArrival, CoinFlightSettings settings)
    {
        if (settings == null || settings.playSound) UiSfx.CoinFlight();

        onFirstArrival?.Invoke();
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

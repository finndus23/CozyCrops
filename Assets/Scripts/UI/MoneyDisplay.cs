using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MoneyDisplay : MonoBehaviour
{
    /// <summary>Damit Belohnungs-FX wissen, wohin die Münzen fliegen sollen.</summary>
    public static MoneyDisplay Instance { get; private set; }

    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite coinSprite;

    [Header("Layout")]
    [SerializeField] private Vector2 minSize = new(180f, 70f);
    [SerializeField] private float horizontalPadding = 58f;
    [SerializeField] private float backgroundHeight = 70f;

    [Header("Hochzählen")]
    [Tooltip("Wie lange der Zähler bei einer Gutschrift hochläuft. 0 = sofort setzen.\n\n" +
             "Sollte ungefähr so lang sein wie der Münzschwarm zum Eintreffen braucht — " +
             "der Zähler startet beim Einschlag der ERSTEN Münze und sollte nicht fertig " +
             "sein, während noch welche unterwegs sind.")]
    [SerializeField] private float countUpDuration = 0.9f;

    [Tooltip("Ausgaben laufen nicht rückwärts mit — abgezogenes Geld wird sofort gesetzt. " +
             "Ein rückwärts trudelnder Zähler liest sich wie ein Fehler.")]
    [SerializeField] private bool animateOnlyGains = true;

    [Tooltip("Kurzer Puls auf dem Münz-Icon pro Gutschrift.")]
    [SerializeField] private float coinPunchScale = 0.35f;

    private RectTransform textRect;
    private RectTransform backgroundRect;
    private RectTransform coinRect;

    // Der Wert, der gerade auf dem Bildschirm steht — nicht das Ziel. Beides
    // auseinanderzuhalten ist nötig, sobald das Hochzählen verzögert startet: kommt
    // währenddessen eine weitere Gutschrift, muss die neue Zählung dort ansetzen, wo der
    // Spieler gerade hinschaut, und nicht beim schon gebuchten Zielwert.
    private int visibleAmount;

    private Tween countTween;
    private Tween punchTween;

    // Vorlauf für die nächste Gutschrift, gesetzt von außen (Münzflug).
    private float pendingGainDelay;

    /// <summary>Münz-Icon — Flugziel für eingesammelte Belohnungen.</summary>
    public RectTransform CoinAnchor => coinRect != null ? coinRect : (RectTransform)transform;

    /// <summary>Dieselbe Sprite wird für die fliegenden Münzen benutzt.</summary>
    public Sprite CoinSprite => coinSprite;

    void Awake() => Instance = this;

    void Start()
    {
        textRect = (RectTransform)transform;
        CreateBackground();
        PlayerInventory.Instance.OnMoneyChanged += UpdateDisplay;

        visibleAmount = PlayerInventory.Instance.Money;
        SetText(visibleAmount);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        countTween?.Kill();
        punchTween?.Kill();

        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnMoneyChanged -= UpdateDisplay;
    }

    /// <summary>
    /// Bringt den Zähler auf den neuen Betrag. Gutschriften laufen hoch, damit man sieht
    /// dass etwas angekommen ist — vor allem beim Einsammeln einer Missions-Belohnung,
    /// wo die Münzen hierher fliegen und der Zähler das Ergebnis bestätigt.
    /// </summary>
    /// <summary>
    /// Hält die nächste Gutschrift um <paramref name="seconds"/> zurück, bevor der Zähler
    /// hochzuzählen beginnt.
    ///
    /// Gedacht für den Münzflug beim Verkauf: dort ist das Geld schon gebucht, während die
    /// Münzen noch unterwegs sind. Ohne den Vorlauf wäre der Zähler fertig, bevor die erste
    /// Münze ankommt — und der Flug sähe aus, als hätte er nichts damit zu tun.
    ///
    /// Bei Missions-Belohnungen ist das nicht nötig: dort wird das Geld ohnehin erst beim
    /// Eintreffen der ersten Münze gebucht.
    /// </summary>
    public void DelayNextGain(float seconds)
    {
        if (seconds <= 0f) return;

        // Bei mehreren Verkäufen kurz hintereinander den längeren Vorlauf behalten —
        // sonst überholt eine späte Buchung die noch fliegenden Münzen der ersten.
        pendingGainDelay = Mathf.Max(pendingGainDelay, seconds);
    }

    /// <summary>
    /// Nimmt einen angemeldeten Vorlauf zurück. Nötig, wenn zwischen Anmeldung und Buchung
    /// doch noch etwas schiefgeht — sonst würde die Verzögerung die nächste, völlig
    /// unbeteiligte Gutschrift ausbremsen.
    /// </summary>
    public void ClearPendingGainDelay() => pendingGainDelay = 0f;

    private void UpdateDisplay(int amount)
    {
        if (moneyText == null) return;

        countTween?.Kill();

        bool isGain = amount > visibleAmount;

        float delay = pendingGainDelay;
        pendingGainDelay = 0f;

        if (countUpDuration <= 0f || (animateOnlyGains && !isGain))
        {
            visibleAmount = amount;
            SetText(amount);
            return;
        }

        int from = visibleAmount;

        countTween = DOVirtual.Float(from, amount, countUpDuration, v =>
            {
                visibleAmount = Mathf.RoundToInt(v);
                SetText(visibleAmount);
            })
            .SetEase(Ease.OutCubic)
            .SetDelay(delay)
            .OnComplete(() =>
            {
                visibleAmount = amount;
                SetText(amount);
            });

        if (!isGain) return;

        // Der Stups gehört zum Einschlag, nicht zum Auslöser — also ebenfalls verzögert.
        punchTween?.Kill();

        if (delay <= 0f) PunchCoin();
        else punchTween = DOVirtual.DelayedCall(delay, PunchCoin);
    }

    /// <summary>Kurzer Stups aufs Münz-Icon — "hier ist was angekommen".</summary>
    public void PunchCoin()
    {
        if (coinRect == null || coinPunchScale <= 0f) return;

        coinRect.DOKill(true);
        coinRect.localScale = Vector3.one;
        coinRect.DOPunchScale(Vector3.one * coinPunchScale, 0.35f, 8, 0.6f);
    }

    private void SetText(int amount)
    {
        moneyText.text = amount.ToString("N0");
        ResizeToFitText();
    }

    private void CreateBackground()
    {
        if (backgroundSprite == null || moneyText == null) return;

        var go = new GameObject("MoneyBackground", typeof(RectTransform));
        go.transform.SetParent(transform.parent, false);
        go.transform.SetSiblingIndex(transform.GetSiblingIndex());

        backgroundRect = go.GetComponent<RectTransform>();
        RectTransform sourceRect = textRect != null ? textRect : (RectTransform)transform;
        backgroundRect.anchorMin = sourceRect.anchorMin;
        backgroundRect.anchorMax = sourceRect.anchorMax;
        backgroundRect.pivot = sourceRect.pivot;
        backgroundRect.anchoredPosition = sourceRect.anchoredPosition;
        backgroundRect.sizeDelta = sourceRect.sizeDelta;

        Image image = go.AddComponent<Image>();
        image.sprite = backgroundSprite;
        image.type = Image.Type.Sliced;
        image.preserveAspect = false;
        image.raycastTarget = false;

        moneyText.raycastTarget = false;
        moneyText.color = new Color(0.26f, 0.14f, 0.04f, 1f);
        moneyText.alignment = TextAlignmentOptions.Center;
        moneyText.margin = new Vector4(58f, 0f, 12f, 0f);
        moneyText.enableAutoSizing = true;
        moneyText.fontSizeMin = 18f;
        moneyText.fontSizeMax = 30f;
        moneyText.textWrappingMode = TextWrappingModes.NoWrap;
        moneyText.overflowMode = TextOverflowModes.Ellipsis;

        CreateCoinIcon();
    }

    private void CreateCoinIcon()
    {
        if (coinSprite == null || moneyText == null) return;

        GameObject coin = new("MoneyCoinIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        coin.transform.SetParent(transform, false);

        coinRect = coin.GetComponent<RectTransform>();
        coinRect.anchorMin = new Vector2(0f, 0.5f);
        coinRect.anchorMax = new Vector2(0f, 0.5f);
        coinRect.pivot = new Vector2(0.5f, 0.5f);
        coinRect.anchoredPosition = new Vector2(34f, 0f);
        coinRect.sizeDelta = new Vector2(44f, 44f);

        Image image = coin.GetComponent<Image>();
        image.sprite = coinSprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private void ResizeToFitText()
    {
        if (textRect == null)
            textRect = (RectTransform)transform;

        float height = Mathf.Max(backgroundHeight, minSize.y);
        Vector2 preferred = moneyText.GetPreferredValues(moneyText.text, 1000f, height);
        float width = Mathf.Max(minSize.x, preferred.x + horizontalPadding);
        Vector2 size = new(width, height);

        textRect.sizeDelta = size;
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        if (backgroundRect == null) return;

        backgroundRect.anchorMin = textRect.anchorMin;
        backgroundRect.anchorMax = textRect.anchorMax;
        backgroundRect.pivot = textRect.pivot;
        backgroundRect.anchoredPosition = textRect.anchoredPosition;
        backgroundRect.sizeDelta = size;
        backgroundRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        backgroundRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }
}

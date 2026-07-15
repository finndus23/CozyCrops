using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MoneyDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite coinSprite;

    [Header("Layout")]
    [SerializeField] private Vector2 minSize = new(180f, 70f);
    [SerializeField] private float horizontalPadding = 58f;
    [SerializeField] private float backgroundHeight = 70f;

    private RectTransform textRect;
    private RectTransform backgroundRect;
    private RectTransform coinRect;

    void Start()
    {
        textRect = (RectTransform)transform;
        CreateBackground();
        PlayerInventory.Instance.OnMoneyChanged += UpdateDisplay;
        UpdateDisplay(PlayerInventory.Instance.Money);
    }

    void OnDestroy()
    {
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnMoneyChanged -= UpdateDisplay;
    }

    private void UpdateDisplay(int amount)
    {
        if (moneyText == null) return;

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

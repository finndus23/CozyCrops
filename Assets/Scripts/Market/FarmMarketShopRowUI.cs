using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Eine UI-Zeile im Shop. Wird für Kaufen und Verkaufen benutzt.
/// </summary>
public class FarmMarketShopRowUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button primaryButton;
    [SerializeField] private TMP_Text primaryButtonText;
    [SerializeField] private Button secondaryButton;
    [SerializeField] private TMP_Text secondaryButtonText;

    public void Setup(
        Sprite icon,
        string displayName,
        string amountLabel,
        string priceLabel,
        string primaryLabel,
        Action primaryAction,
        string secondaryLabel,
        Action secondaryAction)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (nameText != null)
            nameText.text = displayName;

        if (amountText != null)
            amountText.text = amountLabel;

        if (priceText != null)
            priceText.text = priceLabel;

        SetupButton(primaryButton, primaryButtonText, primaryLabel, primaryAction);
        SetupButton(secondaryButton, secondaryButtonText, secondaryLabel, secondaryAction);
    }

    private void SetupButton(Button button, TMP_Text label, string text, Action action)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();

        if (label != null)
            label.text = text;

        if (action != null)
            button.onClick.AddListener(() => action.Invoke());

        button.interactable = action != null;
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        Action secondaryAction,
        string tooltipText = null)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.preserveAspect = true;
        }

        if (nameText != null)
            nameText.text = displayName;

        if (amountText != null)
            amountText.text = amountLabel;

        if (priceText != null)
            priceText.text = priceLabel;

        SetupButton(primaryButton, primaryButtonText, primaryLabel, primaryAction);
        SetupButton(secondaryButton, secondaryButtonText, secondaryLabel, secondaryAction);
        SetupTooltip(tooltipText);

        SetDimmed(false);
    }

    /// <summary>
    /// Zeile abdunkeln — für Ware die sichtbar, aber (noch) nicht kaufbar ist.
    ///
    /// Über eine CanvasGroup statt einzelner Farben: so verblassen Icon, Texte und Knöpfe
    /// gemeinsam und die Zeile bleibt lesbar. Der Spieler soll sehen, was es gibt.
    /// </summary>
    public void SetDimmed(bool dimmed)
    {
        if (!TryGetComponent(out CanvasGroup group))
            group = gameObject.AddComponent<CanvasGroup>();

        group.alpha = dimmed ? 0.45f : 1f;
    }

    private void SetupButton(Button button, TMP_Text label, string text, Action action)
    {
        if (button == null)
            return;

        // Ohne Beschriftung gibt's für diese Zeile keinen zweiten Button (z.B. Upgrade-
        // und Lizenz-Zeilen nutzen nur primaryButton) — dann ganz ausblenden statt eine
        // leere, deaktivierte Box stehen zu lassen. Ein Label wie "—" (MAX Level) bleibt
        // sichtbar, nur nicht klickbar.
        bool used = !string.IsNullOrEmpty(text);
        button.gameObject.SetActive(used);
        if (!used)
            return;

        button.onClick.RemoveAllListeners();

        if (label != null)
            label.text = text;

        if (action != null)
            button.onClick.AddListener(() => action.Invoke());

        button.interactable = action != null;
    }

    /// <summary>
    /// Hover über die Stats-Zeile zeigt die vollen Details (AoE, Warteschlange, Ertrag …).
    /// Ohne <paramref name="text"/> wird ein evtl. vorhandener Trigger nur deaktiviert,
    /// nicht entfernt — die Zeilen werden ja bei jedem Refresh neu instanziiert.
    /// </summary>
    private void SetupTooltip(string text)
    {
        if (amountText == null)
            return;

        // Text-Labels haben im Prefab standardmäßig "Raycast Target" aus (spart
        // Raycasts bei reinem Anzeigetext). Ohne einen raycastbaren Graphen an dieser
        // Stelle sieht der GraphicRaycaster das Objekt gar nicht — der EventTrigger
        // bekäme dann nie einen PointerEnter/Exit gemeldet, egal wie er verdrahtet ist.
        amountText.raycastTarget = !string.IsNullOrEmpty(text);

        if (!amountText.TryGetComponent(out EventTrigger trigger))
            trigger = amountText.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear();

        if (string.IsNullOrEmpty(text))
            return;

        RectTransform anchor = (RectTransform)amountText.transform;

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => UiTooltip.Show(anchor, text));
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => UiTooltip.Hide());
        trigger.triggers.Add(exit);
    }

    private void OnDisable()
    {
        // Zeile wird beim Refresh oft mitten im Hover destroyed — PointerExit feuert dann
        // nicht mehr zuverlässig. Ohne das bliebe der Tooltip tot am Bildschirm hängen.
        UiTooltip.Hide();
    }
}

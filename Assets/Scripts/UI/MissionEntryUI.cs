using System;
using System.Text;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Ein Eintrag im Quest-Tracker. Zwei Zustände:
///  • laufend       — Titel + Ziele mit Fortschritt
///  • abgeschlossen — Erfolgsmeldung, Belohnungsliste und ein "Abholen"-Knopf
/// </summary>
public class MissionEntryUI : MonoBehaviour
{
    private const string BodyColor = "#4D463B";
    private const string CompletedColor = "#4D7F36";
    private const string RewardColor = "#8A5A1E";
    private const string CounterColor = "#8A7F6B";

    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI objectivesText;

    [Header("Belohnung abholen")]
    [Tooltip("Optional. Leer lassen — der Knopf wird beim Abschluss zur Laufzeit angelegt.")]
    [SerializeField] private Button collectButton;
    [SerializeField] private Color collectButtonColor = new(0.42f, 0.62f, 0.24f, 1f);
    [SerializeField] private string collectButtonLabel = "Belohnung abholen";
    [SerializeField] private float collectButtonHeight = 32f;

    [Tooltip("Menge, Tempo und Optik der fliegenden Münzen.")]
    [SerializeField] private CoinFlightSettings coinFlight = new();

    private MissionData data;
    private int[] progressSnapshot;
    private Action onCollect;
    private bool collecting;

    /// <summary>Titel der dargestellten Mission — das Panel zeigt ihn im Kopfbanner.</summary>
    public string MissionTitle => data != null ? data.title : null;

    public void Init(MissionData missionData)
    {
        data = missionData;
        ApplyTextStyle();

        if (titleText != null)
            titleText.text = missionData.title;

        progressSnapshot = new int[missionData.objectives?.Length ?? 0];
        RefreshText();
    }

    /// <summary>Titel ausblenden wenn ihn das Kopfbanner des Panels schon zeigt.</summary>
    public void SetInlineTitleVisible(bool visible)
    {
        if (titleText != null)
            titleText.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Schaltet den Eintrag auf die Abschluss-Meldung um. Die Belohnung wird
    /// <b>automatisch</b> eingesammelt: kurz stehen lassen, Münzen losschicken,
    /// und bei deren Ankunft läuft <paramref name="collectAction"/>.
    ///
    /// Bewusst ohne Knopf — der Abschluss ist eine Belohnung, keine Aufgabe.
    /// </summary>
    public void ShowCompleted(MissionData missionData, Action collectAction)
    {
        data = missionData;
        onCollect = collectAction;
        collecting = false;

        ApplyTextStyle();

        if (titleText != null)
        {
            titleText.gameObject.SetActive(true);
            titleText.text = missionData.title;
        }

        if (objectivesText != null)
        {
            var sb = new StringBuilder();
            sb.Append($"<color={CompletedColor}><b>Geschafft!</b></color>");

            if (missionData.rewards != null)
            {
                foreach (var reward in missionData.rewards)
                {
                    string line = MissionRewardFormatter.Format(reward);
                    if (string.IsNullOrEmpty(line)) continue;
                    sb.AppendLine();
                    sb.Append($"<color={RewardColor}>{line}</color>");
                }
            }

            objectivesText.text = sb.ToString();
        }

        EnsureCollectButton();
        PlayCompletePulse();
    }

    private void HandleCollectClicked()
    {
        // Gegen Doppelklick: TryCollectRewards liefert beim zweiten Mal zwar false,
        // die Münzen wären aber schon ein zweites Mal unterwegs.
        if (collecting) return;
        collecting = true;

        if (collectButton != null)
            collectButton.interactable = false;

        int coins = MissionRewardFormatter.CoinCountFor(data, coinFlight);
        RewardCollectFx.PlayCoinFlight((RectTransform)transform, coins, () => onCollect?.Invoke(), coinFlight);
    }

    private void EnsureCollectButton()
    {
        if (collectButton == null)
            collectButton = BuildCollectButton();

        if (collectButton == null) return;

        collectButton.gameObject.SetActive(true);
        collectButton.interactable = true;
        collectButton.onClick.RemoveListener(HandleCollectClicked);
        collectButton.onClick.AddListener(HandleCollectClicked);
    }

    /// <summary>
    /// Baut den Knopf zur Laufzeit, damit das MissionEntry-Prefab nicht angefasst werden muss.
    /// </summary>
    private Button BuildCollectButton()
    {
        var go = new GameObject("CollectButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(transform, false);

        // Die VerticalLayoutGroup des Eintrags hat childControlWidth=1 und
        // childForceExpandWidth=0: sie setzt die Breite also aus dem LayoutElement des
        // Kindes und streckt NICHT von selbst. Ohne flexibleWidth bliebe der Knopf auf
        // der Default-Breite eines per Code erzeugten RectTransform — also 0. Genau
        // deshalb stand die Beschriftung erst als ein Zeichen pro Zeile untereinander.
        var layout = go.AddComponent<LayoutElement>();
        layout.minWidth = 100f;
        layout.flexibleWidth = 1f;
        layout.minHeight = collectButtonHeight;
        layout.preferredHeight = collectButtonHeight;

        var image = go.GetComponent<Image>();
        image.color = collectButtonColor;
        image.raycastTarget = true;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(6f, 0f);
        labelRect.offsetMax = new Vector2(-6f, 0f);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = collectButtonLabel;
        label.fontSize = 14f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = 9f;
        label.fontSizeMax = 14f;
        // Zweite Sicherung gegen den Zeichen-pro-Zeile-Effekt: sollte der Knopf doch mal
        // zu schmal geraten, schrumpft lieber die Schrift als dass der Text umbricht.
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;

        return button;
    }

    private void PlayCompletePulse()
    {
        var rect = (RectTransform)transform;
        rect.DOKill(true);
        rect.localScale = Vector3.one;
        rect.DOPunchScale(Vector3.one * 0.06f, 0.4f, 6, 0.8f);
    }

    public void UpdateObjective(int index, int current, int required)
    {
        if (progressSnapshot == null || index >= progressSnapshot.Length) return;

        progressSnapshot[index] = current;
        RefreshText();
    }

    private void RefreshText()
    {
        if (data?.objectives == null || objectivesText == null) return;

        ApplyTextStyle();

        var sb = new StringBuilder();
        bool sequential = data.sequentialObjectives;
        bool activeShown = false;
        bool anyAdded = false;

        for (int i = 0; i < data.objectives.Length; i++)
        {
            var obj = data.objectives[i];
            int current = progressSnapshot != null && i < progressSnapshot.Length ? progressSnapshot[i] : 0;
            bool done = current >= obj.requiredAmount;

            if (sequential && !done)
            {
                if (activeShown) continue;
                activeShown = true;
            }

            string label = MissionObjectiveFormatter.Format(obj);

            // Zählerstand hinter das Ziel. Wichtig geworden, seit die Ziele parallel
            // laufen: es stehen jetzt vier offene Zeilen gleichzeitig da, und ohne Zahl
            // sieht man nicht, welche davon fast durch ist und welche noch gar nicht
            // angefangen hat.
            string counter = !done && obj.requiredAmount > 1
                ? $" <color={CounterColor}>{Mathf.Clamp(current, 0, obj.requiredAmount)}/{obj.requiredAmount}</color>"
                : string.Empty;

            string line = done
                ? $"<color={CompletedColor}>OK <s>{label}</s></color>"
                : $"<color={BodyColor}>- {label}</color>{counter}";

            if (anyAdded) sb.AppendLine();
            sb.Append(line);
            anyAdded = true;
        }

        objectivesText.text = sb.ToString();
    }

    private void ApplyTextStyle()
    {
        if (titleText != null)
        {
            titleText.color = new Color(0.22f, 0.2f, 0.17f, 1f);
            titleText.fontSize = 16f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 12f;
            titleText.fontSizeMax = 16f;
            titleText.raycastTarget = false;
        }

        if (objectivesText != null)
        {
            objectivesText.color = new Color(0.3f, 0.27f, 0.23f, 1f);
            objectivesText.fontSize = 13.5f;
            objectivesText.fontStyle = FontStyles.Normal;
            objectivesText.alignment = TextAlignmentOptions.Left;
            objectivesText.enableAutoSizing = true;
            objectivesText.fontSizeMin = 10f;
            objectivesText.fontSizeMax = 13.5f;
            objectivesText.lineSpacing = -4f;
            objectivesText.textWrappingMode = TextWrappingModes.Normal;
            objectivesText.overflowMode = TextOverflowModes.Ellipsis;
            objectivesText.raycastTarget = false;
        }
    }
}

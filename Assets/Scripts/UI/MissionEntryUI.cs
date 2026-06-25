using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// One mission entry in the quest tracker.
/// </summary>
public class MissionEntryUI : MonoBehaviour
{
    private const string BodyColor = "#4D463B";
    private const string CompletedColor = "#4D7F36";

    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI objectivesText;

    private MissionData data;
    private int[] progressSnapshot;

    public void Init(MissionData missionData)
    {
        data = missionData;
        ApplyTextStyle();

        if (titleText != null)
            titleText.text = missionData.title;

        progressSnapshot = new int[missionData.objectives?.Length ?? 0];
        RefreshText();
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
            string line = done
                ? $"<color={CompletedColor}>OK <s>{label}</s></color>"
                : $"<color={BodyColor}>- {label}</color>";

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

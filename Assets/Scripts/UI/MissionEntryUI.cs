using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// Eine Mission im Quest-Tracker.
/// Prefab: MissionEntry (VLG, Content Size Fitter Preferred Height)
///   ├── TitleText  (TMP)
///   └── ObjectivesText (TMP, Content Size Fitter Preferred Height)
/// Kein ObjectiveItem-Prefab nötig.
/// </summary>
public class MissionEntryUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI objectivesText;

    private MissionData data;
    private int[] progressSnapshot;

    public void Init(MissionData missionData)
    {
        data = missionData;

        if (titleText != null) titleText.text = missionData.title;

        progressSnapshot = new int[missionData.objectives?.Length ?? 0];
        RefreshText();
    }

    // Wird von MissionsUI aufgerufen wenn sich ein Objective ändert
    public void UpdateObjective(int index, int current, int required)
    {
        if (progressSnapshot == null || index >= progressSnapshot.Length) return;
        progressSnapshot[index] = current;
        RefreshText();
    }

    private void RefreshText()
    {
        if (data?.objectives == null || objectivesText == null) return;

        var sb = new StringBuilder();
        bool sequential = data.sequentialObjectives;
        bool activeShown = false; // Bei sequential: nur ein offenes Objective anzeigen
        bool anyAdded = false;

        for (int i = 0; i < data.objectives.Length; i++)
        {
            var obj = data.objectives[i];
            int current = (progressSnapshot != null && i < progressSnapshot.Length) ? progressSnapshot[i] : 0;
            bool done = current >= obj.requiredAmount;

            // Bei sequential: abgehakte immer zeigen, aber nur das erste offene
            if (sequential && !done)
            {
                if (activeShown) continue; // Zukünftige Objectives ausblenden
                activeShown = true;
            }

            string label = MissionObjectiveFormatter.Format(obj);
            string line = done
                ? $"<color=#88FF88>✓ <s>{label}</s></color>"
                : $"◻ {label}";

            if (anyAdded) sb.AppendLine();
            sb.Append(line);
            anyAdded = true;
        }

        objectivesText.text = sb.ToString();
    }
}

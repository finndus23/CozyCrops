using UnityEngine;
using TMPro;

/// <summary>
/// Ein einzelnes Objective im Quest-Tracker.
/// Prefab: GameObject mit dieser Component + TextMeshProUGUI.
/// </summary>
public class MissionObjectiveItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;

    private string baseText;

    public void Init(string text)
    {
        baseText = text;
        Refresh(false);
    }

    public void SetCompleted(bool completed)
    {
        Refresh(completed);
    }

    private void Refresh(bool completed)
    {
        if (label == null) return;

        label.text = completed
            ? $"<color=#88FF88>✓ <s>{baseText}</s></color>"
            : $"◻ {baseText}";
    }
}

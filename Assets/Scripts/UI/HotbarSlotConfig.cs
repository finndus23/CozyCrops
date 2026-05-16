using UnityEngine;

[System.Serializable]
public class HotbarSlotConfig
{
    public ToolType toolType;
    public Sprite icon;

    [Tooltip("Zeigt Item-Anzahl an (z.B. für den Seed-Slot)")]
    public bool showCount;

    [Tooltip("Öffnet Dropdown bei Rechtsklick oder Leertaste")]
    public bool hasDropdown;
}

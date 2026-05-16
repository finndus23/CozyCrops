using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Einfache Ernte-Übersicht — wird später von Designern ersetzt.
/// Braucht im Inspector: panel (das Root-GameObject) + listText (ein TMP_Text darin).
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text listText;

    void Awake() => Instance = this;

    void Start()
    {
        PlayerInventory.Instance.OnCropsChanged += (_, _) =>
        {
            if (panel.activeSelf) Refresh();
        };

        panel.SetActive(false);
    }

    void Update()
    {
        if (!panel.activeSelf) return;
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            Close();
    }

    public void Toggle() => SetVisible(!panel.activeSelf);
    public void Close()  => SetVisible(false);

    private void SetVisible(bool visible)
    {
        panel.SetActive(visible);
        if (visible) Refresh();
    }

    private void Refresh()
    {
        var crops = PlayerInventory.Instance.GetAllCrops();
        var sb = new StringBuilder();
        bool any = false;

        foreach (var (crop, count) in crops)
        {
            if (count <= 0) continue;
            sb.AppendLine($"{crop.plantName}  x{count}");
            any = true;
        }

        listText.text = any ? sb.ToString().TrimEnd() : "Keine Ernte vorhanden.";
    }
}

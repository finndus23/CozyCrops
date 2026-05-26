using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optionales Script für Slot-Buttons.
/// Es aktualisiert den Button-Text mit "Leer" oder Infos aus der Save-Datei.
/// Wenn du TextMeshPro benutzt, kannst du die Anzeige auch manuell machen und dieses Script weglassen.
/// </summary>
public class MainMenuSlotButton : MonoBehaviour
{
    [SerializeField] [Range(1, 3)] private int slotIndex = 1;
    [SerializeField] private MainMenuController menuController;
    [SerializeField] private Text label;
    [SerializeField] private Button button;

    public int SlotIndex => slotIndex;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(ClickSlot);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(ClickSlot);
    }

    public void Refresh()
    {
        if (label == null)
            return;

        if (FarmSaveManager.Instance == null)
        {
            label.text = $"Spielstand {slotIndex}";
            return;
        }

        if (!FarmSaveManager.Instance.TryReadSlotData(slotIndex, out SaveGameData data))
        {
            label.text = $"Spielstand {slotIndex}\nLeer";
            return;
        }

        string savedDate = "Unbekannt";

        if (data.savedAtUnix > 0)
        {
            DateTime localTime = DateTimeOffset.FromUnixTimeSeconds(data.savedAtUnix).LocalDateTime;
            savedDate = localTime.ToString("dd.MM.yyyy HH:mm");
        }

        label.text = $"Spielstand {slotIndex}\nGeld: {data.money}\nGespeichert: {savedDate}";
    }

    public void ClickSlot()
    {
        if (menuController == null)
            menuController = FindObjectOfType<MainMenuController>();

        if (menuController == null)
        {
            Debug.LogError("[MainMenuSlotButton] Kein MainMenuController gefunden.");
            return;
        }

        menuController.PlaySlot(slotIndex);
    }
}

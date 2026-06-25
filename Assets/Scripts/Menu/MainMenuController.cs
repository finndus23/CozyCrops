using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Einfacher Hauptmenü-Controller:
/// - Play zeigt Slot-Auswahl
/// - Settings zeigt Settings-Panel
/// - Quit beendet Spiel/Playmode
/// - Slot 1-3 lädt die GameScene und danach den passenden SaveSlot
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject slotSelectionPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Scene")]
    [Tooltip("Name deiner Farm/Game-Szene. Muss exakt so heißen wie in File > Build Settings > Scenes In Build.")]
    [SerializeField] private string gameSceneName = "SampleScene";

    [Header("Optional")]
    [SerializeField] private MainMenuSlotButton[] slotButtons;

    private void Start()
    {
        ShowMainPanel();
        RefreshSlotButtons();
    }

    public void OnPlayClicked()
    {
        ShowSlotSelectionPanel();
    }

    public void OnSettingsClicked()
    {
        ShowSettingsPanel();
    }

    public void OnBackClicked()
    {
        ShowMainPanel();
    }

    public void ShowMainPanel()
    {
        SetPanel(mainPanel, true);
        SetPanel(slotSelectionPanel, false);
        SetPanel(settingsPanel, false);
    }

    public void ShowSlotSelectionPanel()
    {
        SetPanel(mainPanel, false);
        SetPanel(slotSelectionPanel, true);
        SetPanel(settingsPanel, false);

        RefreshSlotButtons();
    }

    public void ShowSettingsPanel()
    {
        SetPanel(mainPanel, false);
        SetPanel(slotSelectionPanel, false);
        SetPanel(settingsPanel, true);
    }

    public void PlaySlot1()
    {
        PlaySlot(1);
    }

    public void PlaySlot2()
    {
        PlaySlot(2);
    }

    public void PlaySlot3()
    {
        PlaySlot(3);
    }

    public void PlaySlot(int slotIndex)
    {
        if (FarmSaveManager.Instance == null)
        {
            Debug.LogError("[MainMenuController] Kein FarmSaveManager gefunden. Lege in der MainMenu-Scene ein GameObject 'SaveSystem' mit FarmSaveManager an.");
            return;
        }

        FarmSaveManager.Instance.StartGameFromSlot(slotIndex, gameSceneName);
    }

    public void DeleteSlot1()
    {
        DeleteSlot(1);
    }

    public void DeleteSlot2()
    {
        DeleteSlot(2);
    }

    public void DeleteSlot3()
    {
        DeleteSlot(3);
    }

    public void DeleteSlot(int slotIndex)
    {
        if (FarmSaveManager.Instance == null)
        {
            Debug.LogError("[MainMenuController] Kein FarmSaveManager gefunden. Slot konnte nicht gelöscht werden.");
            return;
        }

        FarmSaveManager.Instance.DeleteSlot(slotIndex);
        RefreshSlotButtons();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void RefreshSlotButtons()
    {
        if (slotButtons == null) return;

        foreach (MainMenuSlotButton slotButton in slotButtons)
        {
            if (slotButton == null) continue;
            slotButton.Refresh();
        }
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    private const string VolumeKey = "Settings.MasterVolume";
    private const string DisplayModeKey = "Settings.DisplayMode";

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject slotSelectionPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Scene")]
    [Tooltip("Name deiner Farm/Game-Szene. Muss exakt so heißen wie in File > Build Settings > Scenes In Build.")]
    [SerializeField] private string gameSceneName = "SampleScene";

    [Header("Optional")]
    [SerializeField] private MainMenuSlotButton[] slotButtons;

    [Header("Menu UI Sheet")]
    [SerializeField] private Sprite parchmentSprite;
    [SerializeField] private Sprite slotCardSprite;
    [SerializeField] private Sprite normalButtonSprite;
    [SerializeField] private Sprite highlightedButtonSprite;
    [SerializeField] private Sprite inputFieldSprite;
    [SerializeField] private Sprite sliderBackgroundSprite;
    [SerializeField] private Sprite sliderFillSprite;
    [SerializeField] private Sprite sliderHandleSprite;
    [SerializeField] private Sprite displaySelectorSprite;
    [SerializeField] private Sprite displayFullscreenSprite;
    [SerializeField] private Sprite displayWindowedSprite;
    [SerializeField] private Sprite displayBorderlessSprite;
    [SerializeField] private Sprite deleteButtonSprite;
    [SerializeField] private Sprite coinSprite;

    private TMP_InputField nameInput;
    private GameObject nameDialog;
    private int pendingSlot;

    /// <summary>Im Anlege-Dialog gewähltes Tempo. Gilt nur für den neuen Spielstand.</summary>
    private GamePace pendingPace = GamePace.Normal;
    private readonly Image[] paceHighlights = new Image[2];
    private TMP_Text paceDescriptionText;

    private int displayMode;
    private int slotSelectionOpenedFrame = -1;
    private readonly Image[] displayModeHighlights = new Image[3];
    private Image displaySelectorImage;
    private static readonly Color TextBrown = new Color(0.22f, 0.12f, 0.06f, 1f);
    private static readonly Color SlotButtonTextBrown = new Color32(0x38, 0x1F, 0x0F, 0xFF);

    private void Start()
    {
        BindSceneUi();
        ShowMainPanel();
        RefreshSlotButtons();
    }

    private void BindSceneUi()
    {
        Transform dialog = slotSelectionPanel != null ? FindDeepChild(slotSelectionPanel.transform, "Name Dialog") : null;
        if (dialog != null)
        {
            nameDialog = dialog.gameObject;
            nameInput = dialog.GetComponentInChildren<TMP_InputField>(true);
            Button create = FindDeepChild(dialog, "Create")?.GetComponent<Button>();
            Button cancel = FindDeepChild(dialog, "Cancel")?.GetComponent<Button>();
            if (create != null) create.onClick.AddListener(ConfirmCreateSlot);
            if (cancel != null) cancel.onClick.AddListener(() => nameDialog.SetActive(false));

            // Der Dialog kommt fertig aus der Szene — BuildNameDialog() läuft nie.
            // Die Tempo-Auswahl muss deshalb hier nachträglich hineingebaut werden.
            EnsurePaceSelector(dialog);
        }

        Transform settingsRoot = settingsPanel != null ? settingsPanel.transform.Find("Runtime Settings") : null;
        if (settingsRoot != null)
        {
            Transform selector = FindDeepChild(settingsRoot, "Display Selector");
            if (selector != null) displaySelectorImage = selector.GetComponent<Image>();
            Slider slider = settingsRoot.GetComponentInChildren<Slider>(true);
            if (slider != null)
            {
                float volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
                slider.SetValueWithoutNotify(volume);
                AudioListener.volume = volume;
                slider.onValueChanged.AddListener(SetVolume);
            }

            for (int i = 0; i < 3; i++)
            {
                Transform mode = FindDeepChild(settingsRoot, i == 0 ? "Mode Vollbild" : i == 1 ? "Mode Fenster" : "Mode Randlos");
                if (mode == null) continue;
                int capturedMode = i;
                displayModeHighlights[i] = mode.GetComponent<Image>();
                mode.GetComponent<Button>()?.onClick.AddListener(() => SetDisplayMode(capturedMode));
            }
            displayMode = Mathf.Clamp(PlayerPrefs.GetInt(DisplayModeKey, DetectDisplayMode()), 0, 2);
            ApplyDisplayMode(false);
        }

        for (int slot = 1; slot <= 3; slot++)
        {
            Transform slotTransform = slotSelectionPanel != null
                ? FindDeepChild(slotSelectionPanel.transform, $"Slot {slot} Button")
                : null;
            Button delete = slotTransform != null ? slotTransform.Find("Delete Slot Button")?.GetComponent<Button>() : null;
            Button action = slotTransform != null ? slotTransform.Find("Slot Action Background")?.GetComponent<Button>() : null;
            if (action != null)
            {
                int actionSlot = slot;
                action.onClick.AddListener(() => HandleSlotClicked(actionSlot));
            }
            if (delete == null) continue;
            int capturedSlot = slot;
            delete.onClick.AddListener(() => DeleteSlot(capturedSlot));
        }
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
        slotSelectionOpenedFrame = Time.frameCount;
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
        HandleSlotClicked(1);
    }

    public void PlaySlot2()
    {
        HandleSlotClicked(2);
    }

    public void PlaySlot3()
    {
        HandleSlotClicked(3);
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
        RefreshSceneSlotButtons();
        if (slotButtons == null) return;

        foreach (MainMenuSlotButton slotButton in slotButtons)
        {
            if (slotButton == null) continue;
            slotButton.Refresh();
        }
    }

    private void HandleSlotClicked(int slotIndex)
    {
        // Verhindert Click-through: Der Klick, der das Slot-Panel öffnet, darf nicht
        // im selben Frame einen darunterliegenden Laden/Erstellen-Button auslösen.
        if (Time.frameCount <= slotSelectionOpenedFrame)
            return;

        if (FarmSaveManager.Instance != null && FarmSaveManager.Instance.SaveExists(slotIndex))
        {
            PlaySlot(slotIndex);
            return;
        }

        pendingSlot = slotIndex;
        SelectPace(GamePace.Normal);
        if (nameInput != null)
        {
            nameInput.text = "";
            nameInput.Select();
            nameInput.ActivateInputField();
        }
        if (nameDialog != null) nameDialog.SetActive(true);
    }

    public void ConfirmCreateSlot()
    {
        if (FarmSaveManager.Instance == null || nameInput == null) return;
        string playerName = nameInput.text.Trim();
        if (playerName.Length == 0) return;

        if (FarmSaveManager.Instance.CreateSlot(pendingSlot, playerName, pendingPace))
        {
            nameDialog.SetActive(false);
            RefreshSlotButtons();
        }
    }

    private const string PaceRootName = "Pace Selector";

    /// <summary>
    /// Baut die Tempo-Auswahl in den vorhandenen Namens-Dialog ein.
    ///
    /// Der Dialog liegt fertig in der Szene und wird von <see cref="BindSceneUi"/> nur
    /// gesucht — <see cref="BuildNameDialog"/> läuft nie. Deshalb wird hier zur Laufzeit
    /// Platz geschaffen (Höhe 260 → 440) und die vorhandenen Knöpfe nach unten geschoben,
    /// statt die Auswahl in einem Dialog anzulegen, den nie jemand sieht.
    /// </summary>
    private void EnsurePaceSelector(Transform dialog)
    {
        if (dialog == null) return;

        // Schon gebaut (z.B. nach Szenen-Rückkehr) → nur neu verdrahten.
        Transform existing = dialog.Find(PaceRootName);
        if (existing != null)
        {
            SelectPace(pendingPace);
            return;
        }

        var dialogRect = (RectTransform)dialog;
        if (dialogRect.sizeDelta.y < 440f)
            dialogRect.sizeDelta = new Vector2(Mathf.Max(dialogRect.sizeDelta.x, 520f), 440f);

        // Bestätigen/Abbrechen an den neuen unteren Rand. Namen sind dieselben, die
        // BindSceneUi oben schon zum Verdrahten benutzt.
        MoveDialogChild(dialog, "Create", new Vector2(-95f, -185f));
        MoveDialogChild(dialog, "Cancel", new Vector2(95f, -185f));

        // So groß wie der Dialog: die Kinder sitzen dadurch in denselben Koordinaten wie
        // die übrigen Dialog-Elemente und man muss beim Nachjustieren nicht umrechnen.
        GameObject root = CreateUiObject(PaceRootName, dialog, Vector2.zero, dialogRect.sizeDelta);
        Transform parent = root.transform;

        CreateText("Spieltempo", parent, new Vector2(0f, -30f), new Vector2(460f, 34f), 20f);

        Button normal = CreateButton("Pace Normal", parent,
                                     new Vector2(-95f, -75f), new Vector2(170f, 44f), "NORMAL");
        normal.onClick.AddListener(() => SelectPace(GamePace.Normal));
        paceHighlights[(int)GamePace.Normal] = CreatePaceMarker(normal);

        Button relaxed = CreateButton("Pace Relaxed", parent,
                                      new Vector2(95f, -75f), new Vector2(170f, 44f), "ENTSPANNT");
        relaxed.onClick.AddListener(() => SelectPace(GamePace.Relaxed));
        paceHighlights[(int)GamePace.Relaxed] = CreatePaceMarker(relaxed);

        paceDescriptionText = CreateText(string.Empty, parent,
                                         new Vector2(0f, -130f), new Vector2(490f, 46f), 15f);

        SelectPace(GamePace.Normal);
    }

    private static void MoveDialogChild(Transform dialog, string childName, Vector2 position)
    {
        Transform child = FindDeepChild(dialog, childName);
        if (child is RectTransform rect)
            rect.anchoredPosition = position;
    }

    /// <summary>
    /// Markierungsleiste unter einem Tempo-Knopf.
    ///
    /// Eigenes Objekt statt den Knopf umzufärben: <c>ApplyStandardButtonStates</c> legt
    /// Farbübergänge auf die targetGraphic, die jede direkt gesetzte Farbe beim nächsten
    /// Hover wieder überschreiben würden.
    /// </summary>
    private static Image CreatePaceMarker(Button button)
    {
        GameObject go = CreateUiObject("Selection Marker", button.transform,
                                       new Vector2(0, -28), new Vector2(150, 6));
        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    /// <summary>Tempo-Auswahl im Anlege-Dialog. Nachträglich nicht mehr änderbar.</summary>
    private void SelectPace(GamePace pace)
    {
        pendingPace = pace;

        for (int i = 0; i < paceHighlights.Length; i++)
        {
            if (paceHighlights[i] == null) continue;
            bool active = (int)pace == i;
            paceHighlights[i].color = active
                ? new Color(0.42f, 0.62f, 0.24f, 1f)
                : new Color(0f, 0f, 0f, 0.12f);
        }

        if (paceDescriptionText != null)
            paceDescriptionText.text = pace.Description();
    }

    private void RefreshSceneSlotButtons()
    {
        if (slotSelectionPanel == null) return;

        for (int slot = 1; slot <= 3; slot++)
        {
            Transform slotTransform = FindDeepChild(slotSelectionPanel.transform, $"Slot {slot} Button");
            if (slotTransform == null) continue;
            Image slotImage = slotTransform.GetComponent<Image>();
            if (slotImage != null && slotCardSprite != null)
            {
                slotImage.sprite = slotCardSprite;
                slotImage.type = Image.Type.Simple;
                slotImage.preserveAspect = true;
                slotImage.color = Color.white;
            }
            TMP_Text label = slotTransform.Find("Slot Info Text")?.GetComponent<TMP_Text>();
            if (label == null)
            {
                foreach (TMP_Text candidate in slotTransform.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (candidate.name == "Slot Number" || candidate.name == "Slot Action Text") continue;
                    label = candidate;
                    break;
                }
            }
            if (label == null) continue;

            SaveGameData data = null;
            bool occupied = FarmSaveManager.Instance != null
                && FarmSaveManager.Instance.TryReadSlotData(slot, out data);

            if (occupied)
            {
                string playerName = string.IsNullOrWhiteSpace(data.playerName) ? $"Farm {slot}" : data.playerName;
                label.text = $"{playerName}\n\n     {data.money}";
            }
            else
            {
                label.text = "Leerer Slot";
            }

            ConfigureSlotDecorations(slotTransform, slot, occupied);
            ConfigureCoinIcon(slotTransform, occupied);
            ConfigureDeleteButton(slotTransform, slot, occupied);
        }
    }

    private void ConfigureSlotDecorations(Transform slotTransform, int slot, bool occupied)
    {
        Transform numberTransform = slotTransform.Find("Slot Number");
        TMP_Text numberText = numberTransform != null
            ? numberTransform.GetComponent<TMP_Text>()
            : CreateText(slot.ToString(), slotTransform, new Vector2(0, 99), new Vector2(52, 42), 27);
        numberText.gameObject.name = "Slot Number";
        numberText.text = slot.ToString();
        numberText.fontStyle = FontStyles.Bold;

        Transform actionTransform = slotTransform.Find("Slot Action Text");
        TMP_Text actionText = actionTransform != null
            ? actionTransform.GetComponent<TMP_Text>()
            : CreateText("", slotTransform, new Vector2(0, -83), new Vector2(132, 34), 18);
        actionText.gameObject.name = "Slot Action Text";
        actionText.text = occupied ? "Laden" : "Erstellen";
        // TMP can keep a white face color from its material even when the serialized
        // scene vertex color is brown. Set both runtime color layers explicitly.
        actionText.color = SlotButtonTextBrown;
        actionText.faceColor = SlotButtonTextBrown;
        actionText.fontStyle = FontStyles.Bold;
        actionText.raycastTarget = false;
    }

    private void ConfigureCoinIcon(Transform slotTransform, bool occupied)
    {
        Transform existing = slotTransform.Find("Money Icon");
        Image coinImage;
        if (existing == null)
        {
            GameObject coin = CreateUiObject("Money Icon", slotTransform, new Vector2(-35, -11), new Vector2(25, 25));
            coinImage = coin.AddComponent<Image>();
            coinImage.sprite = coinSprite;
            coinImage.preserveAspect = true;
            coinImage.raycastTarget = false;
        }
        else
        {
            coinImage = existing.GetComponent<Image>();
        }

        if (coinImage != null)
            coinImage.gameObject.SetActive(occupied && coinSprite != null);
    }

    private void ConfigureDeleteButton(Transform slotTransform, int slot, bool occupied)
    {
        Transform existing = slotTransform.Find("Delete Slot Button");
        Button deleteButton;

        if (existing == null)
        {
            deleteButton = CreateButton("Delete Slot Button", slotTransform, new Vector2(67, -91), new Vector2(38, 38), "");
            Image image = deleteButton.GetComponent<Image>();
            if (deleteButtonSprite != null)
            {
                image.sprite = deleteButtonSprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = Color.white;
            }
            ColorBlock colors = deleteButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.82f, 0.45f, 1f);
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.colorMultiplier = 1.25f;
            colors.fadeDuration = 0.08f;
            deleteButton.colors = colors;
            deleteButton.transition = Selectable.Transition.ColorTint;
            int capturedSlot = slot;
            deleteButton.onClick.AddListener(() => DeleteSlot(capturedSlot));
        }
        else
        {
            deleteButton = existing.GetComponent<Button>();
        }

        if (deleteButton != null)
            deleteButton.gameObject.SetActive(occupied);
    }

    private void BuildSlotSelectionLayout()
    {
        if (slotSelectionPanel == null || slotSelectionPanel.transform.Find("Slot Selection Backdrop") != null)
            return;

        GameObject backdrop = CreateUiObject("Slot Selection Backdrop", slotSelectionPanel.transform,
            new Vector2(0, -5), new Vector2(700, 405));
        AddParchment(backdrop);
        backdrop.transform.SetAsFirstSibling();

        GameObject title = CreateUiObject("Slot Selection Title", backdrop.transform,
            new Vector2(0, 180), new Vector2(350, 62));
        Image titleImage = title.AddComponent<Image>();
        titleImage.sprite = normalButtonSprite;
        titleImage.type = Image.Type.Sliced;
        titleImage.color = new Color(0.55f, 0.3f, 0.12f, 1f);
        TMP_Text titleText = CreateText("Spielstand auswählen", title.transform,
            new Vector2(0, 3), new Vector2(320, 50), 27);
        titleText.color = Color.white;
        titleText.fontStyle = FontStyles.Bold;

        Transform back = FindDeepChild(slotSelectionPanel.transform, "Back Button");
        if (back != null)
        {
            RectTransform rect = back.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0, -225);
            rect.sizeDelta = new Vector2(180, 58);
        }
    }

    private void BuildSettingsControls()
    {
        if (settingsPanel == null || settingsPanel.transform.Find("Runtime Settings") != null) return;
        GameObject root = CreateUiObject("Runtime Settings", settingsPanel.transform, Vector2.zero, new Vector2(600, 385));
        AddParchment(root);

        CreateHeader("Einstellungen", root.transform, new Vector2(0, 175), new Vector2(290, 62));
        CreateText("Lautstärke", root.transform, new Vector2(0, 78), new Vector2(400, 36), 23);
        Slider slider = CreateSlider(root.transform, new Vector2(0, 38));
        float volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        slider.value = volume;
        AudioListener.volume = volume;
        slider.onValueChanged.AddListener(SetVolume);

        CreateText("Anzeigemodus", root.transform, new Vector2(0, -18), new Vector2(400, 36), 23);
        displayMode = Mathf.Clamp(PlayerPrefs.GetInt(DisplayModeKey, DetectDisplayMode()), 0, 2);
        CreateDisplaySelector(root.transform, new Vector2(0, -73));
        ApplyDisplayMode(false);

        Transform settingsBack = FindDeepChild(settingsPanel.transform, "Back Button");
        if (settingsBack != null)
        {
            RectTransform rect = settingsBack.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0, -225);
            rect.sizeDelta = new Vector2(180, 58);
        }
    }

    private void SetVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(VolumeKey, value);
        PlayerPrefs.Save();
    }

    private int DetectDisplayMode()
    {
        if (Screen.fullScreenMode == FullScreenMode.Windowed) return 1;
        if (Screen.fullScreenMode == FullScreenMode.FullScreenWindow) return 2;
        return 0;
    }

    private void SetDisplayMode(int mode)
    {
        displayMode = Mathf.Clamp(mode, 0, 2);
        ApplyDisplayMode(true);
    }

    private void ApplyDisplayMode(bool persist)
    {
        FullScreenMode[] modes = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed, FullScreenMode.FullScreenWindow };
        Screen.fullScreenMode = modes[displayMode];
        for (int i = 0; i < displayModeHighlights.Length; i++)
        {
            if (displayModeHighlights[i] != null)
                displayModeHighlights[i].color = Color.clear;
        }
        if (displaySelectorImage != null)
        {
            Sprite[] selectorSprites = { displayFullscreenSprite, displayWindowedSprite, displayBorderlessSprite };
            if (selectorSprites[displayMode] != null)
                displaySelectorImage.sprite = selectorSprites[displayMode];
        }
        if (!persist) return;
        PlayerPrefs.SetInt(DisplayModeKey, displayMode);
        PlayerPrefs.Save();
    }

    private void BuildNameDialog()
    {
        if (slotSelectionPanel == null) return;
        nameDialog = CreateUiObject("Name Dialog", slotSelectionPanel.transform, Vector2.zero, new Vector2(520, 260));
        AddParchment(nameDialog);
        CreateHeader("Neue Farm", nameDialog.transform, new Vector2(0, 125), new Vector2(245, 58));
        CreateText("Wie soll deine Farm heißen?", nameDialog.transform, new Vector2(0, 75), new Vector2(460, 38), 21);
        nameInput = CreateInputField(nameDialog.transform, new Vector2(0, 20));
        Button create = CreateButton("Create", nameDialog.transform, new Vector2(-95, -75), new Vector2(170, 55), "ERSTELLEN");
        create.onClick.AddListener(ConfirmCreateSlot);
        Button cancel = CreateButton("Cancel", nameDialog.transform, new Vector2(95, -75), new Vector2(170, 55), "ABBRECHEN");
        cancel.onClick.AddListener(() => nameDialog.SetActive(false));

        // Gleicher Weg wie beim Szenen-Dialog: eine Stelle, an der die Tempo-Auswahl
        // entsteht. Sonst laufen die beiden Aufbauten früher oder später auseinander.
        EnsurePaceSelector(nameDialog.transform);

        nameDialog.SetActive(false);
    }

    private static GameObject CreateUiObject(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return go;
    }

    private static TMP_Text CreateText(string value, Transform parent, Vector2 position, Vector2 size, float fontSize)
    {
        GameObject go = CreateUiObject("Text", parent, position, size);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = TextBrown;
        return text;
    }

    private Button CreateButton(string name, Transform parent, Vector2 position, Vector2 size, string label)
    {
        GameObject go = CreateUiObject(name, parent, position, size);
        Image image = go.AddComponent<Image>();
        image.sprite = normalButtonSprite;
        image.type = normalButtonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = normalButtonSprite != null ? Color.white : new Color(0.35f, 0.55f, 0.28f, 1f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ApplyStandardButtonStates(button);
        TMP_Text buttonText = CreateText(label, go.transform, new Vector2(0, 3), size, 22);
        buttonText.fontStyle = FontStyles.Bold;
        return button;
    }

    private Slider CreateSlider(Transform parent, Vector2 position)
    {
        GameObject go = CreateUiObject("Volume Slider", parent, position, new Vector2(350, 38));
        Slider slider = go.AddComponent<Slider>();
        GameObject background = CreateUiObject("Background", go.transform, Vector2.zero, new Vector2(330, 41));
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.sprite = sliderBackgroundSprite;
        backgroundImage.preserveAspect = true;
        backgroundImage.color = Color.white;
        Mask backgroundMask = background.AddComponent<Mask>();
        backgroundMask.showMaskGraphic = true;
        GameObject fillArea = CreateUiObject("Fill Area", background.transform, Vector2.zero, Vector2.zero);
        SetStretch(fillArea.GetComponent<RectTransform>(), 8, 8, 11, 11);
        GameObject fill = CreateUiObject("Fill", fillArea.transform, Vector2.zero, Vector2.zero);
        SetStretch(fill.GetComponent<RectTransform>(), 0, 0, 0, 0);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.sprite = sliderFillSprite;
        fillImage.type = sliderFillSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        fillImage.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        GameObject handleArea = CreateUiObject("Handle Slide Area", go.transform, Vector2.zero, Vector2.zero);
        SetStretch(handleArea.GetComponent<RectTransform>(), 15, 15, 0, 0);
        GameObject handle = CreateUiObject("Handle", handleArea.transform, Vector2.zero, new Vector2(28, 36));
        Image handleImage = handle.AddComponent<Image>();
        handleImage.sprite = sliderHandleSprite;
        handleImage.color = Color.white;
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        return slider;
    }

    private void CreateDisplaySelector(Transform parent, Vector2 position)
    {
        GameObject selector = CreateUiObject("Display Selector", parent, position, new Vector2(350, 82));
        Image selectorImage = selector.AddComponent<Image>();
        selectorImage.sprite = displaySelectorSprite;
        selectorImage.preserveAspect = true;
        selectorImage.raycastTarget = false;

        string[] labels = { "Vollbild", "Fenster", "Randlos" };
        for (int i = 0; i < 3; i++)
        {
            int modeIndex = i;
            GameObject segment = CreateUiObject($"Mode {labels[i]}", selector.transform,
                new Vector2((i - 1) * 113f, 0), new Vector2(108, 58));
            Image highlight = segment.AddComponent<Image>();
            highlight.color = Color.clear;
            displayModeHighlights[i] = highlight;
            Button button = segment.AddComponent<Button>();
            button.targetGraphic = highlight;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => SetDisplayMode(modeIndex));
            TMP_Text text = CreateText(labels[i], segment.transform, new Vector2(0, 3), new Vector2(104, 48), 18);
            text.fontStyle = FontStyles.Bold;
        }
    }

    private static void SetStretch(RectTransform rect, float left, float right, float bottom, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private TMP_InputField CreateInputField(Transform parent, Vector2 position)
    {
        GameObject go = CreateUiObject("Name Input", parent, position, new Vector2(400, 55));
        Image image = go.AddComponent<Image>();
        image.sprite = inputFieldSprite;
        image.type = inputFieldSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
        TMP_InputField input = go.AddComponent<TMP_InputField>();
        TMP_Text text = CreateText("", go.transform, Vector2.zero, new Vector2(370, 50), 25);
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.Left;
        TMP_Text placeholder = CreateText("Name eingeben ...", go.transform, Vector2.zero, new Vector2(370, 50), 23);
        placeholder.color = new Color(0.35f, 0.35f, 0.35f, 0.7f);
        placeholder.alignment = TextAlignmentOptions.Left;
        input.textComponent = (TextMeshProUGUI)text;
        input.placeholder = (TextMeshProUGUI)placeholder;
        input.characterLimit = 24;
        input.onSubmit.AddListener(_ => FindObjectOfType<MainMenuController>()?.ConfirmCreateSlot());
        return input;
    }

    private void AddParchment(GameObject target)
    {
        Image image = target.AddComponent<Image>();
        image.sprite = parchmentSprite;
        image.type = parchmentSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = parchmentSprite != null ? Color.white : new Color(0.95f, 0.82f, 0.55f, 0.98f);
    }

    private void CreateHeader(string label, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject header = CreateUiObject(label + " Header", parent, position, size);
        Image image = header.AddComponent<Image>();
        image.sprite = normalButtonSprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(0.55f, 0.3f, 0.12f, 1f);
        TMP_Text text = CreateText(label, header.transform, new Vector2(0, 3), size - new Vector2(20, 10), 27);
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
    }

    private void StyleExistingMenu()
    {
        if (normalButtonSprite != null)
        {
            GameObject[] panels = { mainPanel, slotSelectionPanel, settingsPanel };
            Transform runtimeSettings = settingsPanel != null ? settingsPanel.transform.Find("Runtime Settings") : null;
            foreach (GameObject panel in panels)
            {
                if (panel == null) continue;
                foreach (Button button in panel.GetComponentsInChildren<Button>(true))
                {
                    // Slot-Karten und Runtime-Controls besitzen bereits ihr eigenes Styling.
                    if (button.name.StartsWith("Slot ") || (runtimeSettings != null && button.transform.IsChildOf(runtimeSettings)))
                        continue;
                    Image image = button.GetComponent<Image>();
                    if (image != null)
                    {
                        image.sprite = normalButtonSprite;
                        image.type = Image.Type.Sliced;
                        image.color = Color.white;
                        ApplyStandardButtonStates(button);
                    }
                }
            }
        }

        foreach (Button button in FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text == null) continue;
            text.color = TextBrown;
            text.fontStyle = FontStyles.Bold;
            RectTransform textRect = text.rectTransform;
            if (!button.name.StartsWith("Slot "))
            {
                textRect.anchoredPosition = new Vector2(textRect.anchoredPosition.x, 3f);
            }
        }
    }

    private void ApplyStandardButtonStates(Button button)
    {
        if (button == null || highlightedButtonSprite == null) return;
        SpriteState state = button.spriteState;
        state.highlightedSprite = highlightedButtonSprite;
        state.selectedSprite = highlightedButtonSprite;
        state.pressedSprite = highlightedButtonSprite;
        button.spriteState = state;
        button.transition = Selectable.Transition.SpriteSwap;
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform result = FindDeepChild(child, childName);
            if (result != null) return result;
        }
        return null;
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }
}

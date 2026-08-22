using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HotbarUI : MonoBehaviour
{
    public static HotbarUI Instance { get; private set; }

    [Header("Setup")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform container;

    [Header("Startslots")]
    [SerializeField] private HotbarSlotConfig[] defaultSlots = new HotbarSlotConfig[]
    {
        new() { toolType = ToolType.Hoe },
        new() { toolType = ToolType.WateringCan },
        new() { toolType = ToolType.Scythe },
        new() { toolType = ToolType.Seed, showCount = true, hasDropdown = true }, // immer letzter Slot
    };

    [Header("Seed-Slot")]
    [SerializeField] private Sprite emptySeedSprite;

    [Tooltip("Zeichen fürs Badge oben rechts auf dem Seed-Slot, solange KEINE Sorte gewählt " +
             "ist (nicht bei 0 Bestand einer bereits gewählten Sorte — das bleibt wie bisher " +
             "stumm). Leer = kein Badge, alles andere funktioniert trotzdem normal.\n\n" +
             "TMP-Text statt Sprite/Image: Textfarbe ist eine echte Füllung, kein Multiply-" +
             "Tint über eine Bild-Textur — needsSeedIndicatorColor kommt damit IMMER exakt so " +
             "an wie eingestellt, unabhängig vom Ausgangsmaterial (bei Sprites wie Unitys " +
             "eingebautem DropdownArrow ist das nicht garantiert, die sind oft dunkelgrau " +
             "statt weiß und lassen sich kaum hell tinten).")]
    [SerializeField] private string needsSeedIndicatorCharacter = "^";

    [Tooltip("Z-Rotation in Grad, nur fürs Badge. \"^\" zeigt bei 0° schon nach oben, andere " +
             "Zeichen ggf. anders ausrichten.")]
    [SerializeField] private float needsSeedIndicatorRotationZ = 0f;

    [Tooltip("Größe des Badges in UI-Einheiten (bestimmt auch die Schriftgröße).")]
    [SerializeField] private Vector2 needsSeedIndicatorSize = new(36f, 36f);

    [Tooltip("Versatz von der oberen rechten Ecke des Slots (negative Werte = nach innen). " +
             "Bei Bedarf anpassen, falls es über den Slot-Rand hinausragt oder zu weit " +
             "reingerückt wirkt.")]
    [SerializeField] private Vector2 needsSeedIndicatorOffset = new(-6f, -6f);

    [Tooltip("Farbe des Zeichens selbst — echte Füllfarbe, kommt 1:1 an.")]
    [SerializeField] private Color needsSeedIndicatorColor = new(1f, 0.75f, 0.2f, 1f);

    [Header("Scene Hotbar Sprites")]
    [SerializeField] private Sprite normalSlotSprite;
    [SerializeField] private Sprite highlightedSlotSprite;
    [SerializeField] private Sprite pressedSlotSprite;
    [SerializeField] private Sprite disabledSlotSprite;
    [SerializeField] private Sprite hoeIconSprite;
    [SerializeField] private Sprite wateringCanIconSprite;
    [SerializeField] private Sprite scytheIconSprite;
    [SerializeField] private Sprite seedIconSprite;
    [SerializeField] private Sprite[] shortcutSprites;
    [SerializeField] private Sprite carrotSeedSprite;
    [SerializeField] private Sprite cauliflowerSeedSprite;
    [SerializeField] private Sprite sunflowerSeedSprite;
    [SerializeField] private Sprite hammerIconSprite;
    [SerializeField] private Sprite grassTileIconSprite;
    [SerializeField] private Sprite pathTileIconSprite;
    [SerializeField] private Sprite farmTileIconSprite;

    [Header("Farben")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color activeColor  = new Color(0.75f, 0.55f, 0.1f, 1f);

    private readonly List<HotbarSlotConfig> slotConfigs = new();
    private readonly List<HotbarSlotUI> slotInstances = new();
    private readonly List<HotbarSlotUI> buildSlotInstances = new();
    private readonly TileType[] buildTileTypes = { TileType.FarmPlot, TileType.Path, TileType.Grass };
    private GameObject buildToggleSlot;
    private HotbarSlotUI buildToggleSlotUI;

    /// <summary>Badge oben rechts auf dem Seed-Slot — zeigt "hier musst du noch was
    /// auswählen", solange Hotbar.SelectedSeed null ist. Nur für den einen Slot gebraucht,
    /// deshalb hier statt in HotbarSlotUI (das teilen sich auch die Build-Slots). TMP-Text
    /// statt Sprite/Image, damit needsSeedIndicatorColor eine echte Füllfarbe ist statt ein
    /// Multiply-Tint über eine Bild-Textur.</summary>
    private TMPro.TextMeshProUGUI seedNeedsSelectionBadge;

    void Awake() => Instance = this;

    void Start()
    {
        foreach (var config in defaultSlots)
            SpawnSlot(config);

        SyncOwnedToolsToHotbar();
        SpawnBuildToggleSlot();
        SpawnBuildSlots();

        if (ToolRegistry.Instance != null)
            ToolRegistry.Instance.OnOwnedToolsChanged += SyncOwnedToolsToHotbar;

        Hotbar.Instance.OnToolChanged += OnToolChanged;
        Hotbar.Instance.OnSeedChanged += _ => UpdateSeedSlot();
        PlayerInventory.Instance.OnSeedsChanged += (_, _) => UpdateSeedSlot();
        PlayerInventory.Instance.OnFertilizerChanged += _ => UpdateFertilizerSlot();
        BuildModeManager.Instance.OnBuildModeChanged += OnBuildModeChanged;
        BuildModeManager.Instance.OnSelectedTileChanged += UpdateBuildHighlight;

        UpdateHighlight(Hotbar.Instance.ActiveTool);
        UpdateSeedSlot();
        UpdateFertilizerSlot();
    }

    void OnDestroy()
    {
        if (BuildModeManager.Instance != null)
        {
            BuildModeManager.Instance.OnBuildModeChanged -= OnBuildModeChanged;
            BuildModeManager.Instance.OnSelectedTileChanged -= UpdateBuildHighlight;
        }
        if (Hotbar.Instance != null)
            Hotbar.Instance.OnToolChanged -= OnToolChanged;
        if (ToolRegistry.Instance != null)
            ToolRegistry.Instance.OnOwnedToolsChanged -= SyncOwnedToolsToHotbar;
    }

    void Update()
    {
        if (BuildModeManager.Instance != null && BuildModeManager.Instance.IsActive)
        {
            HandleBuildHotkeys();
            return;
        }

        // Dropdown schluckt alle Inputs (auch wenn es gerade in diesem Frame geschlossen wurde)
        if (SeedDropdownUI.Instance != null &&
            (SeedDropdownUI.Instance.IsOpen || SeedDropdownUI.Instance.ConsumedInputThisFrame)) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // 1–8 → Slot auswählen
        for (int i = 0; i < slotInstances.Count && i < 8; i++)
        {
            if (GetDigitKey(keyboard, i + 1).wasPressedThisFrame)
            {
                SelectSlot(i);
                return;
            }
        }

        // 0 / Tab → Tool ablegen
        if (keyboard.digit0Key.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame)
            Hotbar.Instance.SetTool(ToolType.None);

        // Leertaste ODER Enter → Dropdown öffnen wenn Seed-Slot aktiv
        bool wantsSeedDropdown = keyboard.spaceKey.wasPressedThisFrame
            || keyboard.enterKey.wasPressedThisFrame
            || keyboard.numpadEnterKey.wasPressedThisFrame;

        if (wantsSeedDropdown && Hotbar.Instance.ActiveTool == ToolType.Seed)
            SeedDropdownUI.Instance?.Toggle();
    }

    /// <summary>Gibt zurück ob ein Tool bereits in der Hotbar ist.</summary>
    public bool HasTool(ToolType tool) => slotConfigs.Exists(c => c.toolType == tool);

    /// <summary>Fügt alle owned Tools aus ToolRegistry zur Hotbar hinzu die noch fehlen.</summary>
    private void SyncOwnedToolsToHotbar()
    {
        if (ToolRegistry.Instance == null) return;
        ToolType[] allTools = { ToolType.Hoe, ToolType.WateringCan, ToolType.Scythe, ToolType.Seed, ToolType.Fertilize };
        foreach (var tool in allTools)
        {
            if (ToolRegistry.Instance.IsOwned(tool) && !HasTool(tool))
            {
                SpawnSlot(new HotbarSlotConfig
                {
                    toolType = tool,
                    showCount = tool is ToolType.Seed or ToolType.Fertilize,
                    hasDropdown = tool == ToolType.Seed
                });
            }
        }

        UpdateFertilizerSlot();
    }

    /// <summary>Vom Shop aufgerufen wenn ein neues Tool freigeschaltet wird.</summary>
    public void UnlockSlot(HotbarSlotConfig config)
    {
        if (slotInstances.Count >= 8)
        {
            Debug.LogWarning("Hotbar ist voll — max. 8 Slots.");
            return;
        }

        // Seed-Slot (hasDropdown) bleibt immer am Ende
        int seedIndex = slotConfigs.FindIndex(c => c.hasDropdown);
        if (seedIndex >= 0 && seedIndex == slotConfigs.Count - 1)
            InsertSlotBefore(config, seedIndex);
        else
            SpawnSlot(config);
    }

    private void InsertSlotBefore(HotbarSlotConfig config, int index)
    {
        SpawnSlot(config); // ans Ende spawnen

        // In der Hierarchy vor den Seed-Slot schieben
        var newSlotUI = slotInstances[^1];
        newSlotUI.transform.SetSiblingIndex(index);

        // Interne Listen umordnen
        var newConfig = slotConfigs[^1];
        slotConfigs.RemoveAt(slotConfigs.Count - 1);
        slotConfigs.Insert(index, newConfig);

        slotInstances.RemoveAt(slotInstances.Count - 1);
        slotInstances.Insert(index, newSlotUI);

    }

    /// <summary>
    /// Zentrale Stelle für Slotwahl per Linksklick ODER Zifferntaste — beide landen hier,
    /// darum reicht eine Regel für beide Eingabewege.
    ///
    /// Für den Seed-Slot gilt zusätzlich: ein simples "Tool aktivieren" ist NICHT immer die
    /// richtige Reaktion. Zwei Fälle brauchen stattdessen das Dropdown:
    ///  • Noch gar keine Sorte gewählt — sonst aktiviert man ein Werkzeug, das im selben
    ///    Moment nichts tun kann, ohne zu wissen warum.
    ///  • Der Slot ist SCHON aktiv und man klickt/drückt ihn nochmal — ein Reselect auf
    ///    denselben Zustand wäre ein No-Op, das Dropdown zu öffnen ist die einzig sinnvolle
    ///    Reaktion auf "ich will hier nochmal was tun".
    /// </summary>
    private void SelectSlot(int index)
    {
        if (index < 0 || index >= slotConfigs.Count) return;

        var config = slotConfigs[index];
        bool alreadyActive = Hotbar.Instance.ActiveTool == config.toolType;

        if (config.hasDropdown && (alreadyActive || Hotbar.Instance.SelectedSeed == null))
        {
            Hotbar.Instance.SetTool(config.toolType);
            SeedDropdownUI.Instance?.Toggle();
            return;
        }

        Hotbar.Instance.SetTool(config.toolType);
    }

    private void SpawnSlot(HotbarSlotConfig config)
    {
        var go = Instantiate(slotPrefab, container);
        var slotUI = go.GetComponent<HotbarSlotUI>();

        int keyIndex = slotInstances.Count;
        slotUI.background.color = Color.white;
        if (normalSlotSprite != null)
            slotUI.background.sprite = normalSlotSprite;

        var slotIcon = GetSceneToolIcon(config.toolType)
            ?? ToolRegistry.Instance?.GetData(config.toolType)?.icon;
        if (slotUI.icon != null)
        {
            if (slotIcon != null)
                slotUI.icon.sprite = slotIcon;
            slotUI.icon.color = slotIcon != null ? Color.white : Color.clear;
        }

        if (slotUI.countRoot != null)
            slotUI.countRoot.SetActive(config.showCount);
        else if (slotUI.countLabel != null)
            slotUI.countLabel.transform.parent.gameObject.SetActive(config.showCount);

        if (slotUI.countLabel != null)
            slotUI.countLabel.text = "";

        var button = go.GetComponent<Button>();
        ApplyButtonSpriteStates(button);
        if (slotUI.shortcutBadge != null)
        {
            bool hasShortcut = shortcutSprites != null
                && keyIndex >= 0
                && keyIndex < shortcutSprites.Length
                && shortcutSprites[keyIndex] != null;
            slotUI.shortcutBadge.gameObject.SetActive(hasShortcut);
            if (hasShortcut)
                slotUI.shortcutBadge.sprite = shortcutSprites[keyIndex];
        }
        int captured = keyIndex;
        button.onClick.AddListener(() => SelectSlot(captured));

        if (config.hasDropdown)
        {
            AddRightClickHandler(go, () => SeedDropdownUI.Instance.Toggle());
            CreateSeedNeedsSelectionBadge(go.transform);
        }

        slotConfigs.Add(config);
        slotInstances.Add(slotUI);
    }

    /// <summary>Oben rechts auf dem Seed-Slot — die anderen beiden Ecken sind schon belegt
    /// (Shortcut-Badge oben links, Mengen-Badge unten rechts, siehe HotbarSlot.prefab).
    /// Rein prozedural statt im Prefab verankert, weil nur dieser eine Slot es braucht.
    ///
    /// TMP-Text statt Sprite/Image: Textfarbe ist eine echte Füllung, kein Multiply-Tint über
    /// eine Bild-Textur — die eingestellte Farbe kommt damit garantiert 1:1 an, unabhängig
    /// davon wie hell/dunkel ein Sprite von Haus aus wäre.</summary>
    private void CreateSeedNeedsSelectionBadge(Transform parent)
    {
        GameObject go = new("NeedsSelectionBadge", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = needsSeedIndicatorOffset;
        rect.sizeDelta = needsSeedIndicatorSize;
        rect.localEulerAngles = new Vector3(0f, 0f, needsSeedIndicatorRotationZ);

        seedNeedsSelectionBadge = go.AddComponent<TMPro.TextMeshProUGUI>();
        seedNeedsSelectionBadge.fontStyle = TMPro.FontStyles.Bold;
        seedNeedsSelectionBadge.alignment = TMPro.TextAlignmentOptions.Center;
        seedNeedsSelectionBadge.raycastTarget = false;

        go.SetActive(false); // UpdateSeedSlot() entscheidet den Startzustand
    }

    private void OnBuildModeChanged(bool buildModeActive)
    {
        foreach (var slot in slotInstances)
            slot.gameObject.SetActive(!buildModeActive);

        foreach (var slot in buildSlotInstances)
            slot.gameObject.SetActive(buildModeActive);

        if (buildToggleSlot != null)
        {
            Sprite toggleIcon = buildModeActive ? hoeIconSprite : hammerIconSprite;
            if (buildToggleSlotUI != null && buildToggleSlotUI.icon != null)
            {
                buildToggleSlotUI.icon.sprite = toggleIcon;
                buildToggleSlotUI.icon.color = toggleIcon != null ? Color.white : Color.clear;
            }
        }

        if (buildModeActive)
        {
            SeedDropdownUI.Instance?.Close();
            Hotbar.Instance.SetTool(ToolType.None);
        }

        UpdateBuildHighlight(BuildModeManager.Instance.SelectedTileType);
    }

    private void SpawnBuildToggleSlot()
    {
        Transform fixedUiParent = container.parent != null ? container.parent : container;
        buildToggleSlot = Instantiate(slotPrefab, fixedUiParent);
        buildToggleSlot.name = "BuildModeButton";

        buildToggleSlotUI = buildToggleSlot.GetComponent<HotbarSlotUI>();
        ConfigureVisualSlot(buildToggleSlotUI, hammerIconSprite, false);

        RectTransform rect = buildToggleSlot.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 24f);
            rect.sizeDelta = new Vector2(86f, 86f);
        }

        Button button = buildToggleSlot.GetComponent<Button>();
        ApplyButtonSpriteStates(button);
        button.onClick.AddListener(() => BuildModeManager.Instance?.Toggle());
    }

    private void SpawnBuildSlots()
    {
        for (int i = 0; i < buildTileTypes.Length; i++)
        {
            GameObject go = Instantiate(slotPrefab, container);
            go.name = $"BuildSlot_{buildTileTypes[i]}";

            HotbarSlotUI slotUI = go.GetComponent<HotbarSlotUI>();
            ConfigureVisualSlot(slotUI, GetBuildTileIcon(buildTileTypes[i]), false);
            ConfigureShortcutBadge(slotUI, i);

            TileType capturedType = buildTileTypes[i];
            Button button = go.GetComponent<Button>();
            ApplyButtonSpriteStates(button);
            button.onClick.AddListener(() => BuildModeManager.Instance?.SelectTile(capturedType));

            go.SetActive(false);
            buildSlotInstances.Add(slotUI);
        }
    }

    private Sprite GetBuildTileIcon(TileType type) => type switch
    {
        TileType.Grass => grassTileIconSprite,
        TileType.Path => pathTileIconSprite,
        TileType.FarmPlot => farmTileIconSprite,
        _ => null
    };

    private void ConfigureShortcutBadge(HotbarSlotUI slotUI, int shortcutIndex)
    {
        if (slotUI == null || slotUI.shortcutBadge == null)
            return;

        bool hasSprite = shortcutSprites != null
            && shortcutIndex >= 0
            && shortcutIndex < shortcutSprites.Length
            && shortcutSprites[shortcutIndex] != null;

        slotUI.shortcutBadge.gameObject.SetActive(hasSprite);
        if (hasSprite)
            slotUI.shortcutBadge.sprite = shortcutSprites[shortcutIndex];
    }

    private void ConfigureVisualSlot(HotbarSlotUI slotUI, Sprite icon, bool showCount)
    {
        if (slotUI == null) return;

        if (slotUI.background != null)
        {
            slotUI.background.sprite = normalSlotSprite;
            slotUI.background.color = Color.white;
        }

        if (slotUI.icon != null)
        {
            slotUI.icon.sprite = icon;
            slotUI.icon.color = icon != null ? Color.white : Color.clear;
            slotUI.icon.preserveAspect = true;
        }

        if (slotUI.shortcutBadge != null)
            slotUI.shortcutBadge.gameObject.SetActive(false);
        if (slotUI.countRoot != null)
            slotUI.countRoot.SetActive(showCount);
    }

    private void HandleBuildHotkeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || BuildModeManager.Instance == null) return;

        for (int i = 0; i < buildTileTypes.Length; i++)
        {
            if (GetDigitKey(keyboard, i + 1).wasPressedThisFrame)
            {
                BuildModeManager.Instance.SelectTile(buildTileTypes[i]);
                return;
            }
        }
    }

    private void UpdateBuildHighlight(TileType selectedType)
    {
        for (int i = 0; i < buildSlotInstances.Count; i++)
        {
            Image background = buildSlotInstances[i].background;
            if (background == null) continue;

            background.sprite = buildTileTypes[i] == selectedType && highlightedSlotSprite != null
                ? highlightedSlotSprite
                : normalSlotSprite;
            background.color = Color.white;
        }
    }

    private void OnToolChanged(ToolType tool) => UpdateHighlight(tool);

    private void UpdateHighlight(ToolType tool)
    {
        for (int i = 0; i < slotInstances.Count; i++)
        {
            bool selected = slotConfigs[i].toolType == tool;
            Image background = slotInstances[i].background;

            if (normalSlotSprite != null && highlightedSlotSprite != null)
            {
                background.sprite = selected ? highlightedSlotSprite : normalSlotSprite;
                background.color = Color.white;
            }
            else
            {
                background.color = selected ? activeColor : normalColor;
            }
        }
    }

    private Sprite GetSceneToolIcon(ToolType tool) => tool switch
    {
        ToolType.Hoe => hoeIconSprite,
        ToolType.WateringCan => wateringCanIconSprite,
        ToolType.Scythe => scytheIconSprite,
        ToolType.Seed => seedIconSprite,
        _ => null
    };

    private Sprite GetSeedUiSprite(PlantType seed)
    {
        if (seed == null) return null;
        return seed.plantName switch
        {
            "Carrot" => carrotSeedSprite,
            "Cauliflower" => cauliflowerSeedSprite,
            "Sunflower" => sunflowerSeedSprite,
            _ => null
        };
    }

    private void ApplyButtonSpriteStates(Button button)
    {
        if (button == null || highlightedSlotSprite == null) return;

        SpriteState states = button.spriteState;
        states.highlightedSprite = highlightedSlotSprite;
        states.selectedSprite = highlightedSlotSprite;
        states.pressedSprite = pressedSlotSprite != null ? pressedSlotSprite : highlightedSlotSprite;
        states.disabledSprite = disabledSlotSprite;
        button.spriteState = states;
        button.transition = Selectable.Transition.SpriteSwap;
    }

    private void UpdateSeedSlot()
    {
        int seedIndex = slotConfigs.FindIndex(c => c.hasDropdown);
        if (seedIndex < 0) return;

        var slotUI = slotInstances[seedIndex];
        var selected = Hotbar.Instance.SelectedSeed;

        // Icon updaten
        if (slotUI.icon != null)
        {
            Sprite specificSeedSprite = GetSeedUiSprite(selected);
            slotUI.icon.sprite = selected != null
                ? specificSeedSprite ?? selected.icon ?? emptySeedSprite
                : emptySeedSprite;
            slotUI.icon.color = Color.white;
            slotUI.icon.rectTransform.localScale = specificSeedSprite != null
                ? new Vector3(1f, 0.9f, 1f)
                : Vector3.one;
        }

        int seedCount = selected != null && PlayerInventory.Instance != null
            ? PlayerInventory.Instance.GetSeedCount(selected)
            : 0;

        UpdateSeedCountBadge(slotUI, selected != null, seedCount);

        // "Hier fehlt noch eine Entscheidung" — nur wenn wirklich NICHTS gewählt ist.
        // 0 Bestand einer bereits gewählten Sorte bleibt bewusst stumm wie bisher.
        if (seedNeedsSelectionBadge != null)
        {
            // Alle Optik-Werte hier NOCHMAL setzen, nicht nur beim Erstellen des Slots: sonst
            // zeigt eine Inspector-Änderung im Play Mode erst nach einem Neustart Wirkung,
            // weil CreateSeedNeedsSelectionBadge() nur einmal beim Slot-Aufbau lief.
            seedNeedsSelectionBadge.text = needsSeedIndicatorCharacter;
            seedNeedsSelectionBadge.color = needsSeedIndicatorColor;
            seedNeedsSelectionBadge.rectTransform.sizeDelta = needsSeedIndicatorSize;
            seedNeedsSelectionBadge.rectTransform.anchoredPosition = needsSeedIndicatorOffset;
            seedNeedsSelectionBadge.rectTransform.localEulerAngles = new Vector3(0f, 0f, needsSeedIndicatorRotationZ);
            // Schriftgröße an die Box koppeln statt eigenem Feld — bei einem einzelnen
            // Zeichen ist "Boxhöhe = Zeichengröße" genau das erwartete Verhalten.
            seedNeedsSelectionBadge.fontSize = needsSeedIndicatorSize.y * 0.8f;

            bool needsSelection = selected == null && !string.IsNullOrEmpty(needsSeedIndicatorCharacter);
            seedNeedsSelectionBadge.gameObject.SetActive(needsSelection);
        }
    }

    /// <summary>Zeigt den Dünger-Bestand als Zahlen-Badge auf dem Fertilize-Slot — genau wie
    /// beim Samen-Slot, nur ohne Sorten-Auswahl (Dünger ist nicht nach Frucht sortiert).
    /// Tut nichts, falls der Slot noch nicht gekauft/gespawnt ist.</summary>
    private void UpdateFertilizerSlot()
    {
        int slotIndex = slotConfigs.FindIndex(c => c.toolType == ToolType.Fertilize);
        if (slotIndex < 0) return;

        int count = PlayerInventory.Instance != null ? PlayerInventory.Instance.Fertilizer : 0;
        UpdateSeedCountBadge(slotInstances[slotIndex], true, count);
    }

    private void UpdateSeedCountBadge(HotbarSlotUI slotUI, bool show, int count)
    {
        if (slotUI == null) return;

        GameObject countRoot = slotUI.countRoot != null
            ? slotUI.countRoot
            : slotUI.countLabel != null ? slotUI.countLabel.transform.parent.gameObject : null;

        if (countRoot != null)
        {
            countRoot.SetActive(show);

            RectTransform rootRect = countRoot.transform as RectTransform;
            if (rootRect != null)
            {
                rootRect.anchorMin = new Vector2(1f, 0f);
                rootRect.anchorMax = new Vector2(1f, 0f);
                rootRect.pivot = new Vector2(1f, 0f);
                rootRect.anchoredPosition = new Vector2(-8f, 8f);
                rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 28f);
                rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 20f);
            }

            Image badgeImage = countRoot.GetComponentInChildren<Image>(true);
            if (badgeImage != null)
            {
                badgeImage.color = new Color(1f, 0.9f, 0.58f, 0.9f);
                badgeImage.raycastTarget = false;
            }
        }

        if (slotUI.countLabel == null) return;

        RectTransform labelRect = slotUI.countLabel.transform as RectTransform;
        if (labelRect != null)
        {
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        slotUI.countLabel.text = show ? count.ToString() : "";
        slotUI.countLabel.color = new Color(0.26f, 0.14f, 0.04f, 1f);
        slotUI.countLabel.alignment = TMPro.TextAlignmentOptions.Center;
        slotUI.countLabel.fontSize = 14f;
        slotUI.countLabel.raycastTarget = false;
    }

    private void AddRightClickHandler(GameObject target, System.Action callback)
    {
        var trigger = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        entry.callback.AddListener(data =>
        {
            if (((PointerEventData)data).button == PointerEventData.InputButton.Right)
                callback();
        });
        trigger.triggers.Add(entry);
    }

    private UnityEngine.InputSystem.Controls.KeyControl GetDigitKey(Keyboard keyboard, int digit) => digit switch
    {
        1 => keyboard.digit1Key,
        2 => keyboard.digit2Key,
        3 => keyboard.digit3Key,
        4 => keyboard.digit4Key,
        5 => keyboard.digit5Key,
        6 => keyboard.digit6Key,
        7 => keyboard.digit7Key,
        8 => keyboard.digit8Key,
        _ => null
    };
}

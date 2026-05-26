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

    [Header("Farben")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.85f);
    [SerializeField] private Color activeColor  = new Color(0.75f, 0.55f, 0.1f, 1f);

    private readonly List<HotbarSlotConfig> slotConfigs = new();
    private readonly List<HotbarSlotUI> slotInstances = new();

    void Awake() => Instance = this;

    void Start()
    {
        foreach (var config in defaultSlots)
            SpawnSlot(config);

        Hotbar.Instance.OnToolChanged += OnToolChanged;
        Hotbar.Instance.OnSeedChanged += _ => UpdateSeedSlot();
        PlayerInventory.Instance.OnSeedsChanged += (_, _) => UpdateSeedSlot();
        BuildModeManager.Instance.OnBuildModeChanged += OnBuildModeChanged;

        UpdateHighlight(Hotbar.Instance.ActiveTool);
        UpdateSeedSlot();
    }

    void OnDestroy()
    {
        if (BuildModeManager.Instance != null)
            BuildModeManager.Instance.OnBuildModeChanged -= OnBuildModeChanged;
        if (Hotbar.Instance != null)
            Hotbar.Instance.OnToolChanged -= OnToolChanged;
    }

    void Update()
    {
        // Im Build-Modus kein Tool-Input
        if (BuildModeManager.Instance != null && BuildModeManager.Instance.IsActive) return;

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

        // Leertaste → Dropdown öffnen wenn Seed-Slot aktiv
        if (keyboard.spaceKey.wasPressedThisFrame && Hotbar.Instance.ActiveTool == ToolType.Seed)
            SeedDropdownUI.Instance?.Toggle();
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

        // Key-Hints aktualisieren
        for (int i = 0; i < slotInstances.Count; i++)
            slotInstances[i].keyHint.text = (i + 1).ToString();
    }

    private void SelectSlot(int index)
    {
        if (index < 0 || index >= slotConfigs.Count) return;
        Hotbar.Instance.SetTool(slotConfigs[index].toolType);
    }

    private void SpawnSlot(HotbarSlotConfig config)
    {
        var go = Instantiate(slotPrefab, container);
        var slotUI = go.GetComponent<HotbarSlotUI>();

        int keyIndex = slotInstances.Count;
        slotUI.keyHint.text = (keyIndex + 1).ToString();
        slotUI.background.color = normalColor;

        if (config.icon != null)
            slotUI.icon.sprite = config.icon;

        slotUI.countLabel.gameObject.SetActive(config.showCount);
        if (config.showCount)
            slotUI.countLabel.text = "";

        var button = go.GetComponent<Button>();
        int captured = keyIndex;
        button.onClick.AddListener(() => SelectSlot(captured));

        if (config.hasDropdown)
            AddRightClickHandler(go, () => SeedDropdownUI.Instance.Toggle());

        slotConfigs.Add(config);
        slotInstances.Add(slotUI);
    }

    private void OnBuildModeChanged(bool buildModeActive)
    {
        container.gameObject.SetActive(!buildModeActive);
        if (buildModeActive)
        {
            SeedDropdownUI.Instance?.Close();
            Hotbar.Instance.SetTool(ToolType.None);
        }
    }

    private void OnToolChanged(ToolType tool) => UpdateHighlight(tool);

    private void UpdateHighlight(ToolType tool)
    {
        for (int i = 0; i < slotInstances.Count; i++)
            slotInstances[i].background.color = slotConfigs[i].toolType == tool ? activeColor : normalColor;
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
            slotUI.icon.sprite = selected?.icon ?? emptySeedSprite;
            slotUI.icon.color = Color.white;
        }

        // Count updaten
        if (slotUI.countLabel != null)
        {
            int seedCount = selected != null ? PlayerInventory.Instance.GetSeedCount(selected) : 0;
            slotUI.countLabel.text = selected != null ? $"x{seedCount}" : "";
        }
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

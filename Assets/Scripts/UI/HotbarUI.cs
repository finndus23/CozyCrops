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
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color activeColor  = new Color(0.75f, 0.55f, 0.1f, 1f);

    private readonly List<HotbarSlotConfig> slotConfigs = new();
    private readonly List<HotbarSlotUI> slotInstances = new();

    void Awake() => Instance = this;

    void Start()
    {
        foreach (var config in defaultSlots)
            SpawnSlot(config);

        SyncOwnedToolsToHotbar();

        if (ToolRegistry.Instance != null)
            ToolRegistry.Instance.OnOwnedToolsChanged += SyncOwnedToolsToHotbar;

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
        if (ToolRegistry.Instance != null)
            ToolRegistry.Instance.OnOwnedToolsChanged -= SyncOwnedToolsToHotbar;
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

    /// <summary>Gibt zurück ob ein Tool bereits in der Hotbar ist.</summary>
    public bool HasTool(ToolType tool) => slotConfigs.Exists(c => c.toolType == tool);

    /// <summary>Fügt alle owned Tools aus ToolRegistry zur Hotbar hinzu die noch fehlen.</summary>
    private void SyncOwnedToolsToHotbar()
    {
        if (ToolRegistry.Instance == null) return;
        ToolType[] allTools = { ToolType.Hoe, ToolType.WateringCan, ToolType.Scythe, ToolType.Seed };
        foreach (var tool in allTools)
        {
            if (ToolRegistry.Instance.IsOwned(tool) && !HasTool(tool))
                SpawnSlot(new HotbarSlotConfig { toolType = tool, showCount = tool == ToolType.Seed, hasDropdown = tool == ToolType.Seed });
        }
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
        slotUI.background.color = normalColor;

        var slotIcon = ToolRegistry.Instance?.GetData(config.toolType)?.icon;
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
            slotUI.icon.sprite = (selected != null && selected.icon != null) ? selected.icon : emptySeedSprite;
            slotUI.icon.color = Color.white;
        }

        int seedCount = selected != null && PlayerInventory.Instance != null
            ? PlayerInventory.Instance.GetSeedCount(selected)
            : 0;

        UpdateSeedCountBadge(slotUI, selected != null, seedCount);
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

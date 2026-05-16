using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SeedDropdownUI : MonoBehaviour
{
    public static SeedDropdownUI Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private Transform entryContainer;
    [SerializeField] private Button blocker; // vollflächiger transparenter Hintergrund

    public bool IsOpen { get; private set; }

    // Verhindert dass HotbarUI im selben Frame noch eine Taste verarbeitet
    public bool ConsumedInputThisFrame { get; private set; }

    private readonly List<PlantType> currentEntries = new();
    private readonly List<GameObject> spawnedEntries = new();

    void Awake() => Instance = this;

    void Start()
    {
        panel.SetActive(false);
        blocker.gameObject.SetActive(false);
        blocker.onClick.AddListener(Close);

        PlayerInventory.Instance.OnSeedsChanged += (_, _) => { if (IsOpen) Populate(); };
    }

    void Update()
    {
        ConsumedInputThisFrame = false;

        if (!IsOpen) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Zahlentasten 1–9 → Eintrag auswählen
        for (int i = 0; i < currentEntries.Count && i < 9; i++)
        {
            if (GetDigitKey(keyboard, i + 1).wasPressedThisFrame)
            {
                ConsumedInputThisFrame = true;
                SelectEntry(currentEntries[i]);
                return;
            }
        }

        // Escape schließt ohne Auswahl
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            ConsumedInputThisFrame = true;
            Close();
        }
    }

    public void Toggle() => (IsOpen ? (System.Action)Close : Open)();

    public void Open()
    {
        Populate();
        panel.SetActive(true);
        blocker.gameObject.SetActive(true);
        IsOpen = true;
    }

    public void Close()
    {
        panel.SetActive(false);
        blocker.gameObject.SetActive(false);
        IsOpen = false;
    }

    private void Populate()
    {
        foreach (var go in spawnedEntries) Destroy(go);
        spawnedEntries.Clear();
        currentEntries.Clear();

        foreach (var kvp in PlayerInventory.Instance.GetAllSeeds())
        {
            if (kvp.Value <= 0) continue;
            currentEntries.Add(kvp.Key);
            spawnedEntries.Add(CreateEntry(kvp.Key, kvp.Value, currentEntries.Count));
        }

        if (currentEntries.Count == 0)
            spawnedEntries.Add(CreateEmptyPlaceholder());

    }

    private GameObject CreateEntry(PlantType type, int count, int number)
    {
        // Root
        var go = new GameObject(type.plantName, typeof(RectTransform));
        go.transform.SetParent(entryContainer, false);
        var rootRect = go.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(0, 44);

        var bg = go.AddComponent<Image>();
        bg.color = Hotbar.Instance.SelectedSeed == type
            ? new Color(0.75f, 0.55f, 0.1f, 0.6f)
            : new Color(0.15f, 0.15f, 0.15f, 0.9f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(() => SelectEntry(type));

        // Icon — links, feste Größe (32x32, vertikal zentriert)
        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(go.transform, false);
        var iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0, 0.5f);
        iconRect.anchorMax = new Vector2(0, 0.5f);
        iconRect.pivot = new Vector2(0, 0.5f);
        iconRect.anchoredPosition = new Vector2(8, 0);
        iconRect.sizeDelta = new Vector2(32, 32);
        var iconImg = iconGo.AddComponent<Image>();
        iconImg.color = type.icon != null ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
        if (type.icon != null) iconImg.sprite = type.icon;

        // Count — rechts unten neben dem Icon (Left-Anchor, kein Right-Anchor)
        var countGo = new GameObject("Count", typeof(RectTransform));
        countGo.transform.SetParent(go.transform, false);
        var countRect = countGo.GetComponent<RectTransform>();
        countRect.anchorMin = new Vector2(0, 0.5f);
        countRect.anchorMax = new Vector2(0, 0.5f);
        countRect.pivot = new Vector2(0, 1f);       // oben-links des Count-Felds
        countRect.anchoredPosition = new Vector2(30, -8); // rechts unten vom Icon
        countRect.sizeDelta = new Vector2(32, 18);
        var countTmp = countGo.AddComponent<TextMeshProUGUI>();
        countTmp.text = $"x{count}";
        countTmp.fontSize = 12;
        countTmp.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        countTmp.alignment = TextAlignmentOptions.MidlineLeft;
        countTmp.overflowMode = TextOverflowModes.Overflow;

        // Name — nach Icon, streckt sich bis rechts (Left+Right Anchor = Stretch)
        var nameGo = new GameObject("Name", typeof(RectTransform));
        nameGo.transform.SetParent(go.transform, false);
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = new Vector2(50, 4);    // links: nach Icon
        nameRect.offsetMax = new Vector2(-8, -4);   // rechts: kleiner Abstand
        var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
        nameTmp.text = type.plantName;
        nameTmp.fontSize = 14;
        nameTmp.color = Color.white;
        nameTmp.alignment = TextAlignmentOptions.MidlineLeft;
        nameTmp.enableWordWrapping = false;
        nameTmp.overflowMode = TextOverflowModes.Ellipsis;

        return go;
    }

    private GameObject CreateEmptyPlaceholder()
    {
        var go = new GameObject("EmptyPlaceholder", typeof(RectTransform));
        go.transform.SetParent(entryContainer, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 44);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 0.5f); // grauer Placeholder — wird später ersetzt
        return go;
    }

    private void SelectEntry(PlantType type)
    {
        Hotbar.Instance.SetSeed(type);
        Close();
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
        9 => keyboard.digit9Key,
        _ => null
    };
}

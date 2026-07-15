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
    [SerializeField] private Button blocker;
    [SerializeField] private Sprite entrySprite;
    [SerializeField] private Sprite entryHighlightedSprite;
    [SerializeField] private Sprite entryPressedSprite;
    [SerializeField] private Sprite entryDisabledSprite;
    [SerializeField] private Sprite countBadgeSprite;
    [SerializeField] private Sprite panelBackgroundSprite;
    [SerializeField] private Sprite carrotSeedSprite;
    [SerializeField] private Sprite cauliflowerSeedSprite;
    [SerializeField] private Sprite sunflowerSeedSprite;
    [SerializeField] private List<PlantType> fallbackSeedTypes = new();

    [Header("Layout")]
    [SerializeField] private Vector2 entrySize = new(82f, 92f);
    [SerializeField] private float entrySpacing = -2f;
    [SerializeField] private Vector2 panelPadding = new(18f, 18f);

    public bool IsOpen { get; private set; }

    public bool ConsumedInputThisFrame { get; private set; }

    private readonly List<PlantType> currentEntries = new();
    private readonly List<GameObject> spawnedEntries = new();

    private void Awake() => Instance = this;

    private void Start()
    {
        DisableLayoutGroups();
        ApplyPanelBackground();
        panel.SetActive(false);
        blocker.gameObject.SetActive(false);
        blocker.onClick.AddListener(Close);

        PlayerInventory.Instance.OnSeedsChanged += (_, _) =>
        {
            if (!IsOpen) return;
            Populate();
            FitPanelToEntries();
        };
    }

    private void Update()
    {
        ConsumedInputThisFrame = false;

        if (!IsOpen) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        for (int i = 0; i < currentEntries.Count && i < 9; i++)
        {
            if (!GetDigitKey(keyboard, i + 1).wasPressedThisFrame) continue;

            ConsumedInputThisFrame = true;
            SelectEntry(currentEntries[i]);
            return;
        }

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
        FitPanelToEntries();
        blocker.transform.SetAsFirstSibling();
        panel.transform.SetAsLastSibling();
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
        foreach (GameObject go in spawnedEntries)
            Destroy(go);

        spawnedEntries.Clear();
        currentEntries.Clear();

        foreach (PlantType seed in GetKnownSeeds())
        {
            int count = PlayerInventory.Instance.GetSeedCount(seed);

            currentEntries.Add(seed);
            spawnedEntries.Add(CreateEntry(seed, count));
        }

        if (currentEntries.Count == 0)
            spawnedEntries.Add(CreateEmptyPlaceholder());
    }

    private void DisableLayoutGroups()
    {
        if (entryContainer == null) return;

        foreach (LayoutGroup layoutGroup in entryContainer.GetComponents<LayoutGroup>())
            layoutGroup.enabled = false;
    }

    private void FitPanelToEntries()
    {
        int visibleCount = Mathf.Max(1, spawnedEntries.Count);
        float width = visibleCount * entrySize.x + Mathf.Max(0, visibleCount - 1) * entrySpacing + panelPadding.x * 2f;
        float height = entrySize.y + panelPadding.y * 2f;

        if (transform is RectTransform rootRect)
            rootRect.sizeDelta = new Vector2(width, height);

        if (panel != null && panel.transform is RectTransform panelRect)
        {
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
        }

        if (entryContainer is RectTransform containerRect)
        {
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = Vector2.zero;
            containerRect.sizeDelta = new Vector2(
                width - panelPadding.x * 2f,
                height - panelPadding.y * 2f);
        }

        PositionEntries();
    }

    private void PositionEntries()
    {
        if (spawnedEntries.Count == 0) return;

        float totalWidth = spawnedEntries.Count * entrySize.x + Mathf.Max(0, spawnedEntries.Count - 1) * entrySpacing;
        float startX = -totalWidth * 0.5f + entrySize.x * 0.5f;

        for (int i = 0; i < spawnedEntries.Count; i++)
        {
            if (spawnedEntries[i].transform is not RectTransform rect) continue;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = entrySize;
            rect.anchoredPosition = new Vector2(startX + i * (entrySize.x + entrySpacing), 0f);
        }
    }

    private GameObject CreateEntry(PlantType type, int count)
    {
        GameObject go = new(type.plantName, typeof(RectTransform));
        go.transform.SetParent(entryContainer, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = entrySize;

        LayoutElement layoutElement = go.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        Image bg = go.AddComponent<Image>();
        bg.sprite = entrySprite;
        bg.preserveAspect = true;
        bool hasSeeds = count > 0;
        bool isSelected = hasSeeds && Hotbar.Instance.SelectedSeed == type;

        bg.sprite = !hasSeeds && entryDisabledSprite != null
            ? entryDisabledSprite
            : isSelected && entryHighlightedSprite != null ? entryHighlightedSprite : entrySprite;
        bg.color = Color.white;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.interactable = hasSeeds;
        if (entryHighlightedSprite != null)
        {
            SpriteState states = btn.spriteState;
            states.highlightedSprite = entryHighlightedSprite;
            states.selectedSprite = entryHighlightedSprite;
            states.pressedSprite = entryPressedSprite != null ? entryPressedSprite : entryHighlightedSprite;
            states.disabledSprite = entryDisabledSprite;
            btn.spriteState = states;
            btn.transition = Selectable.Transition.SpriteSwap;
        }
        btn.onClick.AddListener(() => SelectEntry(type));

        Image iconImg = CreateChildImage(go.transform, "Icon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(58, 58));
        Sprite seedSprite = GetSeedUiSprite(type) ?? type.icon;
        iconImg.color = seedSprite != null
            ? hasSeeds ? Color.white : new Color(1f, 1f, 1f, 0.45f)
            : new Color(0.4f, 0.4f, 0.4f, 1f);
        iconImg.preserveAspect = true;
        if (seedSprite != null)
            iconImg.sprite = seedSprite;

        Image countBadge = CreateChildImage(go.transform, "CountBadge", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-10, -10), new Vector2(28, 20));
        countBadge.sprite = countBadgeSprite;
        countBadge.type = countBadgeSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        countBadge.color = hasSeeds ? new Color(1f, 0.9f, 0.58f, 0.9f) : new Color(1f, 0.9f, 0.58f, 0.45f);

        TextMeshProUGUI countText = CreateChildText(countBadge.transform, "Count", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        RectTransform countRect = countText.GetComponent<RectTransform>();
        countRect.offsetMin = Vector2.zero;
        countRect.offsetMax = Vector2.zero;
        countText.text = count.ToString();
        countText.fontSize = 14;
        countText.color = hasSeeds ? new Color(0.23f, 0.13f, 0.04f, 1f) : new Color(0.23f, 0.13f, 0.04f, 0.55f);
        countText.alignment = TextAlignmentOptions.Center;
        countText.overflowMode = TextOverflowModes.Overflow;

        TextMeshProUGUI nameText = CreateChildText(go.transform, "Name", Vector2.zero, new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        RectTransform nameRect = nameText.GetComponent<RectTransform>();
        nameRect.offsetMin = new Vector2(8, 8);
        nameRect.offsetMax = new Vector2(-8, 30);
        nameText.text = type.plantName;
        nameText.fontSize = 10;
        nameText.color = hasSeeds ? new Color(0.23f, 0.13f, 0.04f, 1f) : new Color(0.23f, 0.13f, 0.04f, 0.55f);
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Ellipsis;

        return go;
    }

    private GameObject CreateEmptyPlaceholder()
    {
        GameObject go = new("EmptyPlaceholder", typeof(RectTransform));
        go.transform.SetParent(entryContainer, false);
        go.GetComponent<RectTransform>().sizeDelta = entrySize;

        LayoutElement layoutElement = go.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        Image img = go.AddComponent<Image>();
        img.sprite = entrySprite;
        img.preserveAspect = true;
        img.color = new Color(1f, 1f, 1f, 0.75f);

        TextMeshProUGUI label = CreateChildText(go.transform, "Label", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.offsetMin = new Vector2(12, 12);
        labelRect.offsetMax = new Vector2(-12, -12);
        label.text = "Keine Seeds";
        label.fontSize = 12;
        label.color = new Color(0.23f, 0.13f, 0.04f, 1f);
        label.alignment = TextAlignmentOptions.Center;

        return go;
    }

    private IEnumerable<PlantType> GetKnownSeeds()
    {
        HashSet<PlantType> yielded = new();

        foreach (PlantType seed in fallbackSeedTypes)
        {
            if (seed == null || !yielded.Add(seed)) continue;
            yield return seed;
        }

        if (PlantDatabase.Instance != null)
        {
            foreach (PlantType plantType in PlantDatabase.Instance.AllPlantTypes)
            {
                if (plantType == null || !yielded.Add(plantType)) continue;
                yield return plantType;
            }
        }

        foreach (PlantType seed in PlayerInventory.Instance.GetAllSeeds().Keys)
        {
            if (seed == null || !yielded.Add(seed)) continue;
            yield return seed;
        }
    }

    private void ApplyPanelBackground()
    {
        if (panel == null || panelBackgroundSprite == null) return;

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage == null) return;

        panelImage.sprite = panelBackgroundSprite;
        panelImage.color = Color.white;
        panelImage.preserveAspect = false;
    }

    private static Image CreateChildImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return go.AddComponent<Image>();
    }

    private static TextMeshProUGUI CreateChildText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return go.AddComponent<TextMeshProUGUI>();
    }

    private void SelectEntry(PlantType type)
    {
        if (PlayerInventory.Instance.GetSeedCount(type) <= 0)
            return;

        Hotbar.Instance.SetSeed(type);
        Close();
    }

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

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Einfache Ernte-Übersicht — wird später von Designern ersetzt.
/// Braucht im Inspector: panel (das Root-GameObject) + listText (ein TMP_Text darin).
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }
    public bool IsOpen => panel != null && panel.activeSelf;

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text listText;
    [SerializeField] private string title = "Scheune";
    [SerializeField] private Sprite slotSprite;
    [SerializeField] private Sprite carrotSeedSprite;
    [SerializeField] private Sprite cauliflowerSeedSprite;
    [SerializeField] private Sprite sunflowerSeedSprite;

    [Header("Layout")]
    [SerializeField] private Vector2 cardSize = new(170f, 165f);
    [SerializeField] private float cardSpacing = 14f;

    private RectTransform itemContainer;
    private readonly List<GameObject> spawnedItems = new();

    void Awake() => Instance = this;

    void Start()
    {
        PlayerInventory.Instance.OnCropsChanged += (_, _) =>
        {
            if (panel.activeSelf) Refresh();
        };
        PlayerInventory.Instance.OnSeedsChanged += (_, _) =>
        {
            if (panel.activeSelf) Refresh();
        };

        panel.SetActive(false);
        ConfigureText();
        EnsureItemContainer();
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
        if (visible)
        {
            ToolUseHandler.Instance?.CancelCast();
            SeedDropdownUI.Instance?.Close();
        }

        panel.SetActive(visible);
        if (visible) Refresh();
    }

    private void Refresh()
    {
        EnsureItemContainer();

        foreach (GameObject item in spawnedItems)
            Destroy(item);

        spawnedItems.Clear();

        if (listText != null)
            listText.text = title;

        int cropSlots = 0;
        foreach (PlantType crop in GetKnownCrops())
        {
            if (cropSlots >= 3) break;
            spawnedItems.Add(CreateCropCard(crop, PlayerInventory.Instance.GetCropCount(crop)));
            cropSlots++;
        }

        int seedSlots = 0;
        foreach (PlantType seed in GetKnownCrops())
        {
            if (seedSlots >= 3) break;
            spawnedItems.Add(CreateSeedCard(seed, PlayerInventory.Instance.GetSeedCount(seed)));
            seedSlots++;
        }
    }

    private void ConfigureText()
    {
        if (listText == null) return;

        listText.color = new Color(0.26f, 0.14f, 0.04f, 1f);
        listText.fontSize = 34;
        listText.alignment = TextAlignmentOptions.Top;
        listText.textWrappingMode = TextWrappingModes.NoWrap;
        listText.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void EnsureItemContainer()
    {
        if (itemContainer != null || panel == null) return;

        GameObject go = new("InventoryItems", typeof(RectTransform));
        go.transform.SetParent(panel.transform, false);

        itemContainer = go.GetComponent<RectTransform>();
        itemContainer.anchorMin = new Vector2(0.5f, 0.5f);
        itemContainer.anchorMax = new Vector2(0.5f, 0.5f);
        itemContainer.pivot = new Vector2(0.5f, 0.5f);
        itemContainer.anchoredPosition = new Vector2(0f, -30f);
        itemContainer.sizeDelta = new Vector2(560f, 360f);

        GridLayoutGroup layout = go.AddComponent<GridLayoutGroup>();
        layout.cellSize = cardSize;
        layout.spacing = new Vector2(cardSpacing, cardSpacing);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;
    }

    private IEnumerable<PlantType> GetKnownCrops()
    {
        if (PlantDatabase.Instance != null)
        {
            foreach (PlantType plantType in PlantDatabase.Instance.AllPlantTypes)
            {
                if (plantType != null)
                    yield return plantType;
            }

            yield break;
        }

        foreach (PlantType crop in PlayerInventory.Instance.GetAllCrops().Keys)
            yield return crop;
    }

    private GameObject CreateCropCard(PlantType crop, int count)
    {
        GameObject go = new(crop.plantName, typeof(RectTransform));
        go.transform.SetParent(itemContainer, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = cardSize;

        LayoutElement layoutElement = go.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = cardSize.x;
        layoutElement.preferredHeight = cardSize.y;

        Image bg = go.AddComponent<Image>();
        bg.sprite = slotSprite;
        bg.preserveAspect = true;
        bg.color = Color.white;

        Image icon = CreateChildImage(go.transform, "Icon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(78f, 78f));
        icon.preserveAspect = true;
        icon.color = crop.icon != null ? Color.white : Color.clear;
        icon.sprite = crop.icon;

        TextMeshProUGUI countText = CreateChildText(go.transform, "Count", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(130f, 30f));
        countText.text = $"x{count}";
        countText.fontSize = 22f;
        countText.color = new Color(0.26f, 0.14f, 0.04f, 1f);
        countText.alignment = TextAlignmentOptions.Center;
        countText.textWrappingMode = TextWrappingModes.NoWrap;

        TextMeshProUGUI nameText = CreateChildText(go.transform, "Name", Vector2.zero, new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        RectTransform nameRect = nameText.GetComponent<RectTransform>();
        nameRect.offsetMin = new Vector2(10f, 45f);
        nameRect.offsetMax = new Vector2(-10f, 70f);
        nameText.text = crop.plantName;
        nameText.fontSize = 14f;
        nameText.color = new Color(0.26f, 0.14f, 0.04f, 1f);
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Ellipsis;

        return go;
    }

    private GameObject CreateSeedCard(PlantType seed, int count)
    {
        GameObject go = CreateCropCard(seed, count);
        go.name = seed.plantName + " Seeds";

        TMP_Text nameText = go.transform.Find("Name")?.GetComponent<TMP_Text>();
        if (nameText != null)
            nameText.text = seed.plantName + " Saat";

        TMP_Text countText = go.transform.Find("Count")?.GetComponent<TMP_Text>();
        if (countText != null)
            countText.text = $"x{count}";

        Image icon = go.transform.Find("Icon")?.GetComponent<Image>();
        if (icon != null)
        {
            Sprite seedSprite = GetSeedUiSprite(seed);
            if (seedSprite != null)
                icon.sprite = seedSprite;
            icon.preserveAspect = true;
            icon.rectTransform.anchoredPosition = new Vector2(0f, -16f);
            icon.rectTransform.sizeDelta = new Vector2(96f, 86f);
        }

        return go;
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
}

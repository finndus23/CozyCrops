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

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text listText;
    [SerializeField] private string title = "Scheune";
    [SerializeField] private Sprite slotSprite;

    [Header("Layout")]
    [SerializeField] private Vector2 cardSize = new(145f, 125f);
    [SerializeField] private float cardSpacing = 18f;

    private RectTransform itemContainer;
    private readonly List<GameObject> spawnedItems = new();

    void Awake() => Instance = this;

    void Start()
    {
        PlayerInventory.Instance.OnCropsChanged += (_, _) =>
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

        foreach (PlantType crop in GetKnownCrops())
            spawnedItems.Add(CreateCropCard(crop, PlayerInventory.Instance.GetCropCount(crop)));
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
        itemContainer.anchoredPosition = new Vector2(0f, -12f);
        itemContainer.sizeDelta = new Vector2(500f, 150f);

        HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = cardSpacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
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
            new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(58f, 58f));
        icon.preserveAspect = true;
        icon.color = crop.icon != null ? Color.white : Color.clear;
        icon.sprite = crop.icon;

        TextMeshProUGUI countText = CreateChildText(go.transform, "Count", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(100f, 28f));
        countText.text = $"x{count}";
        countText.fontSize = 22f;
        countText.color = new Color(0.26f, 0.14f, 0.04f, 1f);
        countText.alignment = TextAlignmentOptions.Center;
        countText.textWrappingMode = TextWrappingModes.NoWrap;

        TextMeshProUGUI nameText = CreateChildText(go.transform, "Name", Vector2.zero, new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        RectTransform nameRect = nameText.GetComponent<RectTransform>();
        nameRect.offsetMin = new Vector2(10f, 38f);
        nameRect.offsetMax = new Vector2(-10f, 60f);
        nameText.text = crop.plantName;
        nameText.fontSize = 14f;
        nameText.color = new Color(0.26f, 0.14f, 0.04f, 1f);
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Ellipsis;

        return go;
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

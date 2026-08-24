using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Laufzeit-UI einer kaufbaren Farm-Erweiterung.
/// Die gesperrte Flaeche bleibt optisch normales Gras; erst beim Hover ueber den
/// Kaufbutton werden Flaeche und Rand als Vorschau eingeblendet.
/// </summary>
public sealed class FarmExpansionPurchaseView : MonoBehaviour
{
    private static readonly Color AffordableButton = new(0.95f, 0.64f, 0.18f, 0.98f);
    private static readonly Color AffordableHover = new(1f, 0.78f, 0.3f, 1f);
    private static readonly Color AffordablePressed = new(0.82f, 0.48f, 0.1f, 1f);
    private static readonly Color DisabledButton = new(0.34f, 0.35f, 0.36f, 0.94f);
    private static readonly Color DarkText = new(0.24f, 0.12f, 0.035f, 1f);
    private static readonly Color DisabledText = new(0.78f, 0.79f, 0.8f, 1f);

    private GridZone zone;
    private Button purchaseButton;
    private TMP_Text priceLabel;
    private GameObject previewRoot;
    private PlayerInventory inventory;
    private Camera worldCamera;
    private Vector3 normalCanvasScale;
    private Material previewFillMaterial;
    private Material previewOutlineMaterial;

    public static FarmExpansionPurchaseView Create(
        GridZone targetZone,
        string displayName,
        Vector3 worldSize,
        float buttonHeight,
        float worldScale,
        Color previewFillColor,
        Color previewOutlineColor)
    {
        GameObject canvasObject = new(
            $"Purchase Button {displayName}",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(FarmExpansionPurchaseView));
        canvasObject.transform.SetParent(targetZone.transform, false);

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(300f, 112f);
        canvasRect.localPosition = Vector3.up * Mathf.Max(0.1f, buttonHeight);
        canvasRect.localScale = Vector3.one * Mathf.Max(0.001f, worldScale);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 25;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 16f;

        FarmExpansionPurchaseView view = canvasObject.GetComponent<FarmExpansionPurchaseView>();
        view.Initialize(targetZone, displayName, worldSize, previewFillColor, previewOutlineColor);

        // Quest-Highlighting fuer "Schalte eine neue Flaeche frei". HighlightUIOutline
        // baut sich komplett aus Code auf (kein Material, kein Sprite noetig) — anders als
        // die Weltkontur-Variante fuer 3D-Objekte lässt sich das hier gefahrlos zur
        // Laufzeit zusammensetzen. HighlightTarget muss NACH dem Outline dazukommen, sonst
        // findet sein Awake() noch kein IHighlightVisual auf dem Objekt.
        //
        // zoneId löst sich automatisch: der Canvas hängt direkt unter targetZone.transform,
        // GetComponentInParent<GridZone>() im HighlightTarget.Awake() findet sie dort.
        canvasObject.AddComponent<HighlightUIOutline>();
        var highlight = canvasObject.AddComponent<HighlightTarget>();
        highlight.SetObjectiveTypes(MissionObjectiveType.UnlockZone);

        return view;
    }

    private void Initialize(
        GridZone targetZone,
        string displayName,
        Vector3 worldSize,
        Color previewFillColor,
        Color previewOutlineColor)
    {
        zone = targetZone;
        worldCamera = Camera.main;
        normalCanvasScale = transform.localScale;

        CreatePurchaseButton(displayName);
        CreateHoverPreview(displayName, worldSize, previewFillColor, previewOutlineColor);
        SetHovered(false);
        RefreshAffordability();
    }

    private void Start()
    {
        AttachInventory();
        RefreshAffordability();
    }

    private void LateUpdate()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        if (worldCamera != null)
            transform.rotation = worldCamera.transform.rotation;
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnMoneyChanged -= HandleMoneyChanged;

        if (previewFillMaterial != null)
            Destroy(previewFillMaterial);
        if (previewOutlineMaterial != null)
            Destroy(previewOutlineMaterial);
    }

    private void AttachInventory()
    {
        if (inventory == PlayerInventory.Instance)
            return;

        if (inventory != null)
            inventory.OnMoneyChanged -= HandleMoneyChanged;

        inventory = PlayerInventory.Instance;
        if (inventory != null)
            inventory.OnMoneyChanged += HandleMoneyChanged;
    }

    private void HandleMoneyChanged(int _) => RefreshAffordability();

    private void RefreshAffordability()
    {
        if (zone == null || purchaseButton == null || priceLabel == null)
            return;

        if (inventory == null)
            AttachInventory();

        bool affordable = inventory != null && inventory.Money >= zone.unlockCost;
        purchaseButton.interactable = affordable && !zone.IsUnlocked;
        priceLabel.text = $"ERWEITERN\n{zone.unlockCost:N0} G";
        priceLabel.color = affordable ? DarkText : DisabledText;
    }

    private void CreatePurchaseButton(string displayName)
    {
        GameObject buttonObject = new(
            $"Buy {displayName}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(EventTrigger));
        buttonObject.transform.SetParent(transform, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(282f, 96f);

        Image background = buttonObject.GetComponent<Image>();
        background.color = Color.white;

        purchaseButton = buttonObject.GetComponent<Button>();
        purchaseButton.targetGraphic = background;
        purchaseButton.navigation = new Navigation { mode = Navigation.Mode.None };
        purchaseButton.transition = Selectable.Transition.ColorTint;
        purchaseButton.colors = new ColorBlock
        {
            normalColor = AffordableButton,
            highlightedColor = AffordableHover,
            pressedColor = AffordablePressed,
            selectedColor = AffordableHover,
            disabledColor = DisabledButton,
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };
        purchaseButton.onClick.AddListener(HandlePurchaseClicked);

        EventTrigger trigger = buttonObject.GetComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerEnter, _ => SetHovered(true));
        AddTrigger(trigger, EventTriggerType.PointerExit, _ => SetHovered(false));

        GameObject textObject = new(
            "Price",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 6f);
        textRect.offsetMax = new Vector2(-12f, -6f);

        priceLabel = textObject.GetComponent<TextMeshProUGUI>();
        priceLabel.alignment = TextAlignmentOptions.Center;
        priceLabel.fontStyle = FontStyles.Bold;
        priceLabel.enableAutoSizing = true;
        priceLabel.fontSizeMin = 20f;
        priceLabel.fontSizeMax = 31f;
        priceLabel.textWrappingMode = TextWrappingModes.NoWrap;
        priceLabel.raycastTarget = false;

        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.9f, 0.68f, 0.55f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private void CreateHoverPreview(
        string displayName,
        Vector3 worldSize,
        Color fillColor,
        Color outlineColor)
    {
        previewRoot = new GameObject($"Hover Preview {displayName}");
        previewRoot.transform.SetParent(zone.transform, false);
        previewRoot.transform.localPosition = new Vector3(0f, -0.055f, 0f);

        GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fill.name = "Preview Fill";
        fill.transform.SetParent(previewRoot.transform, false);
        fill.transform.localPosition = Vector3.zero;
        fill.transform.localScale = new Vector3(worldSize.x, 0.02f, worldSize.z);

        Collider fillCollider = fill.GetComponent<Collider>();
        if (fillCollider != null)
        {
            fillCollider.enabled = false;
            Destroy(fillCollider);
        }

        Renderer fillRenderer = fill.GetComponent<Renderer>();
        if (fillRenderer != null)
        {
            previewFillMaterial = CreateTransparentMaterial($"Expansion Fill {displayName}", fillColor);
            fillRenderer.sharedMaterial = previewFillMaterial;
            fillRenderer.shadowCastingMode = ShadowCastingMode.Off;
            fillRenderer.receiveShadows = false;
        }

        LineRenderer outline = previewRoot.AddComponent<LineRenderer>();
        outline.useWorldSpace = false;
        outline.loop = true;
        outline.positionCount = 4;
        outline.widthMultiplier = 0.1f;
        outline.numCornerVertices = 2;
        outline.numCapVertices = 2;
        outline.shadowCastingMode = ShadowCastingMode.Off;
        outline.receiveShadows = false;
        outline.startColor = outlineColor;
        outline.endColor = outlineColor;

        float halfX = worldSize.x * 0.5f;
        float halfZ = worldSize.z * 0.5f;
        const float lineY = 0.035f;
        outline.SetPosition(0, new Vector3(-halfX, lineY, -halfZ));
        outline.SetPosition(1, new Vector3(-halfX, lineY, halfZ));
        outline.SetPosition(2, new Vector3(halfX, lineY, halfZ));
        outline.SetPosition(3, new Vector3(halfX, lineY, -halfZ));

        Shader outlineShader = Shader.Find("Sprites/Default");
        if (outlineShader == null)
            outlineShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (outlineShader != null)
        {
            previewOutlineMaterial = new Material(outlineShader)
            {
                name = $"Expansion Outline {displayName}",
                color = outlineColor,
                renderQueue = 3001
            };
            outline.sharedMaterial = previewOutlineMaterial;
        }
    }

    private static Material CreateTransparentMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            return null;

        Material material = new(shader)
        {
            name = materialName,
            color = color,
            renderQueue = 3000
        };

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", 5f);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", 10f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        return material;
    }

    private void HandlePurchaseClicked()
    {
        if (zone == null || zone.IsUnlocked)
            return;

        bool unlocked = ZoneManager.Instance != null
            ? ZoneManager.Instance.TryUnlockZone(zone)
            : zone.TryUnlock();

        if (!unlocked)
            RefreshAffordability();
    }

    private void SetHovered(bool hovered)
    {
        if (previewRoot != null)
            previewRoot.SetActive(hovered && zone != null && !zone.IsUnlocked);

        transform.localScale = hovered ? normalCanvasScale * 1.06f : normalCanvasScale;
    }

    private static void AddTrigger(
        EventTrigger trigger,
        EventTriggerType eventType,
        UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new() { eventID = eventType };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }
}

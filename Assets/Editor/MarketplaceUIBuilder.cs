#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MarketplaceUIBuilder
{
    private const string ScenePath = "Assets/Scenes/Marketplace.unity";
    private const string FarmScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RowPrefabPath = "Assets/Scripts/Market/MarketShopRow.prefab";
    private const string AddonsPath = "Assets/UI/ShopAddons.png";
    private const string GeneralPath = "Assets/UI/GeneralSheet.png";
    private const string BarnPath = "Assets/UI/barn.png";
    private const string SeedsPath = "Assets/UI/Seeds.png";

    private static readonly Color Brown = new(0.22f, 0.12f, 0.06f, 1f);
    private static Dictionary<string, Sprite> addons;
    private static Dictionary<string, Sprite> general;
    private static Dictionary<string, Sprite> menuClean;
    private static Dictionary<string, Sprite> seeds;
    private static Sprite barnIcon;

    [MenuItem("CozyCrops/UI/Rebuild Marketplace UI")]
    public static void Build()
    {
        addons = LoadSpriteMap(AddonsPath);
        general = LoadSpriteMap(GeneralPath);
        menuClean = LoadSpriteMap("Assets/UI/MenuSheetClean.png");
        seeds = LoadSpriteMap(SeedsPath);
        barnIcon = AssetDatabase.LoadAllAssetsAtPath(BarnPath).OfType<Sprite>().FirstOrDefault();

        StyleRowPrefab();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Scene farmScene = EditorSceneManager.OpenScene(FarmScenePath, OpenSceneMode.Additive);
        StyleMarketplaceScene(scene, farmScene);
        EditorSceneManager.CloseScene(farmScene, true);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[MarketplaceUIBuilder] Marketplace UI rebuilt successfully.");
    }

    public static void BuildBatch()
    {
        Build();
        EditorApplication.Exit(0);
    }

    private static void StyleMarketplaceScene(Scene scene, Scene farmScene)
    {
        GameObject buy = Find(scene, "BuyPanel");
        GameObject sell = Find(scene, "SellPanel");
        GameObject upgrade = Find(scene, "UpgradePanel");

        AssignMarketSeedIcons(scene);

        StyleShopPanel(buy, "BuyTitelText", "Saatgut kaufen", addons["ShopAddons_0"]);
        StyleShopPanel(upgrade, "ShopTitelText", "Werkzeuge verbessern", addons["ShopAddons_2"]);
        CopyShopLayout(sell, buy, "SellTitelText", "BuyTitelText");
        CopyShopLayout(sell, upgrade, "SellTitelText", "ShopTitelText");

        StyleDialogue(scene);
        StylePersistentMoney(scene, farmScene);
        StyleStatus(scene);
        StyleCloseButton(scene);
        StyleNavigation(scene);
        EnsureOverlayOrder(scene);
    }

    private static void StyleShopPanel(GameObject panel, string titleName, string title, Sprite categoryIcon)
    {
        if (panel == null) return;

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(420f, -10f);
        rect.sizeDelta = new Vector2(700f, 790f);

        Image background = GetOrAdd<Image>(panel);
        background.sprite = menuClean["MenuClean_Panel"];
        background.type = Image.Type.Sliced;
        background.preserveAspect = false;
        background.color = Color.white;
        background.raycastTarget = true;

        TMP_Text titleText = FindChild(panel.transform, titleName)?.GetComponent<TMP_Text>();
        if (titleText != null)
        {
            titleText.text = title;
            titleText.color = new Color(1f, 0.93f, 0.72f, 1f);
            titleText.fontSize = 30f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.raycastTarget = false;
            RectTransform titleRect = (RectTransform)titleText.transform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(28f, -23f);
            titleRect.sizeDelta = new Vector2(430f, 58f);
        }

        Transform plateTransform = FindChild(panel.transform, "ShopTitlePlate");
        GameObject plate = plateTransform != null ? plateTransform.gameObject : CreateUI("ShopTitlePlate", panel.transform);
        Image plateImage = GetOrAdd<Image>(plate);
        plateImage.sprite = addons["ShopAddons_3"];
        plateImage.type = Image.Type.Sliced;
        plateImage.raycastTarget = false;
        RectTransform plateRect = plate.GetComponent<RectTransform>();
        plateRect.anchorMin = plateRect.anchorMax = new Vector2(0.5f, 1f);
        plateRect.pivot = new Vector2(0.5f, 1f);
        plateRect.anchoredPosition = new Vector2(0f, 23f);
        plateRect.sizeDelta = new Vector2(500f, 92f);
        plate.transform.SetAsFirstSibling();

        Transform iconTransform = FindChild(panel.transform, "CategoryIcon");
        GameObject iconObject = iconTransform != null ? iconTransform.gameObject : CreateUI("CategoryIcon", panel.transform);
        Image icon = GetOrAdd<Image>(iconObject);
        icon.sprite = categoryIcon;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(-228f, -6f);
        iconRect.sizeDelta = new Vector2(72f, 72f);
        iconTransform = iconObject.transform;
        iconTransform.SetAsLastSibling();

        ScrollRect scroll = panel.GetComponentInChildren<ScrollRect>(true);
        if (scroll != null)
        {
            RectTransform scrollRect = (RectTransform)scroll.transform;
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(62f, 78f);
            scrollRect.offsetMax = new Vector2(-62f, -108f);

            if (scroll.viewport != null)
            {
                RectTransform viewportRect = scroll.viewport;
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.pivot = new Vector2(0.5f, 0.5f);
                viewportRect.anchoredPosition = Vector2.zero;
                viewportRect.sizeDelta = Vector2.zero;

                Image viewportImage = scroll.viewport.GetComponent<Image>();
                // Eine transparente Graphic schreibt bei Unity UI Mask keinen brauchbaren
                // Stencil und clippt dadurch alle zur Laufzeit erzeugten Shop-Zeilen weg.
                if (viewportImage != null)
                {
                    viewportImage.color = Color.white;
                    viewportImage.raycastTarget = true;
                }
                Mask mask = scroll.viewport.GetComponent<Mask>();
                if (mask != null) mask.showMaskGraphic = false;
            }

            if (scroll.content != null)
            {
                RectTransform contentRect = scroll.content;
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.anchoredPosition = Vector2.zero;
                contentRect.sizeDelta = new Vector2(0f, 0f);

                VerticalLayoutGroup layout = scroll.content.GetComponent<VerticalLayoutGroup>();
                if (layout != null)
                {
                    layout.padding = new RectOffset(4, 12, 8, 8);
                    layout.spacing = 12f;
                    layout.childAlignment = TextAnchor.UpperCenter;
                    layout.childForceExpandWidth = false;
                    layout.childForceExpandHeight = false;
                    layout.childControlWidth = true;
                    layout.childControlHeight = true;
                }
            }

            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.verticalNormalizedPosition = 1f;
            if (scroll.verticalScrollbar != null)
                scroll.verticalScrollbar.gameObject.SetActive(false);
        }
    }

    private static void AssignMarketSeedIcons(Scene scene)
    {
        FarmMarketDialogueShopController controller = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<FarmMarketDialogueShopController>(true))
            .FirstOrDefault();
        if (controller == null) return;

        SerializedObject data = new(controller);
        data.FindProperty("carrotSeedSprite").objectReferenceValue = seeds["Seeds_7"];
        data.FindProperty("cauliflowerSeedSprite").objectReferenceValue = seeds["Seeds_1"];
        data.FindProperty("sunflowerSeedSprite").objectReferenceValue = seeds["Seeds_4"];
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void StyleRowPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(RowPrefabPath);
        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(560f, 116f);

            Image rowImage = root.GetComponent<Image>();
            rowImage.sprite = menuClean["MenuClean_Panel"];
            rowImage.type = Image.Type.Sliced;
            rowImage.preserveAspect = false;
            rowImage.raycastTarget = false;

            HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 14, 14);
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.MiddleCenter;

            LayoutElement rootLayout = root.GetComponent<LayoutElement>();
            rootLayout.minHeight = 116f;
            rootLayout.preferredHeight = 116f;
            rootLayout.preferredWidth = 560f;

            FarmMarketShopRowUI row = root.GetComponent<FarmMarketShopRowUI>();
            StyleRowIcon(row, root.transform);
            StyleRowTexts(root);
            StyleRowButton(FindChild(root.transform, "PrimaryButton")?.GetComponent<Button>(), true);
            StyleRowButton(FindChild(root.transform, "SecondaryButton")?.GetComponent<Button>(), false);

            PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void StyleRowIcon(FarmMarketShopRowUI row, Transform root)
    {
        Image icon = GetPrivateField<Image>(row, "iconImage");
        if (icon == null) return;

        Transform existingFrame = FindChild(root, "IconFrame");
        GameObject frame = existingFrame != null ? existingFrame.gameObject : CreateUI("IconFrame", root);
        if (existingFrame == null)
        {
            int index = icon.transform.GetSiblingIndex();
            frame.transform.SetSiblingIndex(index);
            icon.transform.SetParent(frame.transform, false);
        }

        Image frameImage = GetOrAdd<Image>(frame);
        frameImage.sprite = general["GeneralSheet_3"];
        frameImage.preserveAspect = true;
        frameImage.raycastTarget = false;

        LayoutElement frameLayout = GetOrAdd<LayoutElement>(frame);
        frameLayout.preferredWidth = 52f;
        frameLayout.preferredHeight = 52f;

        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(7f, 7f);
        iconRect.offsetMax = new Vector2(-7f, -7f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
    }

    private static void StyleRowTexts(GameObject root)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            text.color = Brown;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = text.name == "NameText" ? FontStyles.Bold : FontStyles.Normal;
            text.fontSize = text.name == "NameText" ? 15f : 12.5f;
        }

        SetPreferredWidth(root.transform, "NameText", 105f);
        SetPreferredWidth(root.transform, "AmountText", 90f);
        SetPreferredWidth(root.transform, "PriceText", 95f);
    }

    private static void StyleRowButton(Button button, bool primary)
    {
        if (button == null) return;

        int start = primary ? 12 : 16;
        Image image = button.GetComponent<Image>();
        image.sprite = addons[$"ShopAddons_{start}"];
        image.type = Image.Type.Simple;
        image.preserveAspect = false;

        SpriteState state = button.spriteState;
        state.highlightedSprite = addons[$"ShopAddons_{start + 1}"];
        state.selectedSprite = addons[$"ShopAddons_{start + 1}"];
        state.pressedSprite = addons[$"ShopAddons_{start + 2}"];
        state.disabledSprite = addons[$"ShopAddons_{start + 3}"];
        button.spriteState = state;
        button.transition = Selectable.Transition.SpriteSwap;

        LayoutElement element = GetOrAdd<LayoutElement>(button.gameObject);
        element.preferredWidth = primary ? 72f : 64f;
        element.preferredHeight = 48f;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = Brown;
            label.fontSize = 11.5f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }
    }

    private static void StyleDialogue(Scene scene)
    {
        GameObject legacyPanel = Find(scene, "ShopPanel");
        if (legacyPanel != null)
        {
            Image legacyBackground = legacyPanel.GetComponent<Image>();
            if (legacyBackground != null)
            {
                legacyBackground.sprite = null;
                legacyBackground.color = Color.clear;
                legacyBackground.raycastTarget = false;
                legacyBackground.enabled = false;
            }

            Transform legacyTitle = legacyPanel.transform.Find("ShopTitelText");
            if (legacyTitle != null)
                legacyTitle.gameObject.SetActive(false);
        }

        GameObject bubble = Find(scene, "SpeechBubble");
        if (bubble != null)
        {
            Image bubbleImage = bubble.GetComponent<Image>();
            if (bubbleImage != null)
            {
                bubbleImage.sprite = menuClean["MenuClean_Panel"];
                bubbleImage.type = Image.Type.Sliced;
                bubbleImage.color = Color.white;
                bubbleImage.raycastTarget = false;
                bubbleImage.enabled = true;
            }

            RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
            bubbleRect.anchorMin = bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
            bubbleRect.pivot = new Vector2(0.5f, 0.5f);
            bubbleRect.anchoredPosition = new Vector2(-430f, -355f);
            bubbleRect.sizeDelta = new Vector2(760f, 190f);
            bubble.SetActive(true);
        }

        TMP_Text npc = Find(scene, "NPCName")?.GetComponent<TMP_Text>();
        if (npc != null && bubble != null)
        {
            npc.gameObject.SetActive(true);
            npc.color = new Color(1f, 0.93f, 0.72f, 1f);
            npc.fontSize = 25f;
            npc.fontStyle = FontStyles.Bold;
            npc.alignment = TextAlignmentOptions.Center;
            npc.raycastTarget = false;
            RectTransform npcRect = (RectTransform)npc.transform;
            npcRect.anchorMin = npcRect.anchorMax = new Vector2(0.5f, 1f);
            npcRect.pivot = new Vector2(0.5f, 1f);
            npcRect.anchoredPosition = new Vector2(0f, 20f);
            npcRect.sizeDelta = new Vector2(330f, 62f);

            Transform plateTransform = FindChild(bubble.transform, "NPCNamePlate");
            GameObject plate = plateTransform != null ? plateTransform.gameObject : CreateUI("NPCNamePlate", bubble.transform);
            Image plateImage = GetOrAdd<Image>(plate);
            plateImage.sprite = addons["ShopAddons_3"];
            plateImage.type = Image.Type.Sliced;
            plateImage.raycastTarget = false;
            RectTransform plateRect = plate.GetComponent<RectTransform>();
            plateRect.anchorMin = plateRect.anchorMax = new Vector2(0.5f, 1f);
            plateRect.pivot = new Vector2(0.5f, 1f);
            plateRect.anchoredPosition = new Vector2(0f, 31f);
            plateRect.sizeDelta = new Vector2(390f, 80f);
            plate.transform.SetAsFirstSibling();
            npc.transform.SetAsLastSibling();
        }

        TMP_Text speech = Find(scene, "SpeechBubbleText")?.GetComponent<TMP_Text>();
        if (speech != null)
        {
            speech.gameObject.SetActive(true);
            speech.color = Brown;
            speech.fontSize = 22f;
            speech.fontStyle = FontStyles.Normal;
            speech.alignment = TextAlignmentOptions.Center;
            speech.textWrappingMode = TextWrappingModes.Normal;
            speech.overflowMode = TextOverflowModes.Overflow;
            speech.raycastTarget = false;
            RectTransform speechRect = (RectTransform)speech.transform;
            speechRect.anchorMin = new Vector2(0.07f, 0.08f);
            speechRect.anchorMax = new Vector2(0.93f, 0.70f);
            speechRect.offsetMin = Vector2.zero;
            speechRect.offsetMax = Vector2.zero;
        }
    }

    private static void StylePersistentMoney(Scene scene, Scene farmScene)
    {
        TMP_Text money = Find(scene, "MoneyText")?.GetComponent<TMP_Text>();
        TMP_Text farmMoney = Find(farmScene, "MoneyText")?.GetComponent<TMP_Text>();
        if (money == null || farmMoney == null) return;

        Transform oldWrapper = money.transform.parent != null && money.transform.parent.name == "MarketMoneyBackground"
            ? money.transform.parent
            : null;
        Canvas canvas = Find(scene, "Canvas")?.GetComponent<Canvas>() ?? money.GetComponentInParent<Canvas>();
        if (canvas != null)
            money.transform.SetParent(canvas.transform, false);

        CopyRectTransform((RectTransform)farmMoney.transform, (RectTransform)money.transform);
        EditorUtility.CopySerialized(farmMoney, money);
        money.raycastTarget = false;

        MoneyDisplay farmDisplay = farmMoney.GetComponent<MoneyDisplay>();
        MoneyDisplay marketDisplay = GetOrAdd<MoneyDisplay>(money.gameObject);
        if (farmDisplay != null)
        {
            EditorUtility.CopySerialized(farmDisplay, marketDisplay);
            SerializedObject data = new(marketDisplay);
            data.FindProperty("moneyText").objectReferenceValue = money;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        money.transform.SetAsLastSibling();
        if (oldWrapper != null)
            UnityEngine.Object.DestroyImmediate(oldWrapper.gameObject);
    }

    private static void StyleStatus(Scene scene)
    {
        TMP_Text status = Find(scene, "StatusText")?.GetComponent<TMP_Text>();
        if (status == null) return;

        Transform wrapperTransform = FindChild(status.transform.parent, "MarketStatusBackground");
        GameObject wrapper = wrapperTransform != null ? wrapperTransform.gameObject : CreateUI("MarketStatusBackground", status.transform.parent);
        if (status.transform.parent != wrapper.transform)
            status.transform.SetParent(wrapper.transform, false);

        GameObject dialogueRoot = Find(scene, "DialogueRoot");
        if (dialogueRoot != null && wrapper.transform.parent != dialogueRoot.transform)
            wrapper.transform.SetParent(dialogueRoot.transform, false);

        Image image = GetOrAdd<Image>(wrapper);
        image.sprite = addons["ShopAddons_22"];
        image.preserveAspect = false;
        image.raycastTarget = false;

        RectTransform wrapperRect = wrapper.GetComponent<RectTransform>();
        wrapperRect.anchorMin = wrapperRect.anchorMax = new Vector2(0.5f, 0.5f);
        wrapperRect.anchoredPosition = new Vector2(420f, -375f);
        wrapperRect.sizeDelta = new Vector2(330f, 58f);

        RectTransform statusRect = (RectTransform)status.transform;
        statusRect.anchorMin = Vector2.zero;
        statusRect.anchorMax = Vector2.one;
        statusRect.offsetMin = new Vector2(52f, 5f);
        statusRect.offsetMax = new Vector2(-12f, -5f);
        status.color = Brown;
        status.fontSize = 18f;
        status.alignment = TextAlignmentOptions.Center;
        status.raycastTarget = false;
    }

    private static void StyleCloseButton(Scene scene)
    {
        GameObject close = Find(scene, "CloseButton");
        if (close == null) return;

        GameObject dialogueRoot = Find(scene, "DialogueRoot");
        if (dialogueRoot != null && close.transform.parent != dialogueRoot.transform)
            close.transform.SetParent(dialogueRoot.transform, false);

        RectTransform rect = close.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(744f, 348f);
        rect.sizeDelta = new Vector2(58f, 58f);

        Image image = close.GetComponent<Image>();
        image.sprite = general["GeneralSheet_43"];
        image.preserveAspect = true;

        Button button = close.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.pressedColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        button.colors = colors;

        foreach (TMP_Text text in close.GetComponentsInChildren<TMP_Text>(true))
            text.gameObject.SetActive(false);
    }

    private static void StyleNavigation(Scene scene)
    {
        GameObject farm = Find(scene, "FarmButton");
        if (farm == null) return;

        Transform parent = farm.transform.parent;
        FarmMarketSceneTransition transition = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<FarmMarketSceneTransition>(true))
            .FirstOrDefault();

        StyleNavigationButton(farm, new Vector2(106f, -24f), barnIcon);

        Transform existingMenu = FindChild(parent, "MenuButton");
        GameObject menu = existingMenu != null ? existingMenu.gameObject : CreateUI("MenuButton", parent);
        StyleNavigationButton(menu, new Vector2(24f, -24f), general["GeneralSheet_48"]);

        GameSceneMenuController menuController = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<GameSceneMenuController>(true))
            .FirstOrDefault();
        if (menuController == null)
        {
            Canvas canvas = parent.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                menuController = canvas.gameObject.AddComponent<GameSceneMenuController>();
                SerializedObject serialized = new(menuController);
                serialized.FindProperty("hudCanvas").objectReferenceValue = canvas;
                serialized.FindProperty("confirmationPanelSprite").objectReferenceValue = menuClean["MenuClean_Panel"];
                serialized.FindProperty("normalButtonSprite").objectReferenceValue = menuClean["MenuClean_NormalButton"];
                serialized.FindProperty("confirmButtonSprite").objectReferenceValue = menuClean["MenuClean_GreenButton"];
                serialized.FindProperty("pauseWithEscape").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        Button menuButton = GetOrAdd<Button>(menu);
        menuButton.onClick = new Button.ButtonClickedEvent();
        if (menuController != null)
            UnityEventTools.AddPersistentListener(menuButton.onClick, menuController.RequestBackToMainMenu);
        else if (transition != null)
            UnityEventTools.AddPersistentListener(menuButton.onClick, transition.GoToMainMenu);
    }

    private static void StyleNavigationButton(GameObject target, Vector2 position, Sprite iconSprite)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null) rect = target.AddComponent<RectTransform>();

        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(70f, 70f);

        Image image = GetOrAdd<Image>(target);
        image.sprite = iconSprite;
        image.preserveAspect = true;

        Button button = GetOrAdd<Button>(target);
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.pressedColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        foreach (TMP_Text text in target.GetComponentsInChildren<TMP_Text>(true))
            text.gameObject.SetActive(false);

        Transform iconTransform = FindChild(target.transform, "NavigationIcon");
        if (iconTransform != null)
            iconTransform.gameObject.SetActive(false);
    }

    private static void EnsureOverlayOrder(Scene scene)
    {
        GameObject dialogueRoot = Find(scene, "DialogueRoot");
        if (dialogueRoot == null) return;

        string[] overlayNames = { "MarketStatusBackground", "CloseButton" };
        foreach (string objectName in overlayNames)
        {
            Transform overlay = FindChild(dialogueRoot.transform, objectName);
            if (overlay != null)
                overlay.SetAsLastSibling();
        }

        GameObject menu = Find(scene, "MenuButton");
        GameObject farm = Find(scene, "FarmButton");
        if (menu != null) menu.transform.SetAsLastSibling();
        if (farm != null) farm.transform.SetAsLastSibling();
    }

    private static void CopyShopLayout(GameObject source, GameObject target, string sourceTitleName, string targetTitleName)
    {
        if (source == null || target == null) return;

        CopyRectTransform(source.GetComponent<RectTransform>(), target.GetComponent<RectTransform>());
        CopyNamedRect(source.transform, target.transform, sourceTitleName, targetTitleName);
        CopyNamedRect(source.transform, target.transform, "CategoryIcon", "CategoryIcon");
        CopyNamedRect(source.transform, target.transform, "ShopTitlePlate", "ShopTitlePlate");

        ScrollRect sourceScroll = source.GetComponentInChildren<ScrollRect>(true);
        ScrollRect targetScroll = target.GetComponentInChildren<ScrollRect>(true);
        if (sourceScroll == null || targetScroll == null) return;

        CopyRectTransform((RectTransform)sourceScroll.transform, (RectTransform)targetScroll.transform);
        if (sourceScroll.viewport != null && targetScroll.viewport != null)
            CopyRectTransform(sourceScroll.viewport, targetScroll.viewport);
    }

    private static void CopyNamedRect(Transform sourceRoot, Transform targetRoot, string sourceName, string targetName)
    {
        RectTransform source = FindChild(sourceRoot, sourceName)?.GetComponent<RectTransform>();
        RectTransform target = FindChild(targetRoot, targetName)?.GetComponent<RectTransform>();
        CopyRectTransform(source, target);
    }

    private static void CopyRectTransform(RectTransform source, RectTransform target)
    {
        if (source == null || target == null) return;
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localScale = source.localScale;
        target.localRotation = source.localRotation;
    }

    private static Dictionary<string, Sprite> LoadSpriteMap(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(sprite => sprite.name);

    private static GameObject Find(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform match = FindChild(root.transform, objectName);
            if (match != null) return match.gameObject;
        }
        return null;
    }

    private static Transform FindChild(Transform root, string objectName)
    {
        if (root.name == objectName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindChild(root.GetChild(i), objectName);
            if (match != null) return match;
        }
        return null;
    }

    private static GameObject CreateUI(string objectName, Transform parent)
    {
        GameObject go = new(objectName, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component =>
        go.GetComponent<T>() ?? go.AddComponent<T>();

    private static T GetPrivateField<T>(object target, string fieldName) where T : class
    {
        return target.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(target) as T;
    }

    private static void SetPreferredWidth(Transform root, string objectName, float width)
    {
        Transform child = FindChild(root, objectName);
        if (child == null) return;
        LayoutElement element = GetOrAdd<LayoutElement>(child.gameObject);
        element.preferredWidth = width;
        element.preferredHeight = 84f;
    }
}
#endif

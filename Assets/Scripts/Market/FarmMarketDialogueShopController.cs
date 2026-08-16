using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Steuert das Markt-Gespräch und schaltet getrennt zwischen BuyPanel und SellPanel.
/// Buy-NPC  -> nur BuyPanel sichtbar.
/// Sell-NPC -> nur SellPanel sichtbar.
/// Nutzt dein vorhandenes PlayerInventory/FarmSaveManager-System.
///
/// Fix: Unterstützt jetzt mehrere CloseButtons und eine public CloseShop()-Methode,
/// damit BuyPanel und SellPanel jeweils eigene Close-Buttons haben können.
/// </summary>
public class FarmMarketDialogueShopController : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Camera dialogueCamera;
    [SerializeField] private bool autoPositionDialogueCamera = true;
    [Tooltip("X = seitlicher Versatz nach rechts aus Kamerasicht, Y = Hoehe, Z = Abstand vor dem NPC. Das Vorzeichen von Z wird ignoriert.")]
    [SerializeField] private Vector3 cameraOffsetFromNpc = new Vector3(2.4f, 1.6f, -3.0f);
    [SerializeField] private float dialogueSideOffsetFactor = 0.45f;
    [SerializeField] private float dialogueFrontOffsetFactor = 1.15f;
    [SerializeField] private Vector3 cameraLookOffset = new Vector3(0.8f, 0.7f, 0f);
    [SerializeField] private Vector2 npcViewportPosition = new Vector2(0.3f, 0.52f);

    [Header("Root UI")]
    [SerializeField] private GameObject dialogueRoot;

    [Tooltip("Optional alter einzelner Close Button. Kann leer bleiben, wenn du Close Buttons benutzt.")]
    [SerializeField] private Button closeButton;

    [Tooltip("Hier kannst du CloseButton aus BuyPanel und CloseButton aus SellPanel eintragen.")]
    [SerializeField] private Button[] closeButtons;

    [Header("NPC Text")]
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private TMP_Text speechBubbleText;

    [Header("Shared Header")]
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text statusText;

    [Header("Separate UIs")]
    [SerializeField] private GameObject buyPanel;
    [SerializeField] private GameObject sellPanel;
    [SerializeField] private GameObject upgradePanel;

    [Tooltip("Panel des Lizenz-Amts. Leer lassen = das Upgrade-Panel wird mitbenutzt; die " +
             "Zeilen haben ohnehin dieselbe Struktur. So läuft der Lizenz-NPC ohne " +
             "zusätzliches Szenen-Setup.")]
    [SerializeField] private GameObject licensePanel;

    [Header("Content Roots")]
    [SerializeField] private Transform buyContentRoot;
    [SerializeField] private Transform sellContentRoot;
    [SerializeField] private Transform upgradeContentRoot;

    [Tooltip("Leer lassen = upgradeContentRoot wird mitbenutzt.")]
    [SerializeField] private Transform licenseContentRoot;

    [SerializeField] private FarmMarketShopRowUI rowPrefab;

    [Header("Seed UI Icons")]
    [SerializeField] private Sprite carrotSeedSprite;
    [SerializeField] private Sprite cauliflowerSeedSprite;
    [SerializeField] private Sprite sunflowerSeedSprite;

    [Header("Data")]
    [SerializeField] private PlayerInventory inventory;

    [Header("Trade Behaviour")]
    [SerializeField] private bool saveAfterTrade = true;
    [SerializeField] private bool allowSellingSeeds = true;
    [SerializeField] private bool allowSellingCrops = true;
    [SerializeField] private float seedSellPriceFactor = 0.5f;

    [Header("Münzflug")]
    [Tooltip("Münzen fliegen beim Verkauf von der Zeile zur Geldanzeige — derselbe Effekt " +
             "wie beim Abholen einer Missions-Belohnung.")]
    [SerializeField] private bool coinFlightOnSell = true;

    [Tooltip("Timing und Optik des Münzflugs beim Verkauf.\n\n" +
             "Darf knapper eingestellt sein als bei Missionen: im Shop verkauft man mehrmals " +
             "hintereinander, und ein langer Schwarm überlagert sich sonst selbst.")]
    [SerializeField] private CoinFlightSettings coinFlight = new();

    private FarmMarketNpc currentNpc;
    private AudioListener gameplayAudioListener;
    private AudioListener dialogueAudioListener;
    private bool closeButtonsRegistered;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        CacheReferencesIfNeeded();
        RegisterCloseButtons();
        CloseInstant();
    }

    private void OnEnable()
    {
        RegisterCloseButtons();
    }

    private void OnDestroy()
    {
        UnregisterCloseButtons();
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (WasEscapePressed())
            CloseShop();
    }

    public void Open(FarmMarketNpc npc)
    {
        if (npc == null)
            return;

        CacheReferencesIfNeeded();

        if (inventory == null)
        {
            Debug.LogWarning("[FarmMarketDialogueShopController] Kein PlayerInventory gefunden. Lege in der MarketScene ein GameObject mit PlayerInventory an oder weise es im Inspector zu.");
            return;
        }

        currentNpc = npc;
        currentNpc.BeginDialogue();
        IsOpen = true;

        if (dialogueRoot != null)
            dialogueRoot.SetActive(true);

        RegisterCloseButtons();

        if (gameplayCamera != null)
        {
            gameplayCamera.enabled = false;
            SetAudioListenerEnabled(gameplayAudioListener, false);
        }

        if (dialogueCamera != null)
        {
            if (autoPositionDialogueCamera)
                PositionDialogueCamera(npc);

            dialogueCamera.enabled = true;
            dialogueCamera.gameObject.SetActive(true);
            SetAudioListenerEnabled(dialogueAudioListener, true);
        }

        if (npcNameText != null)
            npcNameText.text = npc.DisplayName;

        if (speechBubbleText != null)
            speechBubbleText.text = npc.SpeechText;

        SetStatus(string.Empty);
        RefreshShop();
    }

    /// <summary>
    /// Alte Methode bleibt absichtlich drin, falls irgendwo schon Close() im Button-OnClick steht.
    /// </summary>
    public void Close()
    {
        CloseShop();
    }

    /// <summary>
    /// Diese Methode kannst du direkt bei jedem CloseButton im OnClick eintragen.
    /// </summary>
    public void CloseShop()
    {
        IsOpen = false;
        if (currentNpc != null)
            currentNpc.EndDialogue();

        currentNpc = null;

        ClearRows(buyContentRoot);
        ClearRows(sellContentRoot);
        ClearRows(upgradeContentRoot);

        if (buyPanel != null)
            buyPanel.SetActive(false);

        if (sellPanel != null)
            sellPanel.SetActive(false);

        if (upgradePanel != null)
            upgradePanel.SetActive(false);

        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);

        if (dialogueCamera != null)
        {
            dialogueCamera.enabled = false;
            SetAudioListenerEnabled(dialogueAudioListener, false);
            dialogueCamera.gameObject.SetActive(false);
        }

        if (gameplayCamera != null)
        {
            gameplayCamera.gameObject.SetActive(true);
            gameplayCamera.enabled = true;
            SetAudioListenerEnabled(gameplayAudioListener, true);
        }
    }

    private void CloseInstant()
    {
        IsOpen = false;
        if (currentNpc != null)
            currentNpc.EndDialogue();

        currentNpc = null;

        ClearRows(buyContentRoot);
        ClearRows(sellContentRoot);
        ClearRows(upgradeContentRoot);

        if (buyPanel != null)
            buyPanel.SetActive(false);

        if (sellPanel != null)
            sellPanel.SetActive(false);

        if (upgradePanel != null)
            upgradePanel.SetActive(false);

        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);

        if (dialogueCamera != null)
        {
            dialogueCamera.enabled = false;
            SetAudioListenerEnabled(dialogueAudioListener, false);
            dialogueCamera.gameObject.SetActive(false);
        }

        if (gameplayCamera != null)
        {
            gameplayCamera.gameObject.SetActive(true);
            gameplayCamera.enabled = true;
            SetAudioListenerEnabled(gameplayAudioListener, true);
        }
    }

    private void CacheReferencesIfNeeded()
    {
        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        if (gameplayCamera != null && gameplayAudioListener == null)
            gameplayAudioListener = gameplayCamera.GetComponent<AudioListener>();

        if (dialogueCamera != null && dialogueAudioListener == null)
            dialogueAudioListener = dialogueCamera.GetComponent<AudioListener>();

        if (inventory == null)
            inventory = PlayerInventory.Instance;
    }

    private void SetAudioListenerEnabled(AudioListener listener, bool enabled)
    {
        if (listener != null)
            listener.enabled = enabled;
    }

    private void RegisterCloseButtons()
    {
        if (closeButtonsRegistered)
            return;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseShop);
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(CloseShop);
        }

        if (closeButtons != null)
        {
            foreach (Button button in closeButtons)
            {
                if (button == null)
                    continue;

                button.onClick.RemoveListener(CloseShop);
                button.onClick.RemoveListener(Close);
                button.onClick.AddListener(CloseShop);
            }
        }

        closeButtonsRegistered = true;
    }

    private void UnregisterCloseButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseShop);
            closeButton.onClick.RemoveListener(Close);
        }

        if (closeButtons != null)
        {
            foreach (Button button in closeButtons)
            {
                if (button == null)
                    continue;

                button.onClick.RemoveListener(CloseShop);
                button.onClick.RemoveListener(Close);
            }
        }

        closeButtonsRegistered = false;
    }

    private void PositionDialogueCamera(FarmMarketNpc npc)
    {
        if (dialogueCamera == null || npc == null)
            return;

        Transform focusTransform = npc.CameraFocusPoint;
        Vector3 focusPosition = focusTransform.position;
        Transform npcTransform = npc.transform;

        Vector3 cameraOffset =
            -npcTransform.right * (cameraOffsetFromNpc.x * dialogueSideOffsetFactor) +
            Vector3.up * cameraOffsetFromNpc.y +
            npcTransform.forward * (Mathf.Abs(cameraOffsetFromNpc.z) * dialogueFrontOffsetFactor);

        Vector3 lookPosition = focusPosition + npcTransform.TransformDirection(cameraLookOffset);

        dialogueCamera.transform.position = focusPosition + cameraOffset;
        dialogueCamera.transform.LookAt(lookPosition);
        FrameWorldPointAtViewportPosition(lookPosition, npcViewportPosition);
    }

    private void FrameWorldPointAtViewportPosition(Vector3 worldPoint, Vector2 viewportPosition)
    {
        if (dialogueCamera == null)
            return;

        viewportPosition.x = Mathf.Clamp01(viewportPosition.x);
        viewportPosition.y = Mathf.Clamp01(viewportPosition.y);

        Vector3 viewportPoint = dialogueCamera.WorldToViewportPoint(worldPoint);
        Vector3 desiredWorldPoint = dialogueCamera.ViewportToWorldPoint(new Vector3(
            viewportPosition.x,
            viewportPosition.y,
            viewportPoint.z));

        dialogueCamera.transform.position += worldPoint - desiredWorldPoint;
    }

    private void RefreshShop()
    {
        CacheReferencesIfNeeded();

        ClearRows(buyContentRoot);
        ClearRows(sellContentRoot);
        ClearRows(upgradeContentRoot);
        if (licenseContentRoot != null) ClearRows(licenseContentRoot);
        UpdateMoneyText();

        if (currentNpc == null)
            return;

        bool openBuy     = currentNpc.TradeMode == FarmMarketNpcTradeMode.BuySeeds;
        bool openSell    = currentNpc.TradeMode == FarmMarketNpcTradeMode.SellInventory;
        bool openUpgrade = currentNpc.TradeMode == FarmMarketNpcTradeMode.ToolUpgrade;
        bool openLicense = currentNpc.TradeMode == FarmMarketNpcTradeMode.Licenses;

        // Fällt aufs Upgrade-Panel zurück wenn kein eigenes zugewiesen ist.
        GameObject licenseHost = licensePanel != null ? licensePanel : upgradePanel;

        if (buyPanel != null)  buyPanel.SetActive(openBuy);
        if (sellPanel != null) sellPanel.SetActive(openSell);

        if (upgradePanel != null)
            upgradePanel.SetActive(openUpgrade || (openLicense && licenseHost == upgradePanel));

        if (licensePanel != null && licensePanel != upgradePanel)
            licensePanel.SetActive(openLicense);

        if (openBuy)          BuildBuyRows();
        else if (openSell)    BuildSellRows();
        else if (openUpgrade) BuildUpgradeRows();
        else if (openLicense) BuildLicenseRows();
    }

    /// <summary>
    /// "Stufe 10: Fläche 2×2" — der nächste Meilenstein als Sparziel.
    /// Ohne diese Ansage sieht der Spieler nur eine Zahl steigen und merkt nicht,
    /// dass bei Stufe 10 der Durchsatz auf einen Schlag vierfach wird.
    /// </summary>
    private static string DescribeNextMilestone(ToolData data, int currentLevel)
    {
        if (data?.milestones == null) return null;

        ToolMilestone next = null;
        foreach (var m in data.milestones)
        {
            if (m == null || m.level <= currentLevel) continue;
            if (next == null || m.level < next.level) next = m;
        }

        if (next == null) return null;

        if (!string.IsNullOrWhiteSpace(next.unlockText))
            return $"Stufe {next.level}: {next.unlockText}";

        if (next.aoSize > 0)   return $"Stufe {next.level}: Fläche {next.aoSize}×{next.aoSize}";
        if (next.wateringPower > 1) return $"Stufe {next.level}: Gießt {next.wateringPower}× pro Einsatz";
        if (next.queueSize > 0) return $"Stufe {next.level}: Warteschlange {next.queueSize}";
        if (next.yieldBonus > 0) return $"Stufe {next.level}: +{next.yieldBonus} Ertrag";

        return $"Stufe {next.level}: Meilenstein";
    }

    private Transform LicenseRoot =>
        licenseContentRoot != null ? licenseContentRoot : upgradeContentRoot;

    /// <summary>
    /// Baut das Lizenz-Regal. Zeigt nur, was gerade erhältlich ist — gekaufte und noch
    /// gesperrte Lizenzen tauchen gar nicht auf, damit das Amt nicht wie eine Wunschliste
    /// wirkt.
    /// </summary>
    private void BuildLicenseRows()
    {
        Transform root = LicenseRoot;

        if (root == null || rowPrefab == null || LicenseRegistry.Instance == null)
        {
            Debug.LogWarning("[FarmMarketDialogueShopController] Lizenzen: ContentRoot, RowPrefab oder LicenseRegistry fehlt.");
            return;
        }

        var available = LicenseRegistry.Instance.GetAvailable();

        if (available.Count == 0)
        {
            FarmMarketShopRowUI empty = Instantiate(rowPrefab, root);
            empty.Setup(null, "Nichts zu vergeben",
                        "Komm wieder, wenn du weiter bist.",
                        string.Empty, "—", null, string.Empty, null);
            return;
        }

        foreach (LicenseData license in available)
        {
            LicenseData captured = license;
            bool affordable = inventory != null && inventory.Money >= license.price;

            FarmMarketShopRowUI row = Instantiate(rowPrefab, root);
            row.Setup(
                license.icon,
                string.IsNullOrWhiteSpace(license.displayName) ? license.licenseId : license.displayName,
                license.description,
                $"{license.price} G",
                affordable ? "Kaufen" : "Zu teuer",
                affordable ? () => BuyLicense(captured) : null,
                string.Empty,
                null);
        }
    }

    private void BuyLicense(LicenseData license)
    {
        if (license == null || LicenseRegistry.Instance == null) return;

        if (!LicenseRegistry.Instance.TryBuy(license))
        {
            SetStatus("Dafür reicht das Geld nicht.");
            UiSfx.Denied();
            return;
        }

        SetStatus($"{license.displayName} erworben!");
        UiSfx.Purchase();

        if (saveAfterTrade)
            FarmSaveManager.Instance?.RequestSave();

        RefreshShop();
    }

    private void BuildBuyRows()
    {
        if (buyPanel == null)
            Debug.LogWarning("[FarmMarketDialogueShopController] BuyPanel ist nicht zugewiesen.");

        if (buyContentRoot == null)
        {
            Debug.LogWarning("[FarmMarketDialogueShopController] BuyContentRoot ist nicht zugewiesen.");
            return;
        }

        if (rowPrefab == null)
        {
            Debug.LogWarning("[FarmMarketDialogueShopController] RowPrefab ist nicht zugewiesen.");
            return;
        }

        if (currentNpc == null || inventory == null)
            return;

        bool createdAnyRow = false;

        foreach (PlantType plant in currentNpc.SeedsForSale)
        {
            if (plant == null)
                continue;

            int buyPrice = Mathf.Max(0, plant.seedPrice);
            string displayName = GetPlantDisplayName(plant) + " Seed";

            // Gesperrte Sorten bleiben im Regal stehen, nur ausgegraut. Sie zu verstecken
            // würde dem Spieler verschweigen, dass es überhaupt mehr gibt — und genau das
            // ist der Anreiz, die Lizenz zu kaufen.
            bool unlocked = LicenseRegistry.Instance == null
                         || LicenseRegistry.Instance.IsPlantUnlocked(plant);

            FarmMarketShopRowUI row = Instantiate(rowPrefab, buyContentRoot);
            row.Setup(
                GetSeedUiSprite(plant) ?? plant.icon,
                displayName,
                unlocked ? $"Besitzt: {inventory.GetSeedCount(plant)}" : RequiredLicenseHint(plant),
                unlocked ? $"Preis: {buyPrice}" : "Gesperrt",
                unlocked ? "Kaufen" : "Lizenz nötig",
                unlocked ? () => BuySeed(plant, 1) : null,
                unlocked ? "10x" : string.Empty,
                unlocked ? () => BuySeed(plant, 10) : null);

            row.SetDimmed(!unlocked);

            createdAnyRow = true;
        }

        if (!createdAnyRow)
            SetStatus("Dieser NPC verkauft aktuell keine Samen.");
    }

    /// <summary>Welche Lizenz für diese Pflanze fehlt — sagt dem Spieler wonach er suchen soll.</summary>
    private static string RequiredLicenseHint(PlantType plant)
    {
        if (LicenseRegistry.Instance == null) return "Lizenz erforderlich";

        foreach (LicenseData license in LicenseRegistry.Instance.All)
        {
            if (license == null || !license.Unlocks(plant)) continue;
            if (LicenseRegistry.Instance.IsOwned(license)) continue;

            string name = string.IsNullOrWhiteSpace(license.displayName)
                ? license.licenseId
                : license.displayName;

            return $"Braucht: {name}";
        }

        return "Lizenz erforderlich";
    }

    private void BuildSellRows()
    {
        if (sellPanel == null)
            Debug.LogWarning("[FarmMarketDialogueShopController] SellPanel ist nicht zugewiesen.");

        if (sellContentRoot == null)
        {
            Debug.LogWarning("[FarmMarketDialogueShopController] SellContentRoot ist nicht zugewiesen.");
            return;
        }

        if (rowPrefab == null)
        {
            Debug.LogWarning("[FarmMarketDialogueShopController] RowPrefab ist nicht zugewiesen.");
            return;
        }

        if (inventory == null)
            return;

        bool createdAnyRow = false;

        if (allowSellingCrops)
        {
            foreach (KeyValuePair<PlantType, int> kvp in inventory.GetAllCrops())
            {
                PlantType plant = kvp.Key;
                int amount = kvp.Value;

                if (plant == null || amount <= 0)
                    continue;

                // Über das Inventar, nicht plant.sellPrice: im entspannten Tempo bekommt der
                // Spieler weniger, und im Regal soll stehen was er wirklich kriegt.
                int sellPrice = Mathf.Max(1, inventory.GetSellValue(plant, 1));
                string displayName = GetPlantDisplayName(plant) + " Crop";

                FarmMarketShopRowUI row = Instantiate(rowPrefab, sellContentRoot);
                row.Setup(
                    plant.icon,
                    displayName,
                    $"Besitzt: {amount}",
                    $"Verkauf: {sellPrice}",
                    "1 verkaufen",
                    () => SellCrop(plant, 1, (RectTransform)row.transform),
                    "Alle",
                    () => SellCrop(plant, inventory.GetCropCount(plant), (RectTransform)row.transform));

                createdAnyRow = true;
            }
        }

        if (allowSellingSeeds)
        {
            foreach (KeyValuePair<PlantType, int> kvp in inventory.GetAllSeeds())
            {
                PlantType plant = kvp.Key;
                int amount = kvp.Value;

                if (plant == null || amount <= 0)
                    continue;

                int sellPrice = GetSeedSellPrice(plant);
                string displayName = GetPlantDisplayName(plant) + " Seed";

                FarmMarketShopRowUI row = Instantiate(rowPrefab, sellContentRoot);
                row.Setup(
                    GetSeedUiSprite(plant) ?? plant.icon,
                    displayName,
                    $"Besitzt: {amount}",
                    $"Verkauf: {sellPrice}",
                    "1 verkaufen",
                    () => SellSeed(plant, 1, (RectTransform)row.transform),
                    "Alle",
                    () => SellSeed(plant, inventory.GetSeedCount(plant), (RectTransform)row.transform));

                createdAnyRow = true;
            }
        }

        if (!createdAnyRow)
            SetStatus("Du hast aktuell keine Items zum Verkaufen.");
    }

    private void BuildUpgradeRows()
    {
        if (upgradeContentRoot == null || rowPrefab == null || ToolRegistry.Instance == null)
        {
            Debug.LogWarning("[FarmMarketDialogueShopController] Upgrade: upgradeContentRoot, rowPrefab oder ToolRegistry fehlt.");
            return;
        }

        ToolType[] upgradableTools = { ToolType.Hoe, ToolType.WateringCan, ToolType.Scythe, ToolType.Seed };

        foreach (ToolType tool in upgradableTools)
        {
            ToolData data = ToolRegistry.Instance.GetData(tool);
            if (data == null) continue;

            bool owned = ToolRegistry.Instance != null && ToolRegistry.Instance.IsOwned(tool);
            ToolType captured = tool;

            if (!owned)
            {
                // Tool noch nicht gekauft — Basis-Stats anzeigen
                int  ao  = data.GetAoSize(0);
                float dur = data.GetDuration(0);
                string baseStats = $"AoE: {ao}×{ao}  |  {dur:F1}s/Tile";

                FarmMarketShopRowUI row = Instantiate(rowPrefab, upgradeContentRoot);
                row.Setup(
                    data.icon,
                    data.displayName,
                    baseStats,
                    $"Kaufen: {data.buyPrice} G",
                    "Kaufen",
                    () => BuyTool(captured),
                    string.Empty,
                    null);
            }
            else
            {
                // Tool bereits vorhanden → Upgrade anzeigen
                int level   = ToolRegistry.Instance.GetLevel(tool);
                int maxLevel = data.maxLevel;
                int cost    = ToolRegistry.Instance.GetUpgradeCost(tool);
                bool isMax  = ToolRegistry.Instance.IsMaxLevel(tool);

                int  ao       = data.GetAoSize(level);
                float dur     = data.GetDuration(level);
                int  yBonus   = data.GetYieldBonus(level);

                int queue = data.GetQueueSize(level);

                string statsLabel = $"Lvl {level}/{maxLevel}  |  AoE: {ao}×{ao}  |  {dur:F1}s/Tile"
                                  + $"  |  Queue: {queue}"
                                  + (yBonus > 0 ? $"  |  +{yBonus} Ertrag" : "");

                // Nächsten Meilenstein ankündigen — das Sparen auf Stufe 10 soll sich
                // wie ein Ziel anfühlen, nicht wie eine Zahl die langsam hochgeht.
                string nextMilestone = DescribeNextMilestone(data, level);
                if (!string.IsNullOrEmpty(nextMilestone))
                    statsLabel += $"\n<color=#8A5A1E>{nextMilestone}</color>";

                string costLabel  = isMax ? "MAX LEVEL" : $"Upgrade: {cost} G";

                FarmMarketShopRowUI row = Instantiate(rowPrefab, upgradeContentRoot);
                row.Setup(
                    data.icon,
                    data.displayName,
                    statsLabel,
                    costLabel,
                    isMax ? "—" : "Upgraden",
                    isMax ? null : () => UpgradeTool(captured),
                    string.Empty,
                    null);
            }
        }
    }

    private void BuyTool(ToolType tool)
    {
        if (TutorialManager.Instance?.IsBlocked(TutorialBlockedAction.BuyTool) == true)
        {
            SetStatus("Tutorial: Schließe zuerst den aktuellen Schritt ab.");
            return;
        }

        if (ToolRegistry.Instance == null || inventory == null) return;

        ToolData data = ToolRegistry.Instance.GetData(tool);
        if (data == null) return;

        if (!inventory.TrySpendMoney(data.buyPrice))
        {
            SetStatus($"Nicht genug Geld. Benötigt: {data.buyPrice} G");
            UpdateMoneyText();
            return;
        }

        ToolRegistry.Instance.OwnTool(tool);
        SetStatus($"{data.displayName} freigeschaltet! Kehre zur Farm zurück um es zu benutzen.");
        SaveAfterTradeIfNeeded();
        ClearRows(upgradeContentRoot);
        BuildUpgradeRows();
        UpdateMoneyText();
    }

    private void UpgradeTool(ToolType tool)
    {
        if (TutorialManager.Instance?.IsBlocked(TutorialBlockedAction.UpgradeTool) == true)
        {
            SetStatus("Tutorial: Schließe zuerst den aktuellen Schritt ab.");
            return;
        }

        if (ToolRegistry.Instance == null || inventory == null) return;

        int cost = ToolRegistry.Instance.GetUpgradeCost(tool);
        if (cost < 0) { SetStatus("Bereits auf MAX LEVEL."); return; }

        if (!inventory.TrySpendMoney(cost))
        {
            SetStatus($"Nicht genug Geld. Benötigt: {cost} G");
            UpdateMoneyText();
            return;
        }

        ToolRegistry.Instance.TryUpgrade(tool);

        ToolData data = ToolRegistry.Instance.GetData(tool);
        SetStatus($"{data?.displayName ?? tool.ToString()} → Lvl {ToolRegistry.Instance.GetLevel(tool)}");
        SaveAfterTradeIfNeeded();
        ClearRows(upgradeContentRoot);
        BuildUpgradeRows();
        UpdateMoneyText();
    }

    private void BuySeed(PlantType plant, int amount)
    {
        if (TutorialManager.Instance?.IsBlocked(TutorialBlockedAction.BuySeed) == true)
        {
            SetStatus("Tutorial: Schließe zuerst den aktuellen Schritt ab.");
            return;
        }

        if (inventory == null || plant == null || amount <= 0)
            return;

        int unitPrice = Mathf.Max(1, plant.seedPrice);

        // So viele wie bezahlbar statt gar keine. Auf "10x" zu klicken und nichts zu
        // bekommen, obwohl das Geld für sieben reicht, liest sich wie ein defekter Knopf —
        // der Spieler rechnet nicht nach, er klickt nochmal.
        int affordable = Mathf.Min(amount, inventory.Money / unitPrice);

        if (affordable <= 0)
        {
            SetStatus($"Nicht genug Geld. Ein Samen kostet {unitPrice}.");
            UiSfx.Denied();
            UpdateMoneyText();
            return;
        }

        if (!inventory.TryBuySeed(plant, affordable))
        {
            SetStatus($"Nicht genug Geld. Benötigt: {unitPrice * affordable}");
            UiSfx.Denied();
            UpdateMoneyText();
            return;
        }

        SetStatus(affordable < amount
            ? $"Gekauft: {affordable}x {GetPlantDisplayName(plant)} Seed — mehr war nicht drin."
            : $"Gekauft: {affordable}x {GetPlantDisplayName(plant)} Seed.");

        SaveAfterTradeIfNeeded();
        RefreshShop();
    }

    private void SellCrop(PlantType plant, int amount, RectTransform origin = null)
    {
        if (TutorialManager.Instance?.IsBlocked(TutorialBlockedAction.SellItem) == true)
        {
            SetStatus("Tutorial: Schließe zuerst den aktuellen Schritt ab.");
            return;
        }

        if (inventory == null || plant == null || amount <= 0)
            return;

        int available = inventory.GetCropCount(plant);
        int amountToSell = Mathf.Min(amount, available);

        if (amountToSell <= 0)
        {
            SetStatus("Davon hast du nichts zum Verkaufen.");
            return;
        }

        // Vor dem Verkauf ermitteln — danach ist die Ware weg und der Wert nicht mehr abfragbar.
        int gold = inventory.GetSellValue(plant, amountToSell);

        // TrySellCrop bucht das Geld selbst, der Zähler würde also sofort loslaufen.
        // Deshalb muss der Vorlauf davor angemeldet sein.
        bool fly = AnnounceCoinFlight(origin, gold);

        if (!inventory.TrySellCrop(plant, amountToSell))
        {
            if (fly) MoneyDisplay.Instance.ClearPendingGainDelay();

            SetStatus("Verkauf fehlgeschlagen.");
            return;
        }

        SetStatus($"Verkauft: {amountToSell}x {GetPlantDisplayName(plant)} Crop.");

        // Vor RefreshShop: dabei werden die Zeilen neu gebaut, und origin zeigt danach auf
        // ein zerstörtes Objekt.
        if (fly) RewardCollectFx.PlaySale(origin, gold, coinFlight, () => CropSfx.PlaySell(plant));
        else     CropSfx.PlaySell(plant);

        SaveAfterTradeIfNeeded();
        RefreshShop();
    }

    private void SellSeed(PlantType plant, int amount, RectTransform origin = null)
    {
        if (TutorialManager.Instance?.IsBlocked(TutorialBlockedAction.SellItem) == true)
        {
            SetStatus("Tutorial: Schließe zuerst den aktuellen Schritt ab.");
            return;
        }

        if (inventory == null || plant == null || amount <= 0)
            return;

        int available = inventory.GetSeedCount(plant);
        int amountToSell = Mathf.Min(amount, available);

        if (amountToSell <= 0)
        {
            SetStatus("Davon hast du nichts zum Verkaufen.");
            return;
        }

        int actuallySold = 0;
        for (int i = 0; i < amountToSell; i++)
        {
            if (!inventory.TryUseSeed(plant))
                break;

            actuallySold++;
        }

        if (actuallySold <= 0)
        {
            SetStatus("Seed-Verkauf fehlgeschlagen.");
            return;
        }

        int moneyGained = actuallySold * GetSeedSellPrice(plant);

        bool fly = AnnounceCoinFlight(origin, moneyGained);
        inventory.AddMoney(moneyGained);

        SetStatus($"Verkauft: {actuallySold}x {GetPlantDisplayName(plant)} Seed für {moneyGained}.");

        if (fly) RewardCollectFx.PlaySale(origin, moneyGained, coinFlight, () => CropSfx.PlaySell(plant));
        else     CropSfx.PlaySell(plant);

        SaveAfterTradeIfNeeded();
        RefreshShop();
    }

    /// <summary>
    /// Prüft, ob ein Münzflug möglich ist, und meldet in dem Fall den passenden Vorlauf beim
    /// Geldzähler an. Gibt zurück, ob der Flug tatsächlich gestartet werden soll.
    ///
    /// Beides gehört zusammen: verzögert man den Zähler, ohne dass Münzen fliegen, steht er
    /// eine Sekunde grundlos still.
    /// </summary>
    private bool AnnounceCoinFlight(RectTransform origin, int gold)
    {
        if (!coinFlightOnSell || origin == null || gold <= 0) return false;

        var money = MoneyDisplay.Instance;
        if (money == null || money.CoinSprite == null) return false;

        money.DelayNextGain(coinFlight.FirstArrivalTime);
        return true;
    }

    private int GetSeedSellPrice(PlantType plant)
    {
        if (plant == null)
            return 1;

        return Mathf.Max(1, Mathf.RoundToInt(plant.seedPrice * seedSellPriceFactor));
    }

    private string GetPlantDisplayName(PlantType plant)
    {
        if (plant == null)
            return "Unknown";

        if (!string.IsNullOrWhiteSpace(plant.plantName))
            return plant.plantName;

        return plant.name;
    }

    private void SaveAfterTradeIfNeeded()
    {
        if (!saveAfterTrade)
            return;

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.SaveNow();
    }

    private void UpdateMoneyText()
    {
        if (moneyText != null)
            moneyText.text = inventory != null ? $"Money: {inventory.Money}" : "Money: -";
    }

    private void SetStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;
    }

    private Sprite GetSeedUiSprite(PlantType plant)
    {
        if (plant == null) return null;
        return plant.plantName switch
        {
            "Carrot" => carrotSeedSprite,
            "Cauliflower" => cauliflowerSeedSprite,
            "Sunflower" => sunflowerSeedSprite,
            _ => null
        };
    }

    private void ClearRows(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }

    private bool WasEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}

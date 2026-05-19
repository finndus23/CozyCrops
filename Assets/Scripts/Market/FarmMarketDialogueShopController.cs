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
/// </summary>
public class FarmMarketDialogueShopController : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Camera dialogueCamera;
    [SerializeField] private bool autoPositionDialogueCamera = true;
    [SerializeField] private Vector3 cameraOffsetFromNpc = new Vector3(2.4f, 1.6f, -3.0f);
    [SerializeField] private Vector3 cameraLookOffset = new Vector3(0.8f, 0.7f, 0f);

    [Header("Root UI")]
    [SerializeField] private GameObject dialogueRoot;
    [SerializeField] private Button closeButton;

    [Header("NPC Text")]
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private TMP_Text speechBubbleText;

    [Header("Shared Header")]
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text statusText;

    [Header("Separate UIs")]
    [SerializeField] private GameObject buyPanel;
    [SerializeField] private GameObject sellPanel;

    [Header("Content Roots")]
    [SerializeField] private Transform buyContentRoot;
    [SerializeField] private Transform sellContentRoot;
    [SerializeField] private FarmMarketShopRowUI rowPrefab;

    [Header("Data")]
    [SerializeField] private PlayerInventory inventory;

    [Header("Trade Behaviour")]
    [SerializeField] private bool saveAfterTrade = true;
    [SerializeField] private bool allowSellingSeeds = true;
    [SerializeField] private bool allowSellingCrops = true;
    [SerializeField] private float seedSellPriceFactor = 0.5f;

    private FarmMarketNpc currentNpc;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        if (inventory == null)
            inventory = PlayerInventory.Instance;

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        CloseInstant();
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (WasEscapePressed())
            Close();
    }

    public void Open(FarmMarketNpc npc)
    {
        if (npc == null)
            return;

        if (inventory == null)
            inventory = PlayerInventory.Instance;

        if (inventory == null)
        {
            Debug.LogWarning("[FarmMarketDialogueShopController] Kein PlayerInventory gefunden. Lege in der MarketScene ein GameObject mit PlayerInventory an.");
            return;
        }

        currentNpc = npc;
        IsOpen = true;

        if (dialogueRoot != null)
            dialogueRoot.SetActive(true);

        if (gameplayCamera != null)
            gameplayCamera.enabled = false;

        if (dialogueCamera != null)
        {
            if (autoPositionDialogueCamera)
                PositionDialogueCamera(npc);

            dialogueCamera.enabled = true;
        }

        if (npcNameText != null)
            npcNameText.text = npc.DisplayName;

        if (speechBubbleText != null)
            speechBubbleText.text = npc.SpeechText;

        SetStatus(string.Empty);
        RefreshShop();
    }

    public void Close()
    {
        IsOpen = false;
        currentNpc = null;

        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);

        if (dialogueCamera != null)
            dialogueCamera.enabled = false;

        if (gameplayCamera != null)
            gameplayCamera.enabled = true;

        ClearRows(buyContentRoot);
        ClearRows(sellContentRoot);
    }

    private void CloseInstant()
    {
        IsOpen = false;

        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);

        if (dialogueCamera != null)
            dialogueCamera.enabled = false;

        if (buyPanel != null)
            buyPanel.SetActive(false);

        if (sellPanel != null)
            sellPanel.SetActive(false);
    }

    private void PositionDialogueCamera(FarmMarketNpc npc)
    {
        if (dialogueCamera == null || npc == null)
            return;

        Transform focusTransform = npc.CameraFocusPoint;
        Vector3 focusPosition = focusTransform.position;
        Transform npcTransform = npc.transform;

        dialogueCamera.transform.position = focusPosition + npcTransform.TransformDirection(cameraOffsetFromNpc);
        dialogueCamera.transform.LookAt(focusPosition + npcTransform.TransformDirection(cameraLookOffset));
    }

    private void RefreshShop()
    {
        if (inventory == null)
            inventory = PlayerInventory.Instance;

        ClearRows(buyContentRoot);
        ClearRows(sellContentRoot);
        UpdateMoneyText();

        if (currentNpc == null)
            return;

        bool openBuy = currentNpc.TradeMode == FarmMarketNpcTradeMode.BuySeeds;
        bool openSell = currentNpc.TradeMode == FarmMarketNpcTradeMode.SellInventory;

        if (buyPanel != null)
            buyPanel.SetActive(openBuy);

        if (sellPanel != null)
            sellPanel.SetActive(openSell);

        if (openBuy)
            BuildBuyRows();
        else if (openSell)
            BuildSellRows();
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

            FarmMarketShopRowUI row = Instantiate(rowPrefab, buyContentRoot);
            row.Setup(
                plant.icon,
                displayName,
                $"Besitzt: {inventory.GetSeedCount(plant)}",
                $"Preis: {buyPrice}",
                "Kaufen",
                () => BuySeed(plant, 1),
                "10x",
                () => BuySeed(plant, 10));

            createdAnyRow = true;
        }

        if (!createdAnyRow)
            SetStatus("Dieser NPC verkauft aktuell keine Samen.");
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

                int sellPrice = Mathf.Max(1, plant.sellPrice);
                string displayName = GetPlantDisplayName(plant) + " Crop";

                FarmMarketShopRowUI row = Instantiate(rowPrefab, sellContentRoot);
                row.Setup(
                    plant.icon,
                    displayName,
                    $"Besitzt: {amount}",
                    $"Verkauf: {sellPrice}",
                    "1 verkaufen",
                    () => SellCrop(plant, 1),
                    "Alle",
                    () => SellCrop(plant, inventory.GetCropCount(plant)));

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
                    plant.icon,
                    displayName,
                    $"Besitzt: {amount}",
                    $"Verkauf: {sellPrice}",
                    "1 verkaufen",
                    () => SellSeed(plant, 1),
                    "Alle",
                    () => SellSeed(plant, inventory.GetSeedCount(plant)));

                createdAnyRow = true;
            }
        }

        if (!createdAnyRow)
            SetStatus("Du hast aktuell keine Items zum Verkaufen.");
    }

    private void BuySeed(PlantType plant, int amount)
    {
        if (inventory == null || plant == null || amount <= 0)
            return;

        int totalPrice = Mathf.Max(0, plant.seedPrice) * amount;

        if (!inventory.TryBuySeed(plant, amount))
        {
            SetStatus($"Nicht genug Geld. Benötigt: {totalPrice}");
            UpdateMoneyText();
            return;
        }

        SetStatus($"Gekauft: {amount}x {GetPlantDisplayName(plant)} Seed.");
        SaveAfterTradeIfNeeded();
        RefreshShop();
    }

    private void SellCrop(PlantType plant, int amount)
    {
        if (inventory == null || plant == null || amount <= 0)
            return;

        int available = inventory.GetCropCount(plant);
        int amountToSell = Mathf.Min(amount, available);

        if (amountToSell <= 0)
        {
            SetStatus("Davon hast du nichts zum Verkaufen.");
            return;
        }

        if (!inventory.TrySellCrop(plant, amountToSell))
        {
            SetStatus("Verkauf fehlgeschlagen.");
            return;
        }

        SetStatus($"Verkauft: {amountToSell}x {GetPlantDisplayName(plant)} Crop.");
        SaveAfterTradeIfNeeded();
        RefreshShop();
    }

    private void SellSeed(PlantType plant, int amount)
    {
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
        inventory.AddMoney(moneyGained);

        SetStatus($"Verkauft: {actuallySold}x {GetPlantDisplayName(plant)} Seed für {moneyGained}.");
        SaveAfterTradeIfNeeded();
        RefreshShop();
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

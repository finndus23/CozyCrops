using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [System.Serializable]
    private class StartingSeed
    {
        public PlantType plantType;
        public int amount = 5;
    }

    [Header("Startwerte")]
    [SerializeField] private int startingMoney = 100;

    [Header("Startseeds (zum Testen)")]
    [SerializeField] private StartingSeed[] startingSeeds;

    private int money;
    private int fertilizer;
    private readonly Dictionary<PlantType, int> seeds = new();
    private readonly Dictionary<PlantType, int> crops = new();

    public int Money => money;

    /// <summary>Dünger aus dem Komposter. Eigene Ressource, kein PlantType — wie Money.</summary>
    public int Fertilizer => fertilizer;

    // Events — UI kann sich hier einhängen
    public event Action<int> OnMoneyChanged;
    public event Action<int> OnFertilizerChanged;
    public event Action<PlantType, int> OnSeedsChanged;
    public event Action<PlantType, int> OnCropsChanged;

    // Statische Events für Mission-System
    public static event Action<PlantType, int> OnCropSoldStatic;
    public static event Action<PlantType, int> OnSeedBoughtStatic;

    /// <summary>Betrag, der dem Spieler gutgeschrieben wurde. Quelle für EarnMoney-Objectives.</summary>
    public static event Action<int> OnMoneyEarnedStatic;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        money = startingMoney;

        // Startwerte direkt setzen, ohne FarmSaveManager.RequestSave() auszulösen.
        // Sonst kann der Unity-Editor beim neuen Playmode-Start einen vorhandenen Spielstand
        // mit Default-Inventar überschreiben, bevor F6 geladen wurde.
        foreach (var s in startingSeeds)
        {
            if (s.plantType == null || s.amount <= 0) continue;

            if (!seeds.ContainsKey(s.plantType))
                seeds.Add(s.plantType, 0);

            seeds[s.plantType] += s.amount;
        }
    }

    // --- Geld ---

    public bool TrySpendMoney(int amount)
    {
        if (money < amount) return false;
        money -= amount;
        OnMoneyChanged?.Invoke(money);

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();

        return true;
    }

    public void AddMoney(int amount)
    {
        money += amount;
        OnMoneyChanged?.Invoke(money);

        // Nur echte Einnahmen melden. Der MissionObjectiveType EarnMoney hatte bisher
        // gar keine Quelle — Missionen mit "Verdiene X Gold" wären nie fertig geworden.
        if (amount > 0)
            OnMoneyEarnedStatic?.Invoke(amount);

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();
    }

    // --- Dünger ---

    public bool TrySpendFertilizer(int amount)
    {
        if (fertilizer < amount) return false;
        fertilizer -= amount;
        OnFertilizerChanged?.Invoke(fertilizer);

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();

        return true;
    }

    public void AddFertilizer(int amount)
    {
        if (amount <= 0) return;

        fertilizer += amount;
        OnFertilizerChanged?.Invoke(fertilizer);

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();
    }

    // --- Seeds ---

    public int GetSeedCount(PlantType type) =>
        seeds.TryGetValue(type, out int count) ? count : 0;

    public void AddSeed(PlantType type, int amount = 1)
    {
        if (type == null || amount <= 0) return;

        seeds[type] = GetSeedCount(type) + amount;
        OnSeedsChanged?.Invoke(type, seeds[type]);

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();
    }

    public bool TryUseSeed(PlantType type)
    {
        if (GetSeedCount(type) <= 0) return false;
        seeds[type]--;
        OnSeedsChanged?.Invoke(type, seeds[type]);

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();

        return true;
    }

    // --- Crops ---

    public int GetCropCount(PlantType type) =>
        crops.TryGetValue(type, out int count) ? count : 0;

    public void AddCrop(PlantType type, int amount = 1)
    {
        if (type == null || amount <= 0) return;

        crops[type] = GetCropCount(type) + amount;
        OnCropsChanged?.Invoke(type, crops[type]);

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();
    }

    /// <summary>
    /// Gewähltes Spieltempo, kommt aus dem Savegame. Wirkt ausschließlich auf
    /// Verkaufserlöse — Ausgaben bleiben in beiden Modi identisch, damit im Shop
    /// überall dieselben Zahlen stehen.
    /// </summary>
    public GamePace Pace { get; set; } = GamePace.Normal;

    /// <summary>Was dieser Verkauf tatsächlich einbringt, inklusive Tempo-Faktor.</summary>
    public int GetSellValue(PlantType type, int amount = 1) =>
        type == null ? 0 : Pace.ApplyToSale(type.sellPrice * amount);

    public bool TrySellCrop(PlantType type, int amount = 1)
    {
        if (GetCropCount(type) < amount) return false;
        crops[type] -= amount;
        AddMoney(GetSellValue(type, amount));
        OnCropsChanged?.Invoke(type, crops[type]);

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();

        OnCropSoldStatic?.Invoke(type, amount);
        return true;
    }

    public IReadOnlyDictionary<PlantType, int> GetAllSeeds() => seeds;
    public IReadOnlyDictionary<PlantType, int> GetAllCrops() => crops;

    /// <summary>
    /// Nimmt exakt <paramref name="amount"/> einer bestimmten Frucht aus dem Inventar —
    /// für den Komposter, der (anders als der Verkauf) kein Geld gutschreibt. Analog zu
    /// <see cref="TrySellCrop"/>, nur ohne die Bezahlung.
    /// </summary>
    public bool TryRemoveCrop(PlantType type, int amount)
    {
        if (type == null || amount <= 0) return false;
        if (GetCropCount(type) < amount) return false;

        crops[type] -= amount;
        OnCropsChanged?.Invoke(type, crops[type]);

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();

        return true;
    }

    // --- Save-/Load-Hilfen ---

    public void ApplyLoadedData(
        int loadedMoney,
        List<InventoryStackSaveData> loadedSeeds,
        List<InventoryStackSaveData> loadedCrops,
        int loadedFertilizer = 0)
    {
        money = loadedMoney;
        fertilizer = loadedFertilizer;
        seeds.Clear();
        crops.Clear();

        ApplyLoadedStacks(loadedSeeds, seeds, true);
        ApplyLoadedStacks(loadedCrops, crops, false);

        OnMoneyChanged?.Invoke(money);
        OnFertilizerChanged?.Invoke(fertilizer);

        foreach (var kvp in seeds)
            OnSeedsChanged?.Invoke(kvp.Key, kvp.Value);

        foreach (var kvp in crops)
            OnCropsChanged?.Invoke(kvp.Key, kvp.Value);

        Debug.Log($"[PlayerInventory] Inventar geladen. Money={money}, Fertilizer={fertilizer}, Seeds={seeds.Count}, Crops={crops.Count}");
    }

    private void ApplyLoadedStacks(
        List<InventoryStackSaveData> loadedStacks,
        Dictionary<PlantType, int> target,
        bool isSeedStack)
    {
        if (loadedStacks == null) return;

        if (PlantDatabase.Instance == null)
        {
            Debug.LogWarning("[PlayerInventory] Kein PlantDatabase in der Scene gefunden. Inventar-Items konnten nicht geladen werden.");
            return;
        }

        foreach (InventoryStackSaveData stack in loadedStacks)
        {
            if (stack == null) continue;
            if (stack.amount <= 0) continue;

            PlantType plantType = PlantDatabase.Instance.GetById(stack.plantId);
            if (plantType == null)
            {
                string stackType = isSeedStack ? "Seed" : "Crop";
                Debug.LogWarning($"[PlayerInventory] {stackType} PlantType mit SaveId '{stack.plantId}' nicht gefunden.");
                continue;
            }

            if (!target.ContainsKey(plantType))
                target.Add(plantType, 0);

            target[plantType] += stack.amount;
        }
    }

    // --- Shop-Shortcuts ---

    public bool TryBuySeed(PlantType type, int amount = 1)
    {
        if (!TrySpendMoney(type.seedPrice * amount)) return false;
        AddSeed(type, amount);
        OnSeedBoughtStatic?.Invoke(type, amount);
        return true;
    }
}

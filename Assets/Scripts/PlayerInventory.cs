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
    private readonly Dictionary<PlantType, int> seeds = new();
    private readonly Dictionary<PlantType, int> crops = new();

    public int Money => money;

    // Events — UI kann sich hier einhängen
    public event Action<int> OnMoneyChanged;
    public event Action<PlantType, int> OnSeedsChanged;
    public event Action<PlantType, int> OnCropsChanged;

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

    public bool TrySellCrop(PlantType type, int amount = 1)
    {
        if (GetCropCount(type) < amount) return false;
        crops[type] -= amount;
        AddMoney(type.sellPrice * amount);
        OnCropsChanged?.Invoke(type, crops[type]);

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.RequestSave();

        return true;
    }

    public IReadOnlyDictionary<PlantType, int> GetAllSeeds() => seeds;
    public IReadOnlyDictionary<PlantType, int> GetAllCrops() => crops;

    // --- Save-/Load-Hilfen ---

    public void ApplyLoadedData(
        int loadedMoney,
        List<InventoryStackSaveData> loadedSeeds,
        List<InventoryStackSaveData> loadedCrops)
    {
        money = loadedMoney;
        seeds.Clear();
        crops.Clear();

        ApplyLoadedStacks(loadedSeeds, seeds, true);
        ApplyLoadedStacks(loadedCrops, crops, false);

        OnMoneyChanged?.Invoke(money);

        foreach (var kvp in seeds)
            OnSeedsChanged?.Invoke(kvp.Key, kvp.Value);

        foreach (var kvp in crops)
            OnCropsChanged?.Invoke(kvp.Key, kvp.Value);

        Debug.Log($"[PlayerInventory] Inventar geladen. Money={money}, Seeds={seeds.Count}, Crops={crops.Count}");
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
        return true;
    }
}

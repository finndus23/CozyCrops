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

        foreach (var s in startingSeeds)
            if (s.plantType != null)
                AddSeed(s.plantType, s.amount);
    }

    // --- Geld ---

    public bool TrySpendMoney(int amount)
    {
        if (money < amount) return false;
        money -= amount;
        OnMoneyChanged?.Invoke(money);
        return true;
    }

    public void AddMoney(int amount)
    {
        money += amount;
        OnMoneyChanged?.Invoke(money);
    }

    // --- Seeds ---

    public int GetSeedCount(PlantType type) =>
        seeds.TryGetValue(type, out int count) ? count : 0;

    public void AddSeed(PlantType type, int amount = 1)
    {
        seeds[type] = GetSeedCount(type) + amount;
        OnSeedsChanged?.Invoke(type, seeds[type]);
    }

    public bool TryUseSeed(PlantType type)
    {
        if (GetSeedCount(type) <= 0) return false;
        seeds[type]--;
        OnSeedsChanged?.Invoke(type, seeds[type]);
        return true;
    }

    // --- Crops ---

    public int GetCropCount(PlantType type) =>
        crops.TryGetValue(type, out int count) ? count : 0;

    public void AddCrop(PlantType type, int amount = 1)
    {
        crops[type] = GetCropCount(type) + amount;
        OnCropsChanged?.Invoke(type, crops[type]);
    }

    public bool TrySellCrop(PlantType type, int amount = 1)
    {
        if (GetCropCount(type) < amount) return false;
        crops[type] -= amount;
        AddMoney(type.sellPrice * amount);
        OnCropsChanged?.Invoke(type, crops[type]);
        return true;
    }

    public IReadOnlyDictionary<PlantType, int> GetAllSeeds()  => seeds;
    public IReadOnlyDictionary<PlantType, int> GetAllCrops()  => crops;

    // --- Shop-Shortcuts ---

    public bool TryBuySeed(PlantType type, int amount = 1)
    {
        if (!TrySpendMoney(type.seedPrice * amount)) return false;
        AddSeed(type, amount);
        return true;
    }
}

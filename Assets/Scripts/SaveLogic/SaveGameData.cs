using System;
using System.Collections.Generic;

[Serializable]
public class SaveGameData
{
    public int version = 1;
    public int slotIndex = 1;
    public long savedAtUnix;

    public int money;

    public List<InventoryStackSaveData> seeds = new();
    public List<InventoryStackSaveData> crops = new();

    public List<TileSaveData> tiles = new();
    public List<ZoneSaveData> zones = new();
    public List<ToolLevelSaveData> toolLevels = new();
    public List<string> ownedTools = new();
}

[Serializable]
public class InventoryStackSaveData
{
    public string plantId;
    public int amount;
}

[Serializable]
public class TileSaveData
{
    public int x;
    public int z;

    public string tileType;
    public bool isLocked;
    public bool isTilled;

    public bool hasPlant;
    public string plantId;
    public int plantStageIndex;
    public float plantGrowthTimer;
    public int plantWateringsThisStage;
}

[Serializable]
public class ZoneSaveData
{
    public string zoneId;
    public bool isUnlocked;
}

[Serializable]
public class ToolLevelSaveData
{
    public string toolType;
    public int level;
}

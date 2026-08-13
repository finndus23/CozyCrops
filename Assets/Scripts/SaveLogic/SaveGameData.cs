using System;
using System.Collections.Generic;

[Serializable]
public class SaveGameData
{
    public int version = 2;
    public int slotIndex = 1;
    public long savedAtUnix;
    public string playerName = "";
    public bool isInitialized;

    public int money;

    public List<InventoryStackSaveData> seeds = new();
    public List<InventoryStackSaveData> crops = new();

    public List<TileSaveData> tiles = new();
    public List<ZoneSaveData> zones = new();
    public List<ToolLevelSaveData> toolLevels = new();
    public List<string> ownedTools = new();
    public List<MissionProgressSaveData> missionProgress = new();

    /// <summary>licenseIds der gekauften Lizenzen.</summary>
    public List<string> ownedLicenses = new();

    /// <summary>
    /// Beim Anlegen des Slots gewähltes Spieltempo. 0 = Normal, damit Altsaves
    /// automatisch auf dem balancierten Tempo landen.
    /// </summary>
    public int gamePace;
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

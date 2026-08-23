using System;
using System.Collections.Generic;

[Serializable]
public class SaveGameData
{
    public int version = 4;
    public int slotIndex = 1;
    public long savedAtUnix;
    public string playerName = "";
    public bool isInitialized;

    public int money;
    public int fertilizer;

    public List<InventoryStackSaveData> seeds = new();
    public List<InventoryStackSaveData> crops = new();

    /// <summary>Läuft der Komposter gerade? Fehlender Batch (kein Aufruf von
    /// Composter.StartBrew) heißt composterBrewing = false, alles andere 0/leer.</summary>
    public bool composterBrewing;
    public float composterTimeRemaining;
    public int composterFertilizerYield;

    /// <summary>Gesamtdauer des laufenden Brauvorgangs — nötig, damit der Fortschrittsring
    /// nach einem Neuladen weiß wie weit er ist, statt bei 0% neu anzufangen.</summary>
    public float composterTotalBrewTime;

    /// <summary>Was aktuell im Komposter brodelt, pro Sorte — nötig, damit "Abbrechen" die
    /// richtige Ernte zurückerstatten kann, auch nach einem Neuladen mitten im Brauvorgang.</summary>
    public List<InventoryStackSaveData> composterComposition = new();

    public List<TileSaveData> tiles = new();
    public List<ZoneSaveData> zones = new();
    public List<ToolLevelSaveData> toolLevels = new();
    public List<string> ownedTools = new();
    public List<MissionProgressSaveData> missionProgress = new();

    /// <summary>licenseIds der gekauften Lizenzen.</summary>
    public List<string> ownedLicenses = new();

    /// <summary>
    /// Platzierte Automatik-Geraete. Eine Liste reicht: das Level haengt am einzelnen
    /// Geraet und einen unplatzierten Bestand gibt es nicht.
    ///
    /// version bleibt bewusst bei 4 — es gibt nichts zu migrieren, JsonUtility liefert bei
    /// Altsaves einfach eine leere Liste.
    /// </summary>
    public List<AutomationDeviceSaveData> automationDevices = new();

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
    public bool isFertilized;

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

[Serializable]
public class AutomationDeviceSaveData
{
    /// <summary>AutomationDeviceType als Text — wie bei ToolLevelSaveData, damit ein
    /// Umsortieren des Enums keine Altsaves zerlegt.</summary>
    public string deviceType;

    public int x;
    public int z;

    public int level;
    public bool enabled = true;

    /// <summary>Nur die Saemaschine: gewaehlte Sorte, ueber PlantDatabase aufgeloest.</summary>
    public string seedId;

    public float cooldownRemaining;
}

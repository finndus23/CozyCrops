using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// Zentrales Save-System für Farm, Inventar, Geld, Spielstände und Szenenwechsel.
///
/// Wichtig für das neue Setup mit Marktplatz:
/// - In der FarmScene wird komplett gespeichert: Grid + Zonen + Inventar + Geld.
/// - In der MarketScene gibt es normalerweise keinen GridManager. Dort speichert SaveNow()
///   nur Geld/Inventar und übernimmt Farm-Tiles/Zonen aus der bestehenden Save-Datei.
///   Dadurch wird die Farm nicht leer überschrieben.
/// - Der FarmSaveManager sollte im MainMenu auf dem persistenten SaveSystem-Objekt liegen.
/// - PlantDatabase gehört am besten auf dasselbe SaveSystem-Objekt.
/// </summary>
public class FarmSaveManager : MonoBehaviour
{
    public static FarmSaveManager Instance { get; private set; }

    [Header("Slot")]
    [SerializeField][Range(1, 3)] private int activeSlot = 1;

    [Header("Scenes")]
    [Tooltip("Name deiner Farm/Game-Szene. Muss in File > Build Settings > Scenes In Build eingetragen sein.")]
    [SerializeField] private string defaultGameSceneName = "GameScene";

    [Tooltip("Name deiner Hauptmenü-Szene. Wird für 'Zurück ins Menü' benutzt.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Tooltip("Wie viele Frames nach dem Scene-Load mindestens gewartet wird, bevor Save-Daten angewendet werden.")]
    [SerializeField] private int framesToWaitAfterSceneLoad = 2;

    [Tooltip("Maximale Frame-Anzahl, die beim Laden auf Runtime-Objekte gewartet wird.")]
    [SerializeField] private int maxDependencyWaitFrames = 30;

    [Header("Manual Save")]
    [SerializeField] private bool saveOnApplicationQuit = false;
    [SerializeField] private bool useAutoSave = false;
    [SerializeField] private float autoSaveInterval = 10f;

    [Header("Debug Hotkeys")]
    [SerializeField] private bool enableDebugHotkeys = true;
    [SerializeField] private Key saveKey = Key.F5;
    [SerializeField] private Key loadKey = Key.F6;

    [Header("Load")]
    [Tooltip("Für F6 im Playmode sicherer: Load wird erst im nächsten Frame auf Grid/Inventory angewendet.")]
    [SerializeField] private bool deferLoadByOneFrame = true;

    private bool saveRequested;
    private bool isLoading;
    private bool allowSaveRequests;
    private bool isQuitting;
    private float nextAutoSaveTime;

    public int ActiveSlot => activeSlot;
    public string DefaultGameSceneName => defaultGameSceneName;
    public string MainMenuSceneName => mainMenuSceneName;
    public string CurrentSavePath => GetSavePath(activeSlot);
    public bool IsLoading => isLoading;
    public bool HandlesDebugHotkeys => enableDebugHotkeys;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        nextAutoSaveTime = Time.unscaledTime + autoSaveInterval;
    }

    private IEnumerator Start()
    {
        // Ein Frame warten, damit Awake/Start-Initialisierung nicht direkt als Änderung zählt.
        yield return null;
        allowSaveRequests = true;
    }

    private void Update()
    {
        HandleDebugHotkeys();

        if (!useAutoSave) return;
        if (isLoading || isQuitting) return;
        if (Time.unscaledTime < nextAutoSaveTime) return;

        if (saveRequested)
            SaveNowInternal("Autosave");
        else
            nextAutoSaveTime = Time.unscaledTime + autoSaveInterval;
    }

    private void HandleDebugHotkeys()
    {
        if (!enableDebugHotkeys) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current[saveKey].wasPressedThisFrame)
        {
            SaveNow();
            Debug.Log($"[FarmSaveManager] F5 Save: {CurrentSavePath}");
        }

        if (Keyboard.current[loadKey].wasPressedThisFrame)
        {
            bool loadStarted = GridManager.Instance != null
                ? LoadNow()
                : LoadInventoryOnlyNow();

            Debug.Log(loadStarted
                ? $"[FarmSaveManager] F6 Load gestartet: {CurrentSavePath}"
                : $"[FarmSaveManager] F6 Load fehlgeschlagen: {CurrentSavePath}");
        }
    }

    public void SetActiveSlot(int slot)
    {
        activeSlot = Mathf.Clamp(slot, 1, 3);
    }

    public void StartGameFromSlot(int slot)
    {
        StartGameFromSlot(slot, defaultGameSceneName);
    }

    /// <summary>
    /// Wird vom Hauptmenü oder vom Marktplatz benutzt:
    /// Slot wählen -> GameScene laden -> falls Save existiert, danach automatisch anwenden.
    /// </summary>
    public void StartGameFromSlot(int slot, string gameSceneName)
    {
        SetActiveSlot(slot);

        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogError("[FarmSaveManager] Kein GameScene-Name gesetzt.");
            return;
        }

        StartCoroutine(LoadGameSceneRoutine(gameSceneName, activeSlot));
    }

    public void LoadFarmSceneFromActiveSlot()
    {
        StartGameFromSlot(activeSlot, defaultGameSceneName);
    }

    public void LoadFarmSceneFromActiveSlot(string gameSceneName)
    {
        StartGameFromSlot(activeSlot, gameSceneName);
    }

    private IEnumerator LoadGameSceneRoutine(string gameSceneName, int slot)
    {
        SetActiveSlot(slot);

        bool hasExistingSave = SaveExists(activeSlot);

        isLoading = true;
        allowSaveRequests = false;
        saveRequested = false;
        nextAutoSaveTime = Time.unscaledTime + autoSaveInterval;

        Debug.Log($"[FarmSaveManager] Slot {activeSlot} gewählt. Lade Scene '{gameSceneName}'. SaveExists={hasExistingSave}");

        AsyncOperation operation = SceneManager.LoadSceneAsync(gameSceneName);

        if (operation == null)
        {
            isLoading = false;
            allowSaveRequests = true;
            Debug.LogError($"[FarmSaveManager] Scene '{gameSceneName}' konnte nicht geladen werden. Ist sie in Build Settings eingetragen?");
            yield break;
        }

        while (!operation.isDone)
            yield return null;

        int waitFrames = Mathf.Max(1, framesToWaitAfterSceneLoad);
        for (int i = 0; i < waitFrames; i++)
            yield return null;

        if (hasExistingSave)
        {
            isLoading = false;
            bool loadStarted = LoadNow();

            if (!loadStarted)
            {
                allowSaveRequests = true;
                saveRequested = false;
            }

            yield break;
        }

        isLoading = false;
        allowSaveRequests = true;
        saveRequested = false;
        nextAutoSaveTime = Time.unscaledTime + autoSaveInterval;

        Debug.Log($"[FarmSaveManager] Neuer Slot {activeSlot}: Keine Save-Datei vorhanden. Default-Farm bleibt aktiv.");
    }

    /// <summary>
    /// Wird von Gameplay-Aktionen aufgerufen. Markiert nur, dass gespeichert werden sollte.
    /// Speichern passiert weiterhin manuell per F5 oder per explizitem Button/Scene-Wechsel.
    /// </summary>
    public void RequestSave()
    {
        if (isLoading || isQuitting) return;
        if (!allowSaveRequests) return;

        saveRequested = true;
    }

    public void SaveNow()
    {
        SaveNowInternal("Manual Save");
    }

    private void SaveNowInternal(string reason)
    {
        if (isLoading)
        {
            Debug.LogWarning("[FarmSaveManager] Save ignoriert, weil gerade geladen wird.");
            return;
        }

        if (!CanSaveCurrentScene())
            return;

        SaveGameData data = BuildCurrentSaveData();
        EnsureSaveLists(data);

        string json = JsonUtility.ToJson(data, true);

        string path = GetSavePath(activeSlot);
        string directory = Path.GetDirectoryName(path);

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, json);

        saveRequested = false;
        nextAutoSaveTime = Time.unscaledTime + autoSaveInterval;

        string saveMode = GridManager.Instance != null ? "Full" : "InventoryOnlyPreserveFarm";
        Debug.Log($"[FarmSaveManager] Gespeichert ({reason}, {saveMode}): {path} | Tiles={data.tiles.Count}, Seeds={data.seeds.Count}, Crops={data.crops.Count}, Money={data.money}");
    }

    private bool CanSaveCurrentScene()
    {
        // Menüschutz: Ohne PlayerInventory sind wir sehr wahrscheinlich im MainMenu.
        if (PlayerInventory.Instance == null)
        {
            Debug.LogWarning("[FarmSaveManager] Save abgebrochen: Kein PlayerInventory in der aktuellen Scene gefunden.");
            return false;
        }

        // MarketScene-Schutz: Kein Grid ist okay, aber nur wenn es bereits einen Spielstand gibt,
        // dessen Farm-Tiles erhalten werden können.
        if (GridManager.Instance == null && !SaveExists(activeSlot))
        {
            Debug.LogWarning("[FarmSaveManager] Save abgebrochen: Kein GridManager und keine bestehende Save-Datei. So würde ein leerer Farm-Spielstand entstehen.");
            return false;
        }

        return true;
    }

    public bool LoadNow()
    {
        string path = GetSavePath(activeSlot);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[FarmSaveManager] Keine Save-Datei gefunden: {path}");
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            SaveGameData data = JsonUtility.FromJson<SaveGameData>(json);

            if (data == null)
            {
                Debug.LogWarning($"[FarmSaveManager] Save-Datei konnte nicht gelesen werden: {path}");
                return false;
            }

            EnsureSaveLists(data);

            if (deferLoadByOneFrame && Application.isPlaying)
                StartCoroutine(ApplySaveDataRoutine(data, path));
            else
                ApplySaveDataImmediate(data, path);

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FarmSaveManager] Fehler beim Laden von {path}: {ex}");
            return false;
        }
    }

    /// <summary>
    /// Für MarketScene: lädt nur Geld/Inventar. Dadurch wird nicht auf GridManager/PlantManager gewartet.
    /// </summary>
    public bool LoadInventoryOnlyNow()
    {
        string path = GetSavePath(activeSlot);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[FarmSaveManager] Keine Save-Datei für InventoryOnlyLoad gefunden: {path}");
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            SaveGameData data = JsonUtility.FromJson<SaveGameData>(json);

            if (data == null)
            {
                Debug.LogWarning($"[FarmSaveManager] Save-Datei konnte nicht gelesen werden: {path}");
                return false;
            }

            EnsureSaveLists(data);
            StartCoroutine(ApplyInventoryOnlyRoutine(data, path));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FarmSaveManager] Fehler beim InventoryOnlyLoad von {path}: {ex}");
            return false;
        }
    }

    private IEnumerator ApplySaveDataRoutine(SaveGameData data, string path)
    {
        isLoading = true;
        allowSaveRequests = false;

        yield return null;
        yield return WaitForRuntimeDependencies(requireGridAndPlantManager: true);

        ApplySaveData(data);

        saveRequested = false;
        nextAutoSaveTime = Time.unscaledTime + autoSaveInterval;
        allowSaveRequests = true;

        Debug.Log($"[FarmSaveManager] Geladen: {path} | Tiles={data.tiles.Count}, Seeds={data.seeds.Count}, Crops={data.crops.Count}, Money={data.money}");
    }

    private IEnumerator ApplyInventoryOnlyRoutine(SaveGameData data, string path)
    {
        isLoading = true;
        allowSaveRequests = false;

        yield return null;
        yield return WaitForInventoryDependencies();

        ApplyInventory(data);

        saveRequested = false;
        nextAutoSaveTime = Time.unscaledTime + autoSaveInterval;
        allowSaveRequests = true;
        isLoading = false;

        Debug.Log($"[FarmSaveManager] InventoryOnly geladen: {path} | Seeds={data.seeds.Count}, Crops={data.crops.Count}, Money={data.money}");
    }

    private void ApplySaveDataImmediate(SaveGameData data, string path)
    {
        ApplySaveData(data);

        saveRequested = false;
        nextAutoSaveTime = Time.unscaledTime + autoSaveInterval;
        allowSaveRequests = true;

        Debug.Log($"[FarmSaveManager] Geladen: {path} | Tiles={data.tiles.Count}, Seeds={data.seeds.Count}, Crops={data.crops.Count}, Money={data.money}");
    }

    private IEnumerator WaitForRuntimeDependencies(bool requireGridAndPlantManager)
    {
        int waited = 0;

        while (waited < maxDependencyWaitFrames)
        {
            bool hasInventory = PlayerInventory.Instance != null;
            bool hasPlantDatabase = PlantDatabase.Instance != null;
            bool hasGrid = GridManager.Instance != null;
            bool hasPlantManager = PlantManager.Instance != null;

            bool hasRequiredWorld = !requireGridAndPlantManager || (hasGrid && hasPlantManager);

            if (hasInventory && hasPlantDatabase && hasRequiredWorld)
                yield break;

            waited++;
            yield return null;
        }

        if (PlayerInventory.Instance == null)
            Debug.LogWarning("[FarmSaveManager] Load-Warnung: Kein PlayerInventory gefunden.");

        if (PlantDatabase.Instance == null)
            Debug.LogWarning("[FarmSaveManager] Load-Warnung: Keine PlantDatabase gefunden. Lege PlantDatabase am besten auf dasselbe SaveSystem-GameObject wie FarmSaveManager und trage alle PlantType-Assets ein.");

        if (requireGridAndPlantManager)
        {
            if (GridManager.Instance == null)
                Debug.LogWarning("[FarmSaveManager] Load-Warnung: Kein GridManager gefunden.");

            if (PlantManager.Instance == null)
                Debug.LogWarning("[FarmSaveManager] Load-Warnung: Kein PlantManager gefunden. Pflanzen-Visuals können fehlen.");
        }
    }

    private IEnumerator WaitForInventoryDependencies()
    {
        int waited = 0;

        while (waited < maxDependencyWaitFrames)
        {
            bool hasInventory = PlayerInventory.Instance != null;
            bool hasPlantDatabase = PlantDatabase.Instance != null;

            if (hasInventory && hasPlantDatabase)
                yield break;

            waited++;
            yield return null;
        }

        if (PlayerInventory.Instance == null)
            Debug.LogWarning("[FarmSaveManager] InventoryOnlyLoad-Warnung: Kein PlayerInventory gefunden.");

        if (PlantDatabase.Instance == null)
            Debug.LogWarning("[FarmSaveManager] InventoryOnlyLoad-Warnung: Keine PlantDatabase gefunden.");
    }

    public void ReturnToMainMenu()
    {
        ReturnToMainMenu(true, mainMenuSceneName);
    }

    public void ReturnToMainMenu(bool saveBeforeReturning)
    {
        ReturnToMainMenu(saveBeforeReturning, mainMenuSceneName);
    }

    public void ReturnToMainMenu(bool saveBeforeReturning, string targetMainMenuSceneName)
    {
        StartCoroutine(ReturnToMainMenuRoutine(saveBeforeReturning, targetMainMenuSceneName));
    }

    private IEnumerator ReturnToMainMenuRoutine(bool saveBeforeReturning, string targetMainMenuSceneName)
    {
        if (string.IsNullOrWhiteSpace(targetMainMenuSceneName))
        {
            Debug.LogError("[FarmSaveManager] Kein MainMenu-Scene-Name gesetzt.");
            yield break;
        }

        if (saveBeforeReturning && CanSaveCurrentScene())
            SaveNowInternal("Return To MainMenu");

        isLoading = true;
        allowSaveRequests = false;
        saveRequested = false;

        AsyncOperation operation = SceneManager.LoadSceneAsync(targetMainMenuSceneName);
        if (operation == null)
        {
            isLoading = false;
            allowSaveRequests = true;
            Debug.LogError($"[FarmSaveManager] MainMenu-Scene '{targetMainMenuSceneName}' konnte nicht geladen werden. Ist sie in Build Settings eingetragen?");
            yield break;
        }

        while (!operation.isDone)
            yield return null;

        yield return null;

        isLoading = false;
        allowSaveRequests = true;
        saveRequested = false;
    }

    public bool SaveExists(int slot)
    {
        return File.Exists(GetSavePath(slot));
    }

    public bool TryReadSlotData(int slot, out SaveGameData data)
    {
        data = null;

        string path = GetSavePath(slot);
        if (!File.Exists(path))
            return false;

        try
        {
            string json = File.ReadAllText(path);
            data = JsonUtility.FromJson<SaveGameData>(json);
            if (data != null)
                EnsureSaveLists(data);
            return data != null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FarmSaveManager] Slot {slot} konnte nicht gelesen werden: {ex.Message}");
            data = null;
            return false;
        }
    }

    public void DeleteSlot(int slot)
    {
        string path = GetSavePath(slot);

        if (!File.Exists(path))
        {
            Debug.Log($"[FarmSaveManager] Slot {slot} ist bereits leer: {path}");
            return;
        }

        File.Delete(path);
        Debug.Log($"[FarmSaveManager] Slot {slot} gelöscht: {path}");
    }

    public string GetSavePath(int slot)
    {
        return Path.Combine(Application.persistentDataPath, $"farm_save_slot_{slot}.json");
    }

    private SaveGameData BuildCurrentSaveData()
    {
        SaveGameData data;

        // Wenn wir im Marktplatz sind, gibt es keinen GridManager.
        // Dann laden wir die vorhandene Save-Datei als Basis und ersetzen nur Geld/Inventar.
        if (!TryReadSlotData(activeSlot, out data) || data == null)
            data = new SaveGameData();

        EnsureSaveLists(data);

        data.version = 1;
        data.slotIndex = activeSlot;
        data.savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        data.seeds.Clear();
        data.crops.Clear();
        SaveInventory(data);

        if (GridManager.Instance != null)
        {
            data.tiles.Clear();
            SaveGrid(data);
        }
        else
        {
            Debug.Log("[FarmSaveManager] Kein GridManager gefunden. Speichere nur Inventar/Geld und behalte Farm-Tiles aus bestehendem Save.");
        }

        GridZone[] zones = FindObjectsByType<GridZone>(FindObjectsSortMode.None);
        if (zones != null && zones.Length > 0)
        {
            data.zones.Clear();
            SaveZones(data, zones);
        }

        EnsureSaveLists(data);
        return data;
    }

    private void SaveInventory(SaveGameData data)
    {
        PlayerInventory inventory = PlayerInventory.Instance;
        if (inventory == null) return;

        data.money = inventory.Money;

        foreach (var kvp in inventory.GetAllSeeds())
        {
            if (kvp.Key == null || kvp.Value <= 0) continue;

            data.seeds.Add(new InventoryStackSaveData
            {
                plantId = PlantDatabase.GetPlantId(kvp.Key),
                amount = kvp.Value
            });
        }

        foreach (var kvp in inventory.GetAllCrops())
        {
            if (kvp.Key == null || kvp.Value <= 0) continue;

            data.crops.Add(new InventoryStackSaveData
            {
                plantId = PlantDatabase.GetPlantId(kvp.Key),
                amount = kvp.Value
            });
        }
    }

    private void SaveGrid(SaveGameData data)
    {
        GridManager grid = GridManager.Instance;
        if (grid == null) return;

        for (int x = 0; x < grid.Width; x++)
        {
            for (int z = 0; z < grid.Height; z++)
            {
                GridCell cell = grid.GetCell(x, z);
                if (cell == null) continue;

                TileSaveData tileData = new TileSaveData
                {
                    x = x,
                    z = z,
                    tileType = cell.Type.ToString(),
                    isLocked = cell.IsLocked,
                    isTilled = cell.IsTilled,
                    hasPlant = cell.HasPlant
                };

                if (cell.HasPlant && cell.Plant != null)
                {
                    tileData.plantId = PlantDatabase.GetPlantId(cell.Plant.Type);
                    tileData.plantStageIndex = cell.Plant.StageIndex;
                    tileData.plantGrowthTimer = cell.Plant.GrowthTimer;
                    tileData.plantWateringsThisStage = cell.Plant.WateringsThisStage;
                }

                data.tiles.Add(tileData);
            }
        }
    }

    private void SaveZones(SaveGameData data, GridZone[] zones)
    {
        foreach (GridZone zone in zones)
        {
            if (zone == null) continue;

            data.zones.Add(new ZoneSaveData
            {
                zoneId = zone.SaveId,
                isUnlocked = zone.IsUnlocked
            });
        }
    }

    private void ApplySaveData(SaveGameData data)
    {
        isLoading = true;

        try
        {
            ApplyZones(data);
            ApplyGrid(data);
            ApplyInventory(data);
        }
        finally
        {
            isLoading = false;
        }
    }

    private void ApplyInventory(SaveGameData data)
    {
        PlayerInventory inventory = PlayerInventory.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("[FarmSaveManager] Kein PlayerInventory in der Scene gefunden. Inventar wurde nicht geladen.");
            return;
        }

        inventory.ApplyLoadedData(data.money, data.seeds, data.crops);
    }

    private void ApplyGrid(SaveGameData data)
    {
        GridManager grid = GridManager.Instance;
        if (grid == null)
        {
            // Im Marktplatz normal und kein Fehler.
            Debug.Log("[FarmSaveManager] Kein GridManager in dieser Scene. Grid-Load wird übersprungen.");
            return;
        }

        grid.ApplySaveTiles(data.tiles);
    }

    private void ApplyZones(SaveGameData data)
    {
        GridZone[] zones = FindObjectsByType<GridZone>(FindObjectsSortMode.None);

        foreach (GridZone zone in zones)
        {
            if (zone == null) continue;

            bool found = false;
            bool unlocked = false;

            foreach (ZoneSaveData zoneData in data.zones)
            {
                if (zoneData == null) continue;
                if (zoneData.zoneId != zone.SaveId) continue;

                found = true;
                unlocked = zoneData.isUnlocked;
                break;
            }

            if (found)
                zone.ApplyLoadedState(unlocked);
        }
    }

    private static void EnsureSaveLists(SaveGameData data)
    {
        if (data == null) return;

        if (data.seeds == null) data.seeds = new List<InventoryStackSaveData>();
        if (data.crops == null) data.crops = new List<InventoryStackSaveData>();
        if (data.tiles == null) data.tiles = new List<TileSaveData>();
        if (data.zones == null) data.zones = new List<ZoneSaveData>();
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;

        if (!saveOnApplicationQuit) return;

        if (!saveRequested)
        {
            Debug.Log("[FarmSaveManager] Quit Save übersprungen: Keine Änderungen seit letztem Save/Load.");
            return;
        }

        SaveNowInternal("Quit Save");
    }
}

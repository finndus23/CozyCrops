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
    [SerializeField] private string defaultGameSceneName = "SampleScene";

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

    [Header("Speichern bei Änderungen")]
    [Tooltip("Schreibt kurz nachdem sich etwas geändert hat.\n\n" +
             "Ohne das bleibt RequestSave() wirkungslos, solange die Autosave-Schleife aus " +
             "ist: Missions-Fortschritt, gebaute Tiles und Käufe melden zwar brav eine " +
             "Änderung an, aber niemand schreibt sie — nur der manuelle Save tut es.")]
    [SerializeField] private bool saveOnRequest = true;

    [Tooltip("Wartezeit nach der ersten Änderung, bevor geschrieben wird.\n\n" +
             "Fasst zusammenhängende Änderungen zu einem Schreibvorgang zusammen: eine " +
             "AoE-Ernte meldet ein Dutzend Änderungen im selben Moment, das soll nicht ein " +
             "Dutzend Dateizugriffe auslösen.\n\n" +
             "Der Timer läuft ab der ERSTEN Änderung und wird von weiteren nicht nach hinten " +
             "geschoben — sonst könnte durchgehendes Spielen das Speichern beliebig lange " +
             "hinauszögern, und genau dann verliert man am meisten.")]
    [SerializeField] private float saveDebounce = 2f;

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

    // Zeitpunkt, zu dem die angesammelten Änderungen geschrieben werden sollen.
    private float saveDueTime;

    public int ActiveSlot => activeSlot;
    public string DefaultGameSceneName => defaultGameSceneName;
    public string MainMenuSceneName => mainMenuSceneName;
    public string CurrentSavePath => GetSavePath(activeSlot);
    public bool IsLoading => isLoading;
    public bool HandlesDebugHotkeys => enableDebugHotkeys;

    /// <summary>
    /// Spiegelt das Inventar in der AKTUELL geladenen Szene bereits den Spielstand?
    ///
    /// Nach jedem Szenenwechsel kurz false: PlayerInventory liegt in der Szene, entsteht
    /// also neu und startet mit 0 Gold und leeren Listen, bis der Spielstand eingespielt
    /// ist. Wer in diesem Fenster den Spielerzustand bewertet, sieht einen Bettler statt
    /// des echten Stands.
    ///
    /// <see cref="IsLoading"/> reicht dafür NICHT: Fahrten zwischen Farm und Marktplatz
    /// laufen über SceneLoadingScreen.LoadScene() am Save-Manager vorbei, dabei bleibt
    /// isLoading durchgehend false. Das Nachladen stößt erst FarmSceneAutoLoad an — also
    /// nach dem Awake/OnEnable aller Szenen-Objekte. IsLoading deckt "wird gerade
    /// geladen" ab, hier fehlte "wurde noch nicht geladen".
    ///
    /// Bewusst als Vergleich der Szenen-Kennung statt als bool, das bei
    /// SceneManager.sceneLoaded zurückgesetzt wird: dieses Ereignis feuert ERST NACH dem
    /// Awake/OnEnable der neuen Szenen-Objekte. Ein Flag stünde also ausgerechnet während
    /// der ersten Prüfungen noch auf dem Stand der VORIGEN Szene — also genau der Fehler,
    /// den es verhindern soll (erster Anlauf ist daran gescheitert). Ein Vergleich gegen
    /// die aktive Szene kann per Konstruktion nicht veralten.
    /// </summary>
    public bool InventoryRestored =>
        inventoryRestoredSceneHandle == SceneManager.GetActiveScene().handle;

    private int inventoryRestoredSceneHandle = -1;

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

        if (isLoading || isQuitting) return;

        // Änderungsgetriebenes Speichern läuft unabhängig von der Autosave-Schleife: es soll
        // auch dann greifen, wenn niemand ein festes Intervall haben will.
        if (saveOnRequest && saveRequested && Time.unscaledTime >= saveDueTime)
        {
            SaveNowInternal("Auto (Änderung)");
            return;
        }

        if (!useAutoSave) return;
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

        bool hasExistingSave = HasInitializedSave(activeSlot);

        isLoading = true;
        allowSaveRequests = false;
        saveRequested = false;
        nextAutoSaveTime = Time.unscaledTime + autoSaveInterval;

        Debug.Log($"[FarmSaveManager] Slot {activeSlot} gewählt. Lade Scene '{gameSceneName}'. SaveExists={hasExistingSave}");

        bool sceneLoaded = false;
        yield return SceneLoadingScreen.LoadSceneRoutine(gameSceneName, success => sceneLoaded = success);

        if (!sceneLoaded)
        {
            isLoading = false;
            allowSaveRequests = true;
            Debug.LogError($"[FarmSaveManager] Scene '{gameSceneName}' konnte nicht geladen werden. Ist sie in Build Settings eingetragen?");
            yield break;
        }

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

        // Ein neuer Slot wird absichtlich nicht vollständig geladen, damit leere Missions-
        // und Tile-Listen nicht die vorbereitete Farm bzw. das Tutorial überschreiben.
        // Geld und Spieltempo aus dem Erstellungsdialog müssen nach dem Scene-Wechsel aber
        // trotzdem auf das neu erzeugte PlayerInventory übertragen werden.
        if (TryReadSlotData(activeSlot, out SaveGameData newSlotDefaults))
        {
            yield return WaitForInventoryDependencies();
            ApplyInventory(newSlotDefaults);
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

        // Nur die erste Änderung startet den Timer. Würde jede weitere ihn neu setzen,
        // schöbe durchgehendes Spielen das Speichern immer weiter vor sich her.
        if (!saveRequested) saveDueTime = Time.unscaledTime + Mathf.Max(0.1f, saveDebounce);

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

            // Frisch per CreateSlot() angelegter, aber noch nicht bespielter Slot:
            // Die Datei existiert zwar, enthält aber nur Defaults. Würde man sie laden,
            // überschriebe ApplySaveData u.a. die gerade von TutorialNpc gestartete
            // Tutorial-Mission mit leeren Daten (activeMissions → 0). Also nichts laden.
            if (!IsInitializedSave(data))
            {
                Debug.Log($"[FarmSaveManager] Slot {activeSlot} existiert, ist aber noch nicht initialisiert → nichts zu laden, Default-Farm bleibt aktiv.");
                return false;
            }

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

        bool sceneLoaded = false;
        yield return SceneLoadingScreen.LoadSceneRoutine(targetMainMenuSceneName, success => sceneLoaded = success);

        if (!sceneLoaded)
        {
            isLoading = false;
            allowSaveRequests = true;
            Debug.LogError($"[FarmSaveManager] MainMenu-Scene '{targetMainMenuSceneName}' konnte nicht geladen werden. Ist sie in Build Settings eingetragen?");
            yield break;
        }

        yield return null;

        isLoading = false;
        allowSaveRequests = true;
        saveRequested = false;
    }

    public bool SaveExists(int slot)
    {
        return File.Exists(GetSavePath(slot));
    }

    /// <summary>
    /// True nur wenn der Slot ein *echtes, initialisiertes* Spiel enthält.
    /// Ein frisch per CreateSlot() angelegter Slot existiert zwar als Datei,
    /// gilt aber erst nach dem ersten echten Save (isInitialized) als bespielt.
    /// Altsaves (version &lt; 2) kannten das Flag nicht → zählen als initialisiert.
    /// </summary>
    public bool HasInitializedSave(int slot)
    {
        return TryReadSlotData(slot, out SaveGameData data)
            && IsInitializedSave(data);
    }

    /// <summary>
    /// True wenn diese Save-Daten ein echtes, bespieltes Spiel darstellen.
    /// Ein frisch per CreateSlot() angelegter Slot hat isInitialized=false und gilt nicht.
    /// Altsaves (version &lt; 2) kannten das Flag nicht → zählen als initialisiert.
    /// Single source of truth für diese Unterscheidung.
    /// </summary>
    private static bool IsInitializedSave(SaveGameData data)
        => data != null && (data.isInitialized || data.version < 2);

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

    [ContextMenu("Debug: Aktiven Slot löschen")]
    private void DebugDeleteActiveSlot()
    {
        DeleteSlot(activeSlot);

        // In-Memory-State aller Manager zurücksetzen
        MissionManager.Instance?.ApplyLoadedData(new System.Collections.Generic.List<MissionProgressSaveData>());
        TutorialManager.Instance?.ForceReset();

        // Scene neu laden damit TutorialNpc.Start() erneut feuert (nur im Play-Modus)
        if (Application.isPlaying)
            SceneManager.LoadScene(defaultGameSceneName);
    }

    public bool CreateSlot(int slot, string playerName, GamePace pace, int startingMoney = 100)
    {
        slot = Mathf.Clamp(slot, 1, 3);
        playerName = string.IsNullOrWhiteSpace(playerName) ? "Farm" : playerName.Trim();

        if (SaveExists(slot))
            return false;

        SaveGameData data = new SaveGameData
        {
            version = 4,
            slotIndex = slot,
            playerName = playerName,
            money = startingMoney,
            gamePace = (int)pace,
            savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            isInitialized = false
        };

        // Sofort auch auf das laufende Inventar anwenden: der Slot wird direkt nach dem
        // Anlegen bespielt, und der noch nicht initialisierte Save wird bewusst nicht
        // vollständig geladen (siehe LoadNow). Ohne das bliebe insbesondere der sichtbare
        // Geldstand vom vorherigen Slot erhalten bzw. stünde bei einem frischen Start auf 0.
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.Pace = pace;
            PlayerInventory.Instance.ApplyLoadedData(
                startingMoney,
                new List<InventoryStackSaveData>(),
                new List<InventoryStackSaveData>());
        }

        string path = GetSavePath(slot);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(data, true));
        return true;
    }

    public string GetSavePath(int slot)
    {
        return Path.Combine(Application.persistentDataPath, $"farm_save_slot_{slot}.json");
    }

    private SaveGameData BuildCurrentSaveData()
    {
        SaveGameData data = null;
        TryReadSlotData(activeSlot, out data);

        // Wenn wir im Marktplatz sind, gibt es keinen GridManager.
        // Dann laden wir die vorhandene Save-Datei als Basis und ersetzen nur Geld/Inventar.
        bool preserveExistingFarmData = GridManager.Instance == null;
        if (preserveExistingFarmData && data == null)
            TryReadSlotData(activeSlot, out data);

        if (data == null)
            data = new SaveGameData();

        EnsureSaveLists(data);

        // Einen alten Farm-Spielstand erst in der Farm auf v4 migrieren. Die neuen
        // Außen-Zonen besitzen absichtlich eigene IDs und kollidieren dadurch nicht mehr
        // mit den ehemaligen Innenzonen zone_1..zone_4.
        if (GridManager.Instance != null || data.version >= 4)
            data.version = 4;
        data.slotIndex = activeSlot;
        data.savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        data.isInitialized = true;

        data.seeds.Clear();
        data.crops.Clear();
        data.composterComposition.Clear();
        SaveInventory(data);
        SaveComposter(data);

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

        if (ToolRegistry.Instance != null)
        {
            data.toolLevels.Clear();
            data.toolLevels.AddRange(ToolRegistry.Instance.GetSaveData());
            data.ownedTools.Clear();
            data.ownedTools.AddRange(ToolRegistry.Instance.GetOwnedToolsSaveData());
        }

        if (LicenseRegistry.Instance != null)
        {
            data.ownedLicenses.Clear();
            data.ownedLicenses.AddRange(LicenseRegistry.Instance.GetSaveData());
        }

        if (MissionManager.Instance != null)
        {
            data.missionProgress.Clear();
            data.missionProgress.AddRange(MissionManager.Instance.GetSaveData());
        }

        // Der Guard ist hier das Entscheidende — exakt dasselbe Muster wie bei SaveGrid und
        // SaveComposter. Der Manager lebt nur in der Farm-Szene; ohne diese Abfrage wuerde
        // jeder Speichervorgang auf dem Marktplatz die Liste leeren und saemtliche Geraete
        // aus dem Spielstand wischen.
        if (AutomationDeviceManager.Instance != null)
        {
            data.automationDevices.Clear();
            data.packedAutomationDevices.Clear();
            SaveAutomationDevices(data);
        }

        EnsureSaveLists(data);
        return data;
    }

    private void SaveAutomationDevices(SaveGameData data)
    {
        var manager = AutomationDeviceManager.Instance;
        if (manager == null) return;

        foreach (var station in manager.AllDevices)
        {
            if (station == null || station.Data == null) continue;

            var tile = station.TilePosition;
            var entry = new AutomationDeviceSaveData
            {
                x = tile.x,
                z = tile.y,
                level = station.Level
            };

            foreach (var module in station.Modules)
                AppendModule(entry, module);

            data.automationDevices.Add(entry);
        }

        // Eingelagerte Stationen: dieselbe Struktur, x/z bleiben ungenutzt.
        foreach (var packed in manager.PackedStations)
        {
            if (packed == null) continue;

            var entry = new AutomationDeviceSaveData { level = packed.level };

            foreach (var module in packed.modules)
                AppendModule(entry, module);

            data.packedAutomationDevices.Add(entry);
        }
    }

    private static void AppendModule(AutomationDeviceSaveData entry, AutomationModule module)
    {
        if (entry == null || module == null || module.data == null) return;

        entry.modules.Add(new AutomationModuleSaveData
        {
            moduleType = module.data.deviceType.ToString(),
            level = module.level,
            enabled = module.enabled,
            seedId = module.seed != null ? PlantDatabase.GetPlantId(module.seed) : null,
            cooldownRemaining = Mathf.Max(0f, module.cooldown)
        });
    }

    /// <summary>Baut ein Modul aus seinem Save-Eintrag. Null, wenn der Typ unbekannt ist.</summary>
    private static AutomationModule BuildModule(AutomationModuleSaveData saved)
    {
        if (saved == null) return null;
        if (!Enum.TryParse(saved.moduleType, out AutomationDeviceType type)) return null;
        if (type == AutomationDeviceType.None) return null;

        var moduleData = AutomationDeviceCatalog.Get(type);
        if (moduleData == null) return null;

        return new AutomationModule
        {
            data = moduleData,
            level = Mathf.Clamp(saved.level, 0, moduleData.maxLevel),
            enabled = saved.enabled,
            cooldown = Mathf.Max(0f, saved.cooldownRemaining),
            seed = !string.IsNullOrEmpty(saved.seedId) && PlantDatabase.Instance != null
                ? PlantDatabase.Instance.GetById(saved.seedId)
                : null
        };
    }

    private void ApplyAutomation(SaveGameData data)
    {
        var manager = AutomationDeviceManager.Instance;
        if (manager == null) return;

        manager.Clear();
        if (data.automationDevices == null) return;

        var stationData = AutomationDeviceCatalog.Station;
        if (stationData == null)
        {
            if (data.automationDevices.Count > 0)
                Debug.LogWarning("[Automation] Kein Stations-Asset im AutomationDeviceCatalog " +
                                 "hinterlegt — gespeicherte Stationen koennen nicht geladen werden.");
            return;
        }

        foreach (var entry in data.automationDevices)
        {
            if (entry == null) continue;

            var station = manager.Spawn(stationData, entry.x, entry.z);
            if (station == null) continue;

            station.SetLevel(entry.level);

            if (entry.modules == null) continue;

            var built = new List<AutomationModule>();
            foreach (var saved in entry.modules)
            {
                var module = BuildModule(saved);
                if (module != null) built.Add(module);
            }

            station.RestoreModules(built);
        }

        // Lager wiederherstellen.
        var packedList = new List<PackedStation>();
        if (data.packedAutomationDevices != null)
        {
            foreach (var entry in data.packedAutomationDevices)
            {
                if (entry == null) continue;

                var packed = new PackedStation { level = entry.level };

                if (entry.modules != null)
                {
                    foreach (var saved in entry.modules)
                    {
                        var module = BuildModule(saved);
                        if (module != null) packed.modules.Add(module);
                    }
                }

                packedList.Add(packed);
            }
        }

        manager.SetPackedStations(packedList);
    }

    private void SaveInventory(SaveGameData data)
    {
        PlayerInventory inventory = PlayerInventory.Instance;
        if (inventory == null) return;

        data.money = inventory.Money;
        data.fertilizer = inventory.Fertilizer;
        data.gamePace = (int)inventory.Pace;

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

    /// <summary>
    /// Nichts zu tun wenn kein Komposter in der Szene steht (z.B. Marktplatz) — dann bleibt
    /// der zuletzt gespeicherte Zustand einfach unangetastet, wie bei SaveGrid ohne GridManager.
    /// </summary>
    private void SaveComposter(SaveGameData data)
    {
        if (ComposterInteraction.Instance == null) return;

        data.composterBrewing = ComposterInteraction.Instance.IsBrewing;
        data.composterTimeRemaining = ComposterInteraction.Instance.TimeRemaining;
        data.composterFertilizerYield = ComposterInteraction.Instance.PendingFertilizerYield;
        data.composterTotalBrewTime = ComposterInteraction.Instance.TotalBrewTime;

        // Zusammensetzung des laufenden Batches — nötig, damit "Abbrechen" auch nach einem
        // Neuladen mitten im Brauvorgang die richtige Ernte zurückgeben kann.
        foreach (var kvp in ComposterInteraction.Instance.BrewingComposition)
        {
            if (kvp.Key == null || kvp.Value <= 0) continue;

            data.composterComposition.Add(new InventoryStackSaveData
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

        for (int x = grid.MinX; x < grid.MaxXExclusive; x++)
        {
            for (int z = grid.MinZ; z < grid.MaxZExclusive; z++)
            {
                GridCell cell = grid.GetCell(x, z);
                if (cell == null) continue;

                // Der äußere Horizont wird ausschließlich statisch in der Unity-Scene
                // dekoriert. Er gehört nicht zum Spielerfortschritt und damit auch nicht
                // in den Tile-Spielstand.
                if (grid.IsDecorationArea(x, z))
                    continue;

                TileSaveData tileData = new TileSaveData
                {
                    x = x,
                    z = z,
                    tileType = cell.Type.ToString(),
                    isLocked = cell.IsLocked,
                    isTilled = cell.IsTilled,
                    isFertilized = cell.IsFertilized,
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
            ApplyComposter(data);
            ApplyToolLevels(data);

            // Nach ApplyGrid, weil die Geraete beim Platzieren ihre Kachelliste cachen —
            // Zellen und IsLocked muessen also schon stehen. Und nach ApplyInventory, weil
            // die Sorte der Saemaschine ueber die PlantDatabase aufgeloest wird.
            // Vor ApplyMissions.
            ApplyAutomation(data);

            ApplyMissions(data);
        }
        finally
        {
            isLoading = false;
        }
    }

    private void ApplyToolLevels(SaveGameData data)
    {
        if (ToolRegistry.Instance != null)
        {
            ToolRegistry.Instance.ApplyLoadedData(data.toolLevels);
            ToolRegistry.Instance.ApplyOwnedToolsData(data.ownedTools);
        }

        // Lizenzen VOR den Missionen laden: Missions-Voraussetzungen und Shop-Angebot
        // fragen den Lizenz-Stand ab.
        if (LicenseRegistry.Instance != null)
            LicenseRegistry.Instance.ApplyLoadedData(data.ownedLicenses);
    }

    private void ApplyMissions(SaveGameData data)
    {
        if (MissionManager.Instance == null) return;
        MissionManager.Instance.ApplyLoadedData(data.missionProgress);
    }

    private void ApplyInventory(SaveGameData data)
    {
        PlayerInventory inventory = PlayerInventory.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("[FarmSaveManager] Kein PlayerInventory in der Scene gefunden. Inventar wurde nicht geladen.");
            return;
        }

        // Tempo VOR dem Geld setzen: sonst liefe ein direkt danach ausgelöster Verkauf
        // noch mit dem Standardfaktor.
        inventory.Pace = (GamePace)data.gamePace;
        inventory.ApplyLoadedData(data.money, data.seeds, data.crops, data.fertilizer);

        // Ab hier zeigt das Inventar echte Werte — aber nur für DIESE Szene. Beim
        // nächsten Szenenwechsel entsteht ein neues PlayerInventory mit Startwerten,
        // und der Vergleich in InventoryRestored schlägt dann von selbst wieder fehl.
        inventoryRestoredSceneHandle = SceneManager.GetActiveScene().handle;
    }

    private void ApplyComposter(SaveGameData data)
    {
        if (ComposterInteraction.Instance == null) return;

        ComposterInteraction.Instance.ApplyLoadedData(
            data.composterBrewing,
            data.composterTimeRemaining,
            data.composterFertilizerYield,
            data.composterTotalBrewTime,
            data.composterComposition);
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
        Dictionary<string, bool> savedZoneStates = new();

        foreach (ZoneSaveData zoneData in data.zones)
        {
            if (zoneData == null || string.IsNullOrEmpty(zoneData.zoneId)) continue;
            savedZoneStates[zoneData.zoneId] = zoneData.isUnlocked;
        }

        foreach (GridZone zone in zones)
        {
            if (zone == null) continue;

            if (savedZoneStates.TryGetValue(zone.SaveId, out bool unlocked))
                zone.ApplyLoadedState(unlocked);
        }
    }

    private static void EnsureSaveLists(SaveGameData data)
    {
        if (data == null) return;

        if (data.seeds == null) data.seeds = new List<InventoryStackSaveData>();
        if (data.crops == null) data.crops = new List<InventoryStackSaveData>();
        if (data.composterComposition == null) data.composterComposition = new List<InventoryStackSaveData>();
        if (data.tiles == null) data.tiles = new List<TileSaveData>();
        if (data.zones == null) data.zones = new List<ZoneSaveData>();
        if (data.toolLevels == null) data.toolLevels = new List<ToolLevelSaveData>();
        if (data.ownedTools == null) data.ownedTools = new List<string>();
        if (data.missionProgress == null) data.missionProgress = new List<MissionProgressSaveData>();
        if (data.ownedLicenses == null) data.ownedLicenses = new List<string>();
        if (data.automationDevices == null) data.automationDevices = new List<AutomationDeviceSaveData>();
        if (data.packedAutomationDevices == null) data.packedAutomationDevices = new List<AutomationDeviceSaveData>();
    }

    private void OnApplicationQuit()
    {
        SaveOnLeaving("Quit Save");
        isQuitting = true;
    }

    /// <summary>
    /// Letzte Gelegenheit zu speichern, wenn das Spiel in den Hintergrund geht.
    ///
    /// Auf dem Desktop ist das die Absicherung gegen das, was <see cref="OnApplicationQuit"/>
    /// nicht mitbekommt: abgewürgte Prozesse, Abstürze, ausgehender Akku. Der Debounce-Timer
    /// wäre in dem Moment womöglich noch nicht abgelaufen.
    /// </summary>
    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveOnLeaving("Pause Save");
    }

    private void SaveOnLeaving(string reason)
    {
        if (isQuitting || isLoading) return;

        // saveOnApplicationQuit schaltet nur die Zusatzsicherung ab; hängen ausstehende
        // Änderungen an, wird trotzdem geschrieben. Ungespeicherter Fortschritt beim
        // Beenden ist kein Verhalten, das man versehentlich konfiguriert haben will.
        if (!saveRequested)
        {
            if (saveOnApplicationQuit)
                Debug.Log($"[FarmSaveManager] {reason} übersprungen: keine Änderungen seit dem letzten Save.");

            return;
        }

        SaveNowInternal(reason);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zentrales Mission-System. Sitzt auf dem SaveSystem-Objekt (DontDestroyOnLoad).
/// Verwaltet Story-Kette + optionale Neben-Missionen.
/// </summary>
public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance { get; private set; }

    [Header("Story-Kette (in Reihenfolge)")]
    [SerializeField] private MissionData[] storyChain;

    [Header("Optionale Neben-Missionen")]
    [SerializeField] private MissionData[] sideMissions;

    [Header("Verhalten")]
    [Tooltip("Startet nach dem Laden automatisch die nächste offene Story-Mission. " +
             "Aus = die Kette muss von außen angestoßen werden (Dialog, Tutorial-Ende).")]
    [SerializeField] private bool autoStartStoryChain = true;

    private readonly List<MissionState> activeMissions = new();
    private readonly HashSet<string> completedMissionIds = new();

    /// <summary>
    /// Abgeschlossene Missionen deren Belohnung noch nicht abgeholt wurde.
    /// Die Kette läuft trotzdem weiter — liegengelassene Beute blockiert keinen Fortschritt.
    /// </summary>
    private readonly Dictionary<string, MissionData> pendingRewards = new();

    public event Action<MissionData> OnMissionStarted;
    public event Action<MissionData> OnMissionCompleted;

    /// <summary>Belohnung liegt bereit. Die UI baut daraus die Abhol-Karte.</summary>
    public event Action<MissionData> OnRewardsPending;

    /// <summary>Belohnung wurde abgeholt und ist gutgeschrieben.</summary>
    public event Action<MissionData> OnRewardsCollected;

    /// <summary>mission, objectiveIndex, currentProgress, requiredAmount</summary>
    public event Action<MissionData, int, int, int> OnObjectiveUpdated;

    public IReadOnlyList<MissionState> ActiveMissions => activeMissions;
    public int CompletedMissionCount => completedMissionIds.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        PlantManager.OnFieldTilled += HandleTilled;
        PlantManager.OnSeedPlanted += HandlePlanted;
        PlantManager.OnPlantWatered += HandleWatered;
        PlantManager.OnCropHarvested += HandleHarvested;
        PlantManager.OnFieldFertilized += HandleFertilized;
        ComposterInteraction.OnCompostStartedStatic += HandleCompostStarted;
        ComposterInteraction.OnFertilizerCollectedStatic += HandleFertilizerCollected;
        AutomationPlacementController.OnStationPlacedStatic += HandleStationPlaced;
        AutomationDevice.OnModuleInstalledStatic += HandleModuleInstalled;
        AutomationDevice.OnStationUpgradedStatic += HandleStationUpgraded;
        AutomationDevice.OnAllModulesMaxedStatic += HandleAllModulesMaxed;
        ComposterInteraction.OnLevelChangedStatic += HandleComposterLevelChanged;
        PlayerInventory.OnCropSoldStatic += HandleCropSold;

        BuildModeManager.OnBuildModeEnteredStatic += HandleBuildModeEntered;
        BuildModeManager.OnBuildModeExitedStatic += HandleBuildModeExited;
        TileContextMenu.OnFarmTilePlacedStatic += HandleFarmTilePlaced;
        CarClickHandler.OnTraveledToMarketStatic += HandleTraveledToMarket;
        CarClickHandler.OnTraveledToFarmStatic += HandleTraveledToFarm;
        BarnInteraction.OnBarnOpenedStatic += HandleBarnOpened;
        ToolRegistry.OnToolAcquiredStatic += HandleToolAcquired;
        ToolRegistry.OnToolUpgradedStatic += HandleToolUpgraded;
        ToolUseHandler.OnActionStackedStatic += HandleActionStacked;
        LicenseRegistry.OnLicenseBoughtStatic += HandleLicenseBought;
        GridZone.OnZonePurchasedStatic += HandleZonePurchased;
        PlayerInventory.OnSeedBoughtStatic += HandleSeedBought;
        PlayerInventory.OnMoneyEarnedStatic += HandleMoneyEarned;
        Hotbar.OnToolSelectedStatic += HandleToolSelected;
    }

    private void OnDisable()
    {
        PlantManager.OnFieldTilled -= HandleTilled;
        PlantManager.OnSeedPlanted -= HandlePlanted;
        PlantManager.OnPlantWatered -= HandleWatered;
        PlantManager.OnCropHarvested -= HandleHarvested;
        PlantManager.OnFieldFertilized -= HandleFertilized;
        ComposterInteraction.OnCompostStartedStatic -= HandleCompostStarted;
        ComposterInteraction.OnFertilizerCollectedStatic -= HandleFertilizerCollected;
        AutomationPlacementController.OnStationPlacedStatic -= HandleStationPlaced;
        AutomationDevice.OnModuleInstalledStatic -= HandleModuleInstalled;
        AutomationDevice.OnStationUpgradedStatic -= HandleStationUpgraded;
        AutomationDevice.OnAllModulesMaxedStatic -= HandleAllModulesMaxed;
        ComposterInteraction.OnLevelChangedStatic -= HandleComposterLevelChanged;
        PlayerInventory.OnCropSoldStatic -= HandleCropSold;

        BuildModeManager.OnBuildModeEnteredStatic -= HandleBuildModeEntered;
        BuildModeManager.OnBuildModeExitedStatic -= HandleBuildModeExited;
        TileContextMenu.OnFarmTilePlacedStatic -= HandleFarmTilePlaced;
        CarClickHandler.OnTraveledToMarketStatic -= HandleTraveledToMarket;
        CarClickHandler.OnTraveledToFarmStatic -= HandleTraveledToFarm;
        BarnInteraction.OnBarnOpenedStatic -= HandleBarnOpened;
        ToolRegistry.OnToolAcquiredStatic -= HandleToolAcquired;
        ToolRegistry.OnToolUpgradedStatic -= HandleToolUpgraded;
        ToolUseHandler.OnActionStackedStatic -= HandleActionStacked;
        LicenseRegistry.OnLicenseBoughtStatic -= HandleLicenseBought;
        GridZone.OnZonePurchasedStatic -= HandleZonePurchased;
        PlayerInventory.OnSeedBoughtStatic -= HandleSeedBought;
        PlayerInventory.OnMoneyEarnedStatic -= HandleMoneyEarned;
        Hotbar.OnToolSelectedStatic -= HandleToolSelected;
    }

    // --- Public API ---

    public void StartMission(MissionData data)
    {
        if (data == null) return;
        if (completedMissionIds.Contains(data.missionId)) return;
        if (activeMissions.Exists(m => m.Data.missionId == data.missionId)) return;
        if (!ArePrerequisitesMet(data))
        {
            Debug.Log($"[MissionManager] '{data.missionId}' übersprungen — Voraussetzungen noch offen.");
            return;
        }

        var state = new MissionState(data);
        SyncAbsoluteObjectives(state);
        activeMissions.Add(state);

        if (state.IsCompleted())
        {
            // Kann passieren wenn die Bedingung schon vor Missionsbeginn erfüllt war,
            // z.B. "Hacke auf Stufe 10" bei bereits ausgebauter Hacke.
            CompleteMission(state);
            return;
        }

        OnMissionStarted?.Invoke(data);
        Debug.Log($"[MissionManager] Mission gestartet: {data.title}");

        FarmSaveManager.Instance?.RequestSave();
    }

    /// <summary>
    /// Sind alle Vorbedingungen einer Mission erfüllt?
    /// Ohne das war die Reihenfolge nur die Array-Position in storyChain — Nebenmissionen
    /// konnten sich nicht an den Story-Fortschritt hängen.
    /// </summary>
    public bool ArePrerequisitesMet(MissionData data)
    {
        if (data == null) return false;
        if (completedMissionIds.Count < data.requiredCompletedCount) return false;

        if (data.requiredMissionIds != null)
        {
            foreach (var id in data.requiredMissionIds)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!completedMissionIds.Contains(id)) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Die nächste Story-Mission die dran wäre — auch wenn sie noch nicht gestartet ist.
    /// Damit kann die UI immer anzeigen was als Nächstes kommt, statt bei "keine Mission
    /// aktiv" leer zu bleiben. Genau da riss der rote Faden bisher.
    /// </summary>
    public MissionData NextStoryMission
    {
        get
        {
            if (storyChain == null) return null;

            foreach (var mission in storyChain)
            {
                if (mission == null) continue;
                if (completedMissionIds.Contains(mission.missionId)) continue;
                return mission;
            }

            return null;
        }
    }

    /// <summary>
    /// Sammelt die highlightIds der NPCs, die gerade ein Gespraech schulden: Missionen, die
    /// dran waeren, deren Voraussetzungen erfuellt sind, die aber noch auf den Dialog
    /// warten (startedByDialogue).
    ///
    /// Genau in diesem Fenster gibt es noch KEIN Objective — der MissionHighlightDirector
    /// haette also nichts, woran er ein Ziel festmachen koennte, obwohl der nextStepHint
    /// schon "Sprich mit Onkel Ozan" sagt.
    /// </summary>
    public void CollectPendingDialogueStarters(List<string> into)
    {
        if (into == null) return;

        AppendStarter(into, NextStoryMission);

        if (sideMissions == null) return;
        foreach (var mission in sideMissions)
            AppendStarter(into, mission);
    }

    private void AppendStarter(List<string> into, MissionData mission)
    {
        if (mission == null) return;
        if (!mission.startedByDialogue) return;
        if (string.IsNullOrWhiteSpace(mission.starterHighlightId)) return;

        if (completedMissionIds.Contains(mission.missionId)) return;
        if (activeMissions.Exists(m => m.Data.missionId == mission.missionId)) return;
        if (!ArePrerequisitesMet(mission)) return;

        if (!into.Contains(mission.starterHighlightId))
            into.Add(mission.starterHighlightId);
    }

    /// <summary>Läuft gerade eine Story-Mission?</summary>
    public bool HasActiveStoryMission => activeMissions.Exists(m => m.Data.isStoryMission);

    /// <summary>
    /// Ist diese Mission abgeschlossen? Einstiegspunkt für alles was an Story-Fortschritt
    /// hängt — z.B. welche Samen der Markt-NPC überhaupt anbietet.
    /// </summary>
    public bool IsMissionCompleted(string missionId) =>
        !string.IsNullOrWhiteSpace(missionId) && completedMissionIds.Contains(missionId);

    /// <summary>Nächste noch nicht abgeschlossene Story-Mission starten, falls keine aktiv ist.</summary>
    public void AdvanceStoryChain()
    {
        if (storyChain == null) return;
        if (HasActiveStoryMission) return;

        foreach (var mission in storyChain)
        {
            if (mission == null) continue;
            if (completedMissionIds.Contains(mission.missionId)) continue;

            // Nicht überspringen wenn die Voraussetzungen fehlen — die Kette ist
            // geordnet, ein Loch darin soll auffallen statt still den nächsten
            // Schritt vorzuziehen.
            if (!ArePrerequisitesMet(mission)) return;

            // Akt-Auftakt: hier hält die Kette bewusst an. Die Mission kommt aus dem
            // NPC-Dialog, die UI zeigt solange mission.nextStepHint ("Sprich mit …").
            if (mission.startedByDialogue) return;

            StartMission(mission);
            return;
        }
    }

    /// <summary>
    /// Prüft alle Nebenmissionen und startet die, deren Voraussetzungen jetzt erfüllt sind.
    /// </summary>
    public void RefreshAvailableSideMissions()
    {
        if (sideMissions == null) return;

        foreach (var mission in sideMissions)
        {
            if (mission == null) continue;
            if (completedMissionIds.Contains(mission.missionId)) continue;
            if (activeMissions.Exists(m => m.Data.missionId == mission.missionId)) continue;
            if (!mission.autoStartWhenAvailable) continue;
            if (!ArePrerequisitesMet(mission)) continue;

            StartMission(mission);
        }
    }

    // --- Event Handler ---

    private void HandleTilled() =>
        ReportProgress(MissionObjectiveType.TillField, null, 1);

    private void HandlePlanted(PlantType type) =>
        ReportProgress(MissionObjectiveType.PlantCrop, type, 1);

    private void HandleWatered(PlantType type, int amount) =>
        ReportProgress(MissionObjectiveType.WaterCrop, type, Mathf.Max(1, amount));

    private void HandleStationPlaced() =>
        ReportProgress(MissionObjectiveType.PlaceStation, null, 1);

    private void HandleModuleInstalled(AutomationDeviceType _) =>
        ReportProgress(MissionObjectiveType.InstallModule, null, 1);

    private void HandleStationUpgraded(int newLevel)
    {
        ReportProgress(MissionObjectiveType.UpgradeStation, null, 1);
        ReportProgress(MissionObjectiveType.StationLevelReached, null, newLevel);
    }

    private void HandleAllModulesMaxed() =>
        ReportProgress(MissionObjectiveType.AllModulesMaxed, null, 1);

    private void HandleComposterLevelChanged(int newLevel) =>
        ReportProgress(MissionObjectiveType.ComposterLevelReached, null, newLevel);

    private void HandleHarvested(PlantType type)
    {
        ReportProgress(MissionObjectiveType.HarvestCrop, type, 1);

        // Zusaetzlich, nicht statt: bestehende HarvestCrop-Ziele (auch die Meilensteine
        // mit countsAutomatedActions) sollen automatische Ernten weiter mitzaehlen. Diese
        // beiden Ziele sind NUR fuer Missionen gedacht, die eine bestimmte Quelle
        // verlangen — Handarbeit darf AutomationHarvest nicht erfuellen koennen und
        // umgekehrt.
        if (ToolUseHandler.CurrentJobSource == ToolJobSource.Automation)
            ReportProgress(MissionObjectiveType.AutomationHarvest, type, 1);
        else
            ReportProgress(MissionObjectiveType.ManualHarvest, type, 1);
    }

    private void HandleCropSold(PlantType type, int amount) =>
        ReportProgress(MissionObjectiveType.SellCrop, type, amount);

    private void HandleFertilized() =>
        ReportProgress(MissionObjectiveType.FertilizeField, null, 1);

    // Menge statt 1: "kompostiere 10" soll die Stuecke zaehlen, nicht die Wuerfe — sonst
    // waere es mit zehn Ein-Stueck-Wuerfen erfuellt und der Spieler haette nie gesehen,
    // dass ein grosser Batch effizienter ist.
    private void HandleCompostStarted(int crops) =>
        ReportProgress(MissionObjectiveType.CompostCrops, null, Mathf.Max(0, crops));

    private void HandleFertilizerCollected(int amount) =>
        ReportProgress(MissionObjectiveType.CollectFertilizer, null, Mathf.Max(0, amount));

    private void HandleBuildModeEntered() => ReportProgress(MissionObjectiveType.EnterBuildMode, null, 1);
    private void HandleBuildModeExited()  => ReportProgress(MissionObjectiveType.ExitBuildMode, null, 1);
    private void HandleFarmTilePlaced()   => ReportProgress(MissionObjectiveType.PlaceFarmTile, null, 1);
    private void HandleTraveledToMarket() => ReportProgress(MissionObjectiveType.TravelToMarket, null, 1);
    private void HandleTraveledToFarm()   => ReportProgress(MissionObjectiveType.TravelToFarm, null, 1);
    private void HandleBarnOpened()       => ReportProgress(MissionObjectiveType.OpenBarn, null, 1);
    private void HandleSeedBought(PlantType type, int amount) => ReportProgress(MissionObjectiveType.BuySeed, type, amount);
    private void HandleMoneyEarned(int amount) => ReportProgress(MissionObjectiveType.EarnMoney, null, amount);

    private void HandleToolAcquired(ToolType tool) =>
        ReportProgress(MissionObjectiveType.AcquireTool, null, 1, tool);

    private void HandleToolUpgraded(ToolType tool)
    {
        ReportProgress(MissionObjectiveType.UpgradeTool, null, 1, tool);

        // Zusätzlich als Zustand melden: hier ist der Betrag die erreichte STUFE.
        int level = ToolRegistry.Instance != null ? ToolRegistry.Instance.GetLevel(tool) : 0;
        ReportProgress(MissionObjectiveType.ToolLevelReached, null, level, tool);
    }

    private void HandleLicenseBought(string licenseId) =>
        ReportProgress(MissionObjectiveType.BuyLicense, null, 1, ToolType.None, null, licenseId);

    private void HandleActionStacked() =>
        ReportProgress(MissionObjectiveType.QueueActions, null, 1);

    private void HandleZonePurchased(string zoneId) =>
        ReportProgress(MissionObjectiveType.UnlockZone, null, 1, ToolType.None, zoneId);

    private void HandleToolSelected(ToolType tool) =>
        ReportProgress(MissionObjectiveType.SelectTool, null, 1, tool);

    /// <summary>
    /// Liefert dieser Objective-Typ überhaupt ein Werkzeug mit?
    ///
    /// Ohne diese Prüfung ist targetTool eine Falle: setzt man es versehentlich auf ein
    /// Ziel wie TillField — dessen Event gar keinen ToolType kennt — vergleicht der Filter
    /// gegen ToolType.None und verwirft **jeden** Fortschritt. Die Mission wird dann nie
    /// fertig, ohne dass irgendwo ein Fehler auftaucht. Genau so ist "Hacke 6 Felder um"
    /// beim ersten Playtest hängen geblieben.
    /// </summary>
    private static bool CarriesTool(MissionObjectiveType type) =>
        type is MissionObjectiveType.AcquireTool
             or MissionObjectiveType.SelectTool
             or MissionObjectiveType.UpgradeTool
             or MissionObjectiveType.ToolLevelReached;

    /// <summary>
    /// Meldet dieser Typ einen ZUSTAND statt eines Ereignisses? Dann wird der Fortschritt
    /// gesetzt statt addiert — sonst summierte sich z.B. die Werkzeugstufe auf (1+2+3…).
    /// </summary>
    /// <summary>
    /// Zustandsbasierte Ziele einmal mit der Wirklichkeit abgleichen.
    ///
    /// Ohne das hinge eine Mission wie "Bring die Hacke auf Stufe 10" fest, wenn die Hacke
    /// beim Missionsstart schon dort ist — es käme nie wieder ein Upgrade-Event.
    /// </summary>
    private void SyncAbsoluteObjectives(MissionState state)
    {
        var objectives = state.Data.objectives;
        if (objectives == null) return;

        for (int i = 0; i < objectives.Length; i++)
        {
            var obj = objectives[i];
            if (obj.type != MissionObjectiveType.ToolLevelReached) continue;
            if (ToolRegistry.Instance == null) continue;
            if (obj.targetTool == ToolType.None) continue;

            state.SetProgress(i, ToolRegistry.Instance.GetLevel(obj.targetTool));
        }
    }

    private static bool IsAbsolute(MissionObjectiveType type) =>
        type is MissionObjectiveType.ToolLevelReached
             or MissionObjectiveType.StationLevelReached
             or MissionObjectiveType.ComposterLevelReached;

    private void ReportProgress(MissionObjectiveType type, PlantType plantType, int amount,
                                ToolType tool = ToolType.None, string zoneId = null,
                                string licenseId = null)
    {
        // Ein einziger Filter fuer alles: laeuft der Fortschritt aus einem Automatik-Job,
        // zaehlt er nur fuer Missionen, die das ausdruecklich erlauben.
        bool fromAutomation = ToolUseHandler.CurrentJobSource == ToolJobSource.Automation;

        for (int m = activeMissions.Count - 1; m >= 0; m--)
        {
            var state = activeMissions[m];
            if (fromAutomation && !state.Data.countsAutomatedActions) continue;

            bool missionUpdated = false;

            for (int i = 0; i < state.Data.objectives.Length; i++)
            {
                // Sequenz und Stufen stecken beide in IsObjectiveActive — eine Regel,
                // damit Fortschritt und Highlighting nicht auseinanderlaufen koennen.
                if (!state.IsObjectiveActive(i)) continue;

                var obj = state.Data.objectives[i];
                if (obj.type != type) continue;
                if (obj.targetPlantType != null && obj.targetPlantType != plantType) continue;
                if (CarriesTool(type) && obj.targetTool != ToolType.None && obj.targetTool != tool) continue;
                if (!string.IsNullOrWhiteSpace(obj.targetZoneId) && obj.targetZoneId != zoneId) continue;
                if (!string.IsNullOrWhiteSpace(obj.targetLicenseId) && obj.targetLicenseId != licenseId) continue;

                if (IsAbsolute(type))
                    state.SetProgress(i, amount);
                else
                    state.AddProgress(i, amount);

                missionUpdated = true;

                OnObjectiveUpdated?.Invoke(state.Data, i, state.GetProgress(i), obj.requiredAmount);
            }

            if (missionUpdated && state.IsCompleted())
                CompleteMission(state);
        }
    }

    private void CompleteMission(MissionState state)
    {
        activeMissions.Remove(state);
        completedMissionIds.Add(state.Data.missionId);

        // Belohnung wird NICHT sofort gutgeschrieben, sondern zum Abholen hingelegt.
        // Ohne diesen Zwischenschritt gäbe es keinen Moment, an dem die Münzen fliegen
        // können — das Geld wäre schon da, bevor der Spieler den Abschluss überhaupt sieht.
        //
        // Hintergrund-Achievements sind die Ausnahme: sie haben keinen Quest-Tracker-Eintrag
        // (MissionsUI überspringt sie) und damit auch keine Abhol-Karte — die Belohnung läge
        // sonst für immer unerreichbar in pendingRewards. Direkt gutschreiben statt liegen
        // lassen; ein Fanfaren-Moment passt zu einem stillen Hintergrund-Ziel ohnehin nicht.
        if (state.Data.isBackgroundAchievement)
        {
            if (HasRewards(state.Data))
            {
                foreach (var reward in state.Data.rewards)
                    GrantReward(reward);
                OnRewardsCollected?.Invoke(state.Data);
            }
        }
        else if (HasRewards(state.Data))
        {
            pendingRewards[state.Data.missionId] = state.Data;
            OnRewardsPending?.Invoke(state.Data);
        }

        OnMissionCompleted?.Invoke(state.Data);
        Debug.Log($"[MissionManager] Mission abgeschlossen: {state.Data.title}");

        FarmSaveManager.Instance?.RequestSave();

        // Explizit verlinkte Folgemissionen starten
        if (state.Data.unlocks != null)
        {
            foreach (var next in state.Data.unlocks)
                StartMission(next);
        }

        // Story-Kette weiterführen — bewusst nach JEDEM Abschluss, nicht nur nach
        // Story-Missionen. Das Tutorial ist keine Story-Mission; mit der alten Bedingung
        // blieb die Kette direkt nach dem Tutorial stehen, also genau an der Stelle wo
        // der Spieler zum ersten Mal alleine dasteht.
        if (autoStartStoryChain)
            AdvanceStoryChain();

        // Jeder Abschluss kann Nebenmissionen freischalten — auch der einer Nebenmission.
        RefreshAvailableSideMissions();
    }

    // --- Belohnungen abholen ---

    private static bool HasRewards(MissionData data) =>
        data?.rewards != null && data.rewards.Length > 0;

    /// <summary>Missionen deren Belohnung noch abgeholt werden kann.</summary>
    public IEnumerable<MissionData> PendingRewardMissions => pendingRewards.Values;

    public bool HasPendingRewards(string missionId) =>
        !string.IsNullOrWhiteSpace(missionId) && pendingRewards.ContainsKey(missionId);

    /// <summary>
    /// Holt die Belohnung einer abgeschlossenen Mission ab und schreibt sie gut.
    /// Gibt false zurück wenn dort nichts (mehr) liegt — schützt gegen Doppelklicks
    /// auf der Abhol-Karte.
    /// </summary>
    public bool TryCollectRewards(string missionId)
    {
        if (string.IsNullOrWhiteSpace(missionId)) return false;
        if (!pendingRewards.TryGetValue(missionId, out var data)) return false;

        pendingRewards.Remove(missionId);

        if (data.rewards != null)
        {
            foreach (var reward in data.rewards)
                GrantReward(reward);
        }

        OnRewardsCollected?.Invoke(data);
        FarmSaveManager.Instance?.RequestSave();
        return true;
    }

    private void GrantReward(MissionReward reward)
    {
        if (reward == null) return;

        switch (reward.type)
        {
            case MissionReward.RewardType.Money:
                if (reward.amount > 0)
                    PlayerInventory.Instance?.AddMoney(reward.amount);
                break;

            case MissionReward.RewardType.Seed:
                if (reward.plant != null && reward.amount > 0)
                    PlayerInventory.Instance?.AddSeed(reward.plant, reward.amount);
                break;

            case MissionReward.RewardType.Tool:
                if (reward.tool != ToolType.None)
                    ToolRegistry.Instance?.OwnTool(reward.tool);
                break;

            case MissionReward.RewardType.UnlockZone:
                UnlockZoneByName(reward.zoneName);
                break;

            case MissionReward.RewardType.License:
                LicenseRegistry.Instance?.Grant(reward.licenseId);
                break;
        }
    }

    /// <summary>
    /// Schaltet eine Zone frei, ohne Gold abzuziehen — deshalb direkt zone.Unlock()
    /// statt ZoneManager.TryUnlockZone(): die Zone ist hier die Belohnung, nicht der Kauf.
    /// </summary>
    private void UnlockZoneByName(string zoneName)
    {
        if (string.IsNullOrWhiteSpace(zoneName)) return;

        // Über ZoneManager statt FindObjectsByType: der hängt an OnUnlocked und entsperrt
        // dadurch auch die Tiles. Ein direktes zone.Unlock() räumte früher nur die Blocker
        // weg und ließ die Fläche unbespielbar zurück.
        var zone = ZoneManager.Instance != null
            ? ZoneManager.Instance.FindZone(zoneName)
            : null;

        if (zone == null)
        {
            Debug.LogWarning($"[MissionManager] Zone '{zoneName}' für Missions-Belohnung nicht gefunden.");
            return;
        }

        if (zone.IsUnlocked) return;
        zone.Unlock();
    }

    // --- Save / Load ---

    public List<MissionProgressSaveData> GetSaveData()
    {
        var list = new List<MissionProgressSaveData>();

        foreach (var state in activeMissions)
        {
            list.Add(new MissionProgressSaveData
            {
                missionId = state.Data.missionId,
                isActive = true,
                isCompleted = false,
                objectiveProgress = new List<int>(state.GetAllProgress())
            });
        }

        foreach (var id in completedMissionIds)
        {
            list.Add(new MissionProgressSaveData
            {
                missionId = id,
                isActive = false,
                isCompleted = true,
                objectiveProgress = new List<int>(),
                rewardsPending = pendingRewards.ContainsKey(id)
            });
        }

        return list;
    }

    public void ApplyLoadedData(List<MissionProgressSaveData> saved)
    {
        if (saved == null) return;

        activeMissions.Clear();
        completedMissionIds.Clear();
        pendingRewards.Clear();

        var allMissions = GetAllMissions();

        foreach (var save in saved)
        {
            if (string.IsNullOrEmpty(save.missionId)) continue;

            if (save.isCompleted)
            {
                completedMissionIds.Add(save.missionId);

                if (save.rewardsPending)
                {
                    var pending = allMissions.Find(m => m != null && m.missionId == save.missionId);
                    if (pending != null && HasRewards(pending))
                        pendingRewards[save.missionId] = pending;
                }

                continue;
            }

            if (!save.isActive) continue;

            var data = allMissions.Find(m => m != null && m.missionId == save.missionId);
            if (data == null)
            {
                Debug.LogWarning($"[MissionManager] MissionData für '{save.missionId}' nicht gefunden. Wird übersprungen.");
                continue;
            }

            var state = new MissionState(data, save.objectiveProgress);
            activeMissions.Add(state);
        }

        // UI informieren
        foreach (var state in activeMissions)
            OnMissionStarted?.Invoke(state.Data);

        // Abhol-Karten für Belohnungen die vor dem Speichern liegen geblieben sind
        foreach (var data in pendingRewards.Values)
            OnRewardsPending?.Invoke(data);

        Debug.Log($"[MissionManager] {activeMissions.Count} aktive Mission(en), {completedMissionIds.Count} abgeschlossen geladen.");

        // Der eigentliche Bruch im roten Faden: AdvanceStoryChain() wurde bisher NUR nach
        // Abschluss einer Story-Mission aufgerufen. Es gab keinen Einstieg — war die erste
        // Story-Mission nie gestartet (oder ein Save älter als die Kette), stand der Spieler
        // nach dem Tutorial ohne jede Mission da und die Kette lief nie an.
        if (autoStartStoryChain)
            AdvanceStoryChain();

        RefreshAvailableSideMissions();
    }

    /// <summary>Alle konfigurierten Hintergrund-Achievements — unabhaengig davon, ob sie
    /// gerade aktiv, schon abgeschlossen oder (theoretisch) noch gar nicht gestartet
    /// sind. Fuer die Erfolge-Uebersicht, die im Gegensatz zum Quest-Tracker permanent
    /// alle sehen koennen soll, nicht nur die gerade laufenden.</summary>
    public IReadOnlyList<MissionData> BackgroundAchievements
    {
        get
        {
            var result = new List<MissionData>();
            foreach (var data in GetAllMissions())
                if (data != null && data.isBackgroundAchievement)
                    result.Add(data);
            return result;
        }
    }

    /// <summary>Fortschritt eines Achievements, ueber alle seine Ziele aufsummiert — ein
    /// Endgame-Achievement mit mehreren Objectives (z.B. fuenf Werkzeuge auf Stufe 30)
    /// zeigt sich damit als EIN Balken statt fuenf einzelner.</summary>
    public (int current, int required, bool completed) GetAchievementProgress(MissionData data)
    {
        if (data == null || data.objectives == null || data.objectives.Length == 0)
            return (0, 1, false);

        if (IsMissionCompleted(data.missionId))
        {
            int total = 0;
            foreach (var o in data.objectives)
                total += Mathf.Max(1, o?.requiredAmount ?? 1);
            return (total, total, true);
        }

        MissionState state = null;
        foreach (var s2 in activeMissions)
            if (s2.Data.missionId == data.missionId) { state = s2; break; }

        int current = 0, required = 0;
        for (int i = 0; i < data.objectives.Length; i++)
        {
            int need = Mathf.Max(1, data.objectives[i]?.requiredAmount ?? 1);
            required += need;
            current += state != null ? Mathf.Min(state.GetProgress(i), need) : 0;
        }

        return (current, Mathf.Max(1, required), current >= required && required > 0);
    }

    private List<MissionData> GetAllMissions()
    {
        var all = new List<MissionData>();
        if (storyChain != null) all.AddRange(storyChain);
        if (sideMissions != null) all.AddRange(sideMissions);

        // Tutorial-Mission immer einschließen — auch wenn sie nicht in sideMissions eingetragen ist
        var tutorialMission = TutorialManager.Instance?.TutorialMission;
        if (tutorialMission != null && !all.Contains(tutorialMission))
            all.Add(tutorialMission);

        return all;
    }
}

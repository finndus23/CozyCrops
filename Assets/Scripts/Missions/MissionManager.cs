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

    public event Action<MissionData> OnMissionStarted;
    public event Action<MissionData> OnMissionCompleted;

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
        PlayerInventory.OnCropSoldStatic += HandleCropSold;

        BuildModeManager.OnBuildModeEnteredStatic += HandleBuildModeEntered;
        BuildModeManager.OnBuildModeExitedStatic += HandleBuildModeExited;
        TileContextMenu.OnFarmTilePlacedStatic += HandleFarmTilePlaced;
        CarClickHandler.OnTraveledToMarketStatic += HandleTraveledToMarket;
        CarClickHandler.OnTraveledToFarmStatic += HandleTraveledToFarm;
        BarnInteraction.OnBarnOpenedStatic += HandleBarnOpened;
        ToolRegistry.OnToolAcquiredStatic += HandleToolAcquired;
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
        PlayerInventory.OnCropSoldStatic -= HandleCropSold;

        BuildModeManager.OnBuildModeEnteredStatic -= HandleBuildModeEntered;
        BuildModeManager.OnBuildModeExitedStatic -= HandleBuildModeExited;
        TileContextMenu.OnFarmTilePlacedStatic -= HandleFarmTilePlaced;
        CarClickHandler.OnTraveledToMarketStatic -= HandleTraveledToMarket;
        CarClickHandler.OnTraveledToFarmStatic -= HandleTraveledToFarm;
        BarnInteraction.OnBarnOpenedStatic -= HandleBarnOpened;
        ToolRegistry.OnToolAcquiredStatic -= HandleToolAcquired;
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
        activeMissions.Add(state);

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

    /// <summary>Läuft gerade eine Story-Mission?</summary>
    public bool HasActiveStoryMission => activeMissions.Exists(m => m.Data.isStoryMission);

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

    private void HandleWatered(PlantType type) =>
        ReportProgress(MissionObjectiveType.WaterCrop, type, 1);

    private void HandleHarvested(PlantType type) =>
        ReportProgress(MissionObjectiveType.HarvestCrop, type, 1);

    private void HandleCropSold(PlantType type, int amount) =>
        ReportProgress(MissionObjectiveType.SellCrop, type, amount);

    private void HandleBuildModeEntered() => ReportProgress(MissionObjectiveType.EnterBuildMode, null, 1);
    private void HandleBuildModeExited()  => ReportProgress(MissionObjectiveType.ExitBuildMode, null, 1);
    private void HandleFarmTilePlaced()   => ReportProgress(MissionObjectiveType.PlaceFarmTile, null, 1);
    private void HandleTraveledToMarket() => ReportProgress(MissionObjectiveType.TravelToMarket, null, 1);
    private void HandleTraveledToFarm()   => ReportProgress(MissionObjectiveType.TravelToFarm, null, 1);
    private void HandleBarnOpened()       => ReportProgress(MissionObjectiveType.OpenBarn, null, 1);
    private void HandleToolAcquired()     => ReportProgress(MissionObjectiveType.AcquireTool, null, 1);
    private void HandleSeedBought(PlantType type, int amount) => ReportProgress(MissionObjectiveType.BuySeed, type, amount);
    private void HandleMoneyEarned(int amount) => ReportProgress(MissionObjectiveType.EarnMoney, null, amount);
    private void HandleToolSelected(ToolType tool)
    {
        Debug.Log($"[MissionManager] HandleToolSelected({tool}) → ReportProgress SelectTool");
        ReportProgress(MissionObjectiveType.SelectTool, null, 1);
    }

    private void ReportProgress(MissionObjectiveType type, PlantType plantType, int amount)
    {
        Debug.Log($"[MissionManager] ReportProgress: type={type}, activeMissions={activeMissions.Count}");
        for (int m = activeMissions.Count - 1; m >= 0; m--)
        {
            var state = activeMissions[m];
            bool missionUpdated = false;

            // Sequential: nur das erste unvollständige Objective kann Fortschritt machen
            int sequentialLimit = -1;
            if (state.Data.sequentialObjectives)
            {
                for (int j = 0; j < state.Data.objectives.Length; j++)
                {
                    if (!state.ObjectiveCompleted(j)) { sequentialLimit = j; break; }
                }
                Debug.Log($"[MissionManager] Mission '{state.Data.missionId}' sequential, aktives Objective={sequentialLimit}, gesuchter Typ={type}, Objective[{sequentialLimit}].type={(sequentialLimit >= 0 ? state.Data.objectives[sequentialLimit].type.ToString() : "—")}");
                if (sequentialLimit < 0) continue; // Alle fertig
            }

            for (int i = 0; i < state.Data.objectives.Length; i++)
            {
                if (state.Data.sequentialObjectives && i != sequentialLimit) continue;

                var obj = state.Data.objectives[i];
                if (obj.type != type) continue;
                if (state.ObjectiveCompleted(i)) continue;
                if (obj.targetPlantType != null && obj.targetPlantType != plantType) continue;

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

        if (state.Data.rewards != null)
        {
            foreach (var reward in state.Data.rewards)
                GrantReward(reward);
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
        }
    }

    /// <summary>
    /// Schaltet eine Zone frei, ohne Gold abzuziehen — deshalb direkt zone.Unlock()
    /// statt ZoneManager.TryUnlockZone(): die Zone ist hier die Belohnung, nicht der Kauf.
    /// </summary>
    private void UnlockZoneByName(string zoneName)
    {
        if (string.IsNullOrWhiteSpace(zoneName)) return;

        var zones = FindObjectsByType<GridZone>(FindObjectsSortMode.None);
        foreach (var zone in zones)
        {
            if (zone == null || zone.IsUnlocked) continue;
            if (zone.SaveId != zoneName && zone.gameObject.name != zoneName) continue;

            zone.Unlock();
            return;
        }

        Debug.LogWarning($"[MissionManager] Zone '{zoneName}' für Missions-Belohnung nicht gefunden.");
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
                objectiveProgress = new List<int>()
            });
        }

        return list;
    }

    public void ApplyLoadedData(List<MissionProgressSaveData> saved)
    {
        if (saved == null) return;

        activeMissions.Clear();
        completedMissionIds.Clear();

        var allMissions = GetAllMissions();

        foreach (var save in saved)
        {
            if (string.IsNullOrEmpty(save.missionId)) continue;

            if (save.isCompleted)
            {
                completedMissionIds.Add(save.missionId);
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

        Debug.Log($"[MissionManager] {activeMissions.Count} aktive Mission(en), {completedMissionIds.Count} abgeschlossen geladen.");

        // Der eigentliche Bruch im roten Faden: AdvanceStoryChain() wurde bisher NUR nach
        // Abschluss einer Story-Mission aufgerufen. Es gab keinen Einstieg — war die erste
        // Story-Mission nie gestartet (oder ein Save älter als die Kette), stand der Spieler
        // nach dem Tutorial ohne jede Mission da und die Kette lief nie an.
        if (autoStartStoryChain)
            AdvanceStoryChain();

        RefreshAvailableSideMissions();
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

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

/// <summary>
/// Quest-Tracker UI. Zeigt alle aktiven Missionen.
/// Setup: contentRoot (Transform mit VerticalLayoutGroup) + missionEntryPrefab (mit MissionEntryUI).
/// Optional: emptyText (TMP) wenn keine Missionen aktiv sind.
/// </summary>
public class MissionsUI : MonoBehaviour
{
    public static MissionsUI Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private GameObject missionEntryPrefab;

    private readonly Dictionary<string, MissionEntryUI> entries = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
    }

    private void Start()
    {
        if (MissionManager.Instance == null) return;

        MissionManager.Instance.OnMissionStarted += OnMissionStarted;
        MissionManager.Instance.OnMissionCompleted += OnMissionCompleted;
        MissionManager.Instance.OnObjectiveUpdated += OnObjectiveUpdated;

        // Missionen die beim Start bereits aktiv sind (z.B. nach Scene-Load)
        foreach (var state in MissionManager.Instance.ActiveMissions)
        {
            CreateEntry(state.Data);
            SyncProgress(state);
        }

        RefreshEmpty();
    }

    private void OnDestroy()
    {
        if (MissionManager.Instance == null) return;
        MissionManager.Instance.OnMissionStarted -= OnMissionStarted;
        MissionManager.Instance.OnMissionCompleted -= OnMissionCompleted;
        MissionManager.Instance.OnObjectiveUpdated -= OnObjectiveUpdated;
    }

    private void OnMissionStarted(MissionData data)
    {
        CreateEntry(data);

        // Progress aus dem MissionState synchronisieren (wichtig nach Scene-Load/Reload)
        var state = MissionManager.Instance?.ActiveMissions
            .FirstOrDefault(m => m.Data.missionId == data.missionId);
        if (state != null) SyncProgress(state);

        RefreshEmpty();
    }

    private void OnMissionCompleted(MissionData data)
    {
        if (!entries.TryGetValue(data.missionId, out var entry)) return;
        Destroy(entry.gameObject);
        entries.Remove(data.missionId);
        RefreshEmpty();
    }

    private void OnObjectiveUpdated(MissionData data, int objIdx, int current, int required)
    {
        if (entries.TryGetValue(data.missionId, out var entry))
            entry.UpdateObjective(objIdx, current, required);
    }

    private void CreateEntry(MissionData data)
    {
        if (data == null || entries.ContainsKey(data.missionId)) return;
        if (missionEntryPrefab == null || contentRoot == null) return;

        var go = Instantiate(missionEntryPrefab, contentRoot);
        var entry = go.GetComponent<MissionEntryUI>();
        if (entry == null) return;

        entry.Init(data);
        entries[data.missionId] = entry;
    }

    private void SyncProgress(MissionState state)
    {
        if (!entries.TryGetValue(state.Data.missionId, out var entry)) return;

        for (int i = 0; i < state.Data.objectives.Length; i++)
            entry.UpdateObjective(i, state.GetProgress(i), state.Data.objectives[i].requiredAmount);
    }

    private void RefreshEmpty()
    {
        if (panel != null)
            panel.SetActive(entries.Count > 0);
    }
}

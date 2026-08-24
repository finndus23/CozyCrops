using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kleine Übersicht über die Hintergrund-Achievements (Meilenstein- und Endgame-Missionen,
/// <see cref="MissionData.isBackgroundAchievement"/>).
///
/// Diese Missionen laufen ohne Dialog von Spielbeginn an mit, tauchen NICHT im normalen
/// Quest-Tracker auf (<see cref="MissionsUI"/> überspringt sie) und werden vom
/// Quest-Highlighting ignoriert — ohne dieses Panel gäbe es für den Spieler gar keinen Ort,
/// an dem er ihren Fortschritt überhaupt sehen könnte.
///
/// Drei feste Abschnitte, in dieser Reihenfolge:
///  1. "Vollautomatisiert" (endgame_full_automation) — jedes einzelne Ziel als eigene Zeile
///  2. "Vollausrüstung" (endgame_full_toolkit) — jedes Werkzeug als eigene Zeile
///  3. "Meilensteine" — alle übrigen Achievements, je EIN aggregierter Balken
///
/// Die beiden Endgame-Missionen werden ueber ihre missionId erkannt (siehe
/// AutomationMissionId/ToolkitMissionId) und zeigen ihre Ziele einzeln, weil "Station Stufe
/// 12/30, Module 0/1, Komposter 4/10" als EIN Balken keine brauchbare Aussage waere. Alles
/// andere unter isBackgroundAchievement landet unveraendert im Meilenstein-Block.
///
/// Baut sich komplett aus Code auf wie AutomationDevicePopup/ComposterInteraction — kein
/// Prefab nötig für das Panel selbst. Den Umschalt-Knopf gibt es hier NICHT — der lebt als
/// normales UI-Element in der Szene und ruft <see cref="Toggle"/> auf.
///
/// Setup: EIN leeres GameObject mit dieser Komponente irgendwo in der SampleScene ablegen
/// (z.B. neben MissionsUI). Awake() macht das Objekt DontDestroyOnLoad — ein einziges
/// Objekt reicht für Farm und Marktplatz.
/// </summary>
public class AchievementsUI : MonoBehaviour
{
    public static AchievementsUI Instance { get; private set; }

    private const string AutomationMissionId = "endgame_full_automation";
    private const string ToolkitMissionId = "endgame_full_toolkit";

    [Header("Optik")]
    [Tooltip("Leer lassen — wird über HotbarUI aufgelöst.")]
    [SerializeField] private Canvas hudCanvas;

    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private Sprite barBackgroundSprite;
    [SerializeField] private Sprite barFillSprite;

    [SerializeField] private Color barFillColor = new(0.95f, 0.64f, 0.18f, 1f);
    [SerializeField] private Color barCompleteColor = new(0.45f, 0.82f, 0.35f, 1f);
    [SerializeField] private Color sectionHeaderColor = new(0.45f, 0.28f, 0.1f, 1f);

    [Tooltip("Höhe einer einzelnen Ziel-Zeile (Label + Balken).")]
    [SerializeField] private float rowHeight = 48f;

    [Tooltip("Höhe einer Abschnitts-Überschrift.")]
    [SerializeField] private float sectionHeaderHeight = 30f;

    private class Row
    {
        public MissionData data;

        /// <summary>-1 = aggregierter Balken über alle Ziele der Mission (Meilensteine).
        /// Sonst der Index EINES Ziels (Endgame-Abschnitte).</summary>
        public int objectiveIndex = -1;

        public TextMeshProUGUI progressLabel;
        public Image fillImage;
    }

    private GameObject panel;
    private readonly List<Row> rows = new();

    public bool IsOpen => panel != null && panel.activeSelf;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        var manager = MissionManager.Instance;
        if (manager != null)
        {
            manager.OnObjectiveUpdated += HandleMissionChanged;
            manager.OnMissionStarted += HandleMissionChangedData;
            manager.OnMissionCompleted += HandleMissionChangedData;
        }
    }

    void OnDestroy()
    {
        if (Instance != this) return;

        var manager = MissionManager.Instance;
        if (manager != null)
        {
            manager.OnObjectiveUpdated -= HandleMissionChanged;
            manager.OnMissionStarted -= HandleMissionChangedData;
            manager.OnMissionCompleted -= HandleMissionChangedData;
        }
    }

    private void HandleMissionChanged(MissionData data, int index, int current, int required)
    {
        if (IsOpen) RefreshAll();
    }

    private void HandleMissionChangedData(MissionData data)
    {
        if (IsOpen) RefreshAll();
    }

    // ── Öffnen / Schließen ────────────────────────────────────────────────────

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        EnsurePanel();
        if (panel == null) return;

        panel.SetActive(true);
        UiSfx.PanelOpen();
        RefreshAll();
    }

    public void Close()
    {
        bool wasOpen = IsOpen;
        if (panel != null) panel.SetActive(false);
        if (wasOpen) UiSfx.PanelClose();
    }

    // ── Inhalt ────────────────────────────────────────────────────────────────

    private void RefreshAll()
    {
        var manager = MissionManager.Instance;
        if (manager == null) return;

        foreach (var row in rows)
            RefreshRow(manager, row);
    }

    private void RefreshRow(MissionManager manager, Row row)
    {
        if (row?.data == null) return;

        var (current, required, completed) = row.objectiveIndex >= 0
            ? manager.GetObjectiveProgress(row.data, row.objectiveIndex)
            : manager.GetAchievementProgress(row.data);

        float fraction = required > 0 ? Mathf.Clamp01(current / (float)required) : 0f;

        row.progressLabel.text = completed
            ? "Abgeschlossen"
            : $"{current:N0} / {required:N0}";

        row.fillImage.fillAmount = fraction;
        row.fillImage.color = completed ? barCompleteColor : barFillColor;
    }

    // ── Aufbau ────────────────────────────────────────────────────────────────

    private void EnsurePanel()
    {
        if (panel != null) return;

        var canvas = RuntimePopupBuilder.ResolveHudCanvas(hudCanvas);
        if (canvas == null) return;

        var manager = MissionManager.Instance;
        var achievements = manager != null ? manager.BackgroundAchievements : null;

        MissionData automationMission = FindById(achievements, AutomationMissionId);
        MissionData toolkitMission = FindById(achievements, ToolkitMissionId);

        var milestones = new List<MissionData>();
        if (achievements != null)
        {
            foreach (var data in achievements)
            {
                if (data == null) continue;
                if (data == automationMission || data == toolkitMission) continue;
                milestones.Add(data);
            }
        }

        int sectionCount = 0;
        int rowCount = 0;
        if (automationMission != null) { sectionCount++; rowCount += automationMission.objectives?.Length ?? 0; }
        if (toolkitMission != null) { sectionCount++; rowCount += toolkitMission.objectives?.Length ?? 0; }
        if (milestones.Count > 0) { sectionCount++; rowCount += milestones.Count; }

        float panelHeight = rowCount == 0
            ? 180f
            : 110f + sectionCount * sectionHeaderHeight + rowCount * rowHeight;

        panel = RuntimePopupBuilder.CreatePanel(canvas.transform, "AchievementsPanel",
                                                new Vector2(460f, panelHeight), panelSprite);

        float top = panelHeight * 0.5f - 36f;

        var headerLabel = RuntimePopupBuilder.CreateLabel(panel.transform, "Header",
            new Vector2(0f, top), new Vector2(400f, 40f), "Erfolge", 22f);
        headerLabel.fontStyle = FontStyles.Bold;

        RuntimePopupBuilder.CreateButton(panel.transform, "Close",
            new Vector2(198f, top), new Vector2(34f, 34f), "X", buttonSprite, Close);

        float y = top - 44f;

        if (rowCount == 0)
        {
            RuntimePopupBuilder.CreateLabel(panel.transform, "Empty",
                new Vector2(0f, y), new Vector2(400f, 40f),
                "Noch keine Erfolge konfiguriert.", 15f);
        }
        else
        {
            if (automationMission != null)
                y = CreatePerObjectiveSection("Vollautomatisiert", automationMission, y);

            if (toolkitMission != null)
                y = CreatePerObjectiveSection("Vollausrüstung", toolkitMission, y);

            if (milestones.Count > 0)
                y = CreateMilestoneSection("Meilensteine", milestones, y);
        }

        panel.SetActive(false);
    }

    private static MissionData FindById(IReadOnlyList<MissionData> list, string missionId)
    {
        if (list == null) return null;
        foreach (var data in list)
            if (data != null && data.missionId == missionId) return data;
        return null;
    }

    private float CreateSectionHeader(string title, float y)
    {
        var label = RuntimePopupBuilder.CreateLabel(panel.transform, $"Section_{title}",
            new Vector2(0f, y), new Vector2(400f, 24f), title, 17f,
            TextAlignmentOptions.Left);
        label.fontStyle = FontStyles.Bold;
        label.color = sectionHeaderColor;

        return y - sectionHeaderHeight;
    }

    /// <summary>Eine Zeile je Objective der Mission — für die zwei Endgame-Abschnitte.
    /// "Station 12/30, Module 0/1, Komposter 4/10" als EIN Balken wäre keine brauchbare
    /// Aussage, deshalb einzeln statt aggregiert.</summary>
    private float CreatePerObjectiveSection(string title, MissionData data, float y)
    {
        y = CreateSectionHeader(title, y);

        if (data.objectives == null) return y;

        for (int i = 0; i < data.objectives.Length; i++)
        {
            var objective = data.objectives[i];
            if (objective == null) continue;

            // Automatisch generierter deutscher Satz aus dem Objective-Typ — kein
            // manuelles Label pro Ziel nötig, MissionObjectiveFormatter kennt schon alle
            // Typen (auch ToolLevelReached/StationLevelReached/AllModulesMaxed/
            // ComposterLevelReached).
            string label = MissionObjectiveFormatter.Format(objective);
            CreateRow(data, i, label, y);
            y -= rowHeight;
        }

        return y;
    }

    /// <summary>Ein aggregierter Balken je Mission — für die restlichen (einzieligen)
    /// Achievements.</summary>
    private float CreateMilestoneSection(string title, List<MissionData> missions, float y)
    {
        y = CreateSectionHeader(title, y);

        foreach (var data in missions)
        {
            if (data == null) continue;
            CreateRow(data, -1, data.title, y);
            y -= rowHeight;
        }

        return y;
    }

    private void CreateRow(MissionData data, int objectiveIndex, string label, float y)
    {
        var row = new Row { data = data, objectiveIndex = objectiveIndex };
        string uniqueSuffix = $"{data.missionId}_{objectiveIndex}_{rows.Count}";

        // Zentriert bei x=0 mit Breite 400 -> spannt -200..200, sicher innerhalb des 460
        // breiten Panels (Rand bei ±230).
        RuntimePopupBuilder.CreateLabel(panel.transform, $"Row_{uniqueSuffix}_Title",
            new Vector2(0f, y), new Vector2(400f, 20f), label, 13f,
            TextAlignmentOptions.Left);

        row.fillImage = RuntimePopupBuilder.CreateProgressBar(panel.transform, $"Row_{uniqueSuffix}_Bar",
            new Vector2(0f, y - 20f), new Vector2(360f, 14f),
            barBackgroundSprite, barFillSprite,
            new Color(0.2f, 0.15f, 0.1f, 0.6f), barFillColor);

        row.progressLabel = RuntimePopupBuilder.CreateLabel(panel.transform, $"Row_{uniqueSuffix}_Progress",
            new Vector2(0f, y - 20f), new Vector2(360f, 14f), "", 10f);

        rows.Add(row);
    }
}

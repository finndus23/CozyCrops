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
/// Baut sich komplett aus Code auf wie AutomationDevicePopup/ComposterInteraction — kein
/// Prefab nötig. Ein kleiner Umschalt-Knopf oben rechts öffnet/schließt das Panel.
///
/// Setup: EIN leeres GameObject mit dieser Komponente irgendwo in der SampleScene ablegen
/// (z.B. neben MissionsUI). Awake() macht das Objekt DontDestroyOnLoad, genau wie
/// MissionsUI es für sich selbst tut — ein einziges Objekt reicht für Farm und Marktplatz.
/// </summary>
public class AchievementsUI : MonoBehaviour
{
    public static AchievementsUI Instance { get; private set; }

    [Header("Optik")]
    [Tooltip("Leer lassen — wird über HotbarUI aufgelöst.")]
    [SerializeField] private Canvas hudCanvas;

    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private Sprite barBackgroundSprite;
    [SerializeField] private Sprite barFillSprite;

    [SerializeField] private Color barFillColor = new(0.95f, 0.64f, 0.18f, 1f);
    [SerializeField] private Color barCompleteColor = new(0.45f, 0.82f, 0.35f, 1f);

    [Tooltip("Position des Umschalt-Knopfs, oben rechts vom Panel-Pivot aus.")]
    [SerializeField] private Vector2 toggleButtonPosition = new(-540f, -40f);

    private class Row
    {
        public MissionData data;
        public TextMeshProUGUI titleLabel;
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
        EnsureToggleButton();

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

        var (current, required, completed) = manager.GetAchievementProgress(row.data);
        float fraction = required > 0 ? Mathf.Clamp01(current / (float)required) : 0f;

        row.titleLabel.text = row.data.title;
        row.progressLabel.text = completed
            ? "Abgeschlossen"
            : $"{current:N0} / {required:N0}";

        row.fillImage.fillAmount = fraction;
        row.fillImage.color = completed ? barCompleteColor : barFillColor;
    }

    // ── Aufbau ────────────────────────────────────────────────────────────────

    private void EnsureToggleButton()
    {
        var canvas = RuntimePopupBuilder.ResolveHudCanvas(hudCanvas);
        if (canvas == null) return;

        RuntimePopupBuilder.CreateButton(canvas.transform, "AchievementsToggle",
            toggleButtonPosition, new Vector2(140f, 40f), "Erfolge", buttonSprite, Toggle);
    }

    private void EnsurePanel()
    {
        if (panel != null) return;

        var canvas = RuntimePopupBuilder.ResolveHudCanvas(hudCanvas);
        if (canvas == null) return;

        var manager = MissionManager.Instance;
        var achievements = manager != null ? manager.BackgroundAchievements : null;
        int count = achievements?.Count ?? 0;

        float rowHeight = 60f;
        float panelHeight = 110f + count * rowHeight;

        panel = RuntimePopupBuilder.CreatePanel(canvas.transform, "AchievementsPanel",
                                                new Vector2(460f, panelHeight), panelSprite);

        float top = panelHeight * 0.5f - 36f;

        var headerLabel = RuntimePopupBuilder.CreateLabel(panel.transform, "Header",
            new Vector2(0f, top), new Vector2(400f, 40f), "Erfolge", 22f);
        headerLabel.fontStyle = FontStyles.Bold;

        RuntimePopupBuilder.CreateButton(panel.transform, "Close",
            new Vector2(198f, top), new Vector2(34f, 34f), "X", buttonSprite, Close);

        float y = top - 44f;

        if (count == 0)
        {
            RuntimePopupBuilder.CreateLabel(panel.transform, "Empty",
                new Vector2(0f, y), new Vector2(400f, 40f),
                "Noch keine Erfolge konfiguriert.", 15f);
        }
        else
        {
            foreach (var data in achievements)
            {
                if (data == null) continue;
                CreateRow(data, y);
                y -= rowHeight;
            }
        }

        panel.SetActive(false);
    }

    private void CreateRow(MissionData data, float y)
    {
        var row = new Row { data = data };

        row.titleLabel = RuntimePopupBuilder.CreateLabel(panel.transform, $"Row_{data.missionId}_Title",
            new Vector2(-200f, y), new Vector2(400f, 22f), data.title, 15f,
            TMPro.TextAlignmentOptions.Left);

        row.fillImage = RuntimePopupBuilder.CreateProgressBar(panel.transform, $"Row_{data.missionId}_Bar",
            new Vector2(0f, y - 20f), new Vector2(360f, 16f),
            barBackgroundSprite, barFillSprite,
            new Color(0.2f, 0.15f, 0.1f, 0.6f), barFillColor);

        row.progressLabel = RuntimePopupBuilder.CreateLabel(panel.transform, $"Row_{data.missionId}_Progress",
            new Vector2(0f, y - 20f), new Vector2(360f, 16f), "", 12f);

        rows.Add(row);
    }
}

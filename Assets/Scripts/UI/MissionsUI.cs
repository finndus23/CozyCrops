using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
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
    [SerializeField] private Sprite panelSprite;

    [Header("Panel-Größe")]
    [Tooltip("Skaliert das GESAMTE Panel — Rahmen, Schrift und Abstände gleichermaßen.\n" +
             "Ein Regler statt Breite, Padding, borderScale und die Schriftgrößen im " +
             "MissionEntry-Prefab einzeln aufeinander abstimmen zu müssen.")]
    [SerializeField] private float panelScale = 1.3f;

    [Tooltip("Position der oberen linken Ecke. Unabhängig von panelScale, " +
             "weil der Pivot oben links sitzt.")]
    [SerializeField] private Vector2 panelPosition = new(18f, -78f);
    [Tooltip("Feste Breite. Die Höhe ergibt sich aus dem Inhalt.")]
    [SerializeField] private float panelWidth = 430f;
    [Tooltip("Mindesthöhe — darunter sähe der Rahmen gestaucht aus, " +
             "weil Kopfbanner und Fußleiste zusammen schon Platz brauchen.")]
    [SerializeField] private float minPanelHeight = 230f;

    [Header("Innenabstände (Rahmen freihalten)")]
    [Tooltip("Muss zur gerenderten Breite der Rahmen-Grafik passen. " +
             "Oben ist am größten, da sitzt das Holz-Kopfbanner.")]
    [SerializeField] private float padLeft = 68f;
    [SerializeField] private float padRight = 68f;
    [SerializeField] private float padTop = 122f;
    [SerializeField] private float padBottom = 80f;
    [SerializeField] private float entrySpacing = 10f;

    [Tooltip("Verkleinert die 9-Slice-Ränder. 1 = Originalgröße der Grafik (viel zu wuchtig), " +
             "höher = feinerer Rahmen.")]
    [SerializeField] private float borderScale = 3f;

    private readonly Dictionary<string, MissionEntryUI> entries = new();

    private const string BackgroundName = "PanelBackground";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
        ApplyPanelStyle();
    }

    private void Start()
    {
        ApplyPanelStyle();

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
        if (panel == null)
            panel = gameObject;

        if (panel != null)
            panel.SetActive(entries.Count > 0);
    }

    /// <summary>
    /// Im Editor per Rechtsklick auf den Kopf der MissionsUI-Komponente aufrufbar.
    ///
    /// Das Layout wird sonst ausschließlich zur Laufzeit gesetzt (Awake/Start) — im
    /// Scene-View sieht man von den Inspector-Werten also nichts, und ob eine Einstellung
    /// überhaupt greift, merkt man erst im Playtest. Der Menüpunkt wendet alles sofort an
    /// und schreibt das Ergebnis in die Konsole.
    /// </summary>
    [ContextMenu("Panel-Layout jetzt anwenden")]
    private void ApplyPanelStyleFromMenu() => ApplyPanelStyle(true);

    private void ApplyPanelStyle() => ApplyPanelStyle(false);

    /// <summary>
    /// Baut das Panel so auf, dass die Höhe dem Inhalt folgt.
    ///
    /// Vorher stand hier eine feste Größe (300×400) und ContentSizeFitter sowie
    /// VerticalLayoutGroup wurden aktiv <c>enabled = false</c> gesetzt — das Panel konnte
    /// also gar nicht mitwachsen, und von den 300 Breite blieben nach den Rahmen-Insets
    /// nur ~200 nutzbar.
    ///
    /// Jetzt: das Panel hat eine VerticalLayoutGroup (Padding hält den gemalten Rahmen frei)
    /// und einen ContentSizeFitter auf PreferredSize in der Höhe. Die Kette ist
    /// Eintrag → contentRoot → Panel: jede Ebene meldet ihre Wunschhöhe nach oben.
    ///
    /// Voraussetzung dafür ist das 9-Slicing der Rahmengrafik (Border im Import gesetzt):
    /// Kopfbanner, Fußleiste und Ecken bleiben in Originalgröße, nur die Mitte wird
    /// gestreckt. Ohne das würde das Holzschild oben bei jeder Höhenänderung mitverzerren.
    /// </summary>
    private void ApplyPanelStyle(bool verbose)
    {
        if (panel == null)
            panel = gameObject;

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            // Pivot oben links: das Panel wächst nach unten, die Kopfzeile bleibt liegen.
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = panelPosition;
            panelRect.sizeDelta = new Vector2(panelWidth, panelRect.sizeDelta.y);
            // Skalierung statt größerer Werte: so bleiben Rahmenstärke, Schrift und
            // Innenabstände in genau dem Verhältnis zueinander, das jetzt passt.
            panelRect.localScale = Vector3.one * Mathf.Max(0.1f, panelScale);
        }

        Image panelImage = EnsureBackgroundImage();

        var panelLayout = GetOrAdd<VerticalLayoutGroup>(panel);
        panelLayout.enabled = true;
        panelLayout.padding = new RectOffset(
            Mathf.RoundToInt(padLeft), Mathf.RoundToInt(padRight),
            Mathf.RoundToInt(padTop), Mathf.RoundToInt(padBottom));
        panelLayout.spacing = 0f;
        panelLayout.childAlignment = TextAnchor.UpperLeft;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        // Mindesthöhe: ContentSizeFitter kennt keine Untergrenze, LayoutUtility nimmt
        // aber das Maximum aus minHeight und preferredHeight.
        var panelElement = GetOrAdd<LayoutElement>(panel);
        panelElement.minHeight = minPanelHeight;
        panelElement.preferredHeight = -1f;

        var fitter = GetOrAdd<ContentSizeFitter>(panel);
        fitter.enabled = true;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; // Breite bleibt fest
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;   // Höhe folgt dem Inhalt

        if (contentRoot == null) return;

        // contentRoot wird jetzt von der Layout-Group des Panels positioniert und
        // vermessen. Die alten Anker-/Offset-Werte dürfen dem nicht mehr reinreden.
        var contentLayout = GetOrAdd<VerticalLayoutGroup>(contentRoot.gameObject);
        contentLayout.enabled = true;
        contentLayout.padding = new RectOffset(0, 0, 0, 0);
        contentLayout.spacing = entrySpacing;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        // Layout sofort neu rechnen, sonst greift die neue Größe erst im nächsten Frame —
        // im Editor (kein Frame) gar nicht.
        if (panelRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

        if (verbose)
        {
            Vector2 raw = panelRect != null ? panelRect.rect.size : Vector2.zero;
            Debug.Log($"[MissionsUI] Panel='{panel.name}' auf {raw} gesetzt, " +
                      $"×{panelScale} skaliert = {raw * panelScale} auf dem Bildschirm " +
                      $"(Breite {panelWidth}, minHöhe {minPanelHeight}). " +
                      $"Image={(panelImage != null ? panelImage.sprite?.name ?? "kein Sprite" : "FEHLT")}, " +
                      $"Border={panelImage?.sprite?.border}, " +
                      $"contentRoot='{contentRoot.name}' mit {contentRoot.childCount} Einträgen.", panel);
        }
    }

    /// <summary>
    /// Legt die Rahmengrafik auf ein eigenes Hintergrund-Kind statt auf das Panel selbst.
    ///
    /// Das ist der Grund, warum die Höhe vorher auf 540 festhing: <see cref="Image"/> ist
    /// selbst ein ILayoutElement und meldet bei 9-Slice die Summe seiner Ränder als
    /// preferredHeight (hier 330 oben + 210 unten). Der ContentSizeFitter nimmt das
    /// Maximum über alle Layout-Elemente am selben Objekt — das Sprite hat also immer
    /// gegen den Inhalt gewonnen, und das Panel konnte nie kleiner werden als sein Rahmen.
    ///
    /// Liegt der Hintergrund auf einem Kind, taucht er in dieser Rechnung nicht mehr auf.
    /// Das Image auf dem Root wird nur deaktiviert, nicht zerstört: LayoutUtility
    /// überspringt deaktivierte Behaviours, und so bleibt die Szene reparierbar.
    /// </summary>
    private Image EnsureBackgroundImage()
    {
        var rootImage = panel.GetComponent<Image>();
        if (rootImage != null)
            rootImage.enabled = false;

        var panelTransform = panel.transform;
        Transform existing = panelTransform.Find(BackgroundName);

        RectTransform bgRect;
        if (existing != null)
        {
            bgRect = existing as RectTransform;
        }
        else
        {
            var go = new GameObject(BackgroundName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgRect = go.GetComponent<RectTransform>();
            bgRect.SetParent(panelTransform, false);
        }

        if (bgRect == null) return null;

        // Ganz nach hinten, damit der Rahmen hinter den Missions-Einträgen liegt.
        bgRect.SetAsFirstSibling();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Die VerticalLayoutGroup des Panels darf den Hintergrund nicht wie einen
        // Eintrag einsortieren — er soll die volle Fläche füllen.
        var ignore = GetOrAdd<LayoutElement>(bgRect.gameObject);
        ignore.ignoreLayout = true;

        var image = GetOrAdd<Image>(bgRect.gameObject);
        image.enabled = true;
        image.sprite = panelSprite;
        image.color = Color.white;
        image.type = Image.Type.Sliced;
        image.preserveAspect = false;
        image.raycastTarget = false;
        // Die Grafik ist 1086×1448 — bei Originalgröße wäre allein der Rahmen
        // breiter als das ganze Panel.
        image.pixelsPerUnitMultiplier = Mathf.Max(0.01f, borderScale);

        return image;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component =>
        go.TryGetComponent<T>(out var existing) ? existing : go.AddComponent<T>();
}

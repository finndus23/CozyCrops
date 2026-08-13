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

    [Header("Kopfbanner (Missionstitel)")]
    [Tooltip("Optional. Leer lassen — wird zur Laufzeit angelegt.\n\n" +
             "Sitzt im Holz-Banner der Rahmengrafik, also OBERHALB des Inhaltsbereichs. " +
             "Liegt bewusst am Panel und nicht im contentRoot: die VerticalLayoutGroup " +
             "würde es sonst unter den Rahmen schieben.")]
    [SerializeField] private TMP_Text bannerTitleText;

    [Tooltip("AUS = der Code fasst Position und Größe des Titels nie an, du schiebst ihn " +
             "im Scene-View selbst zurecht.\n\n" +
             "Vorgehen: Rechtsklick auf die Komponente → 'Panel-Layout jetzt anwenden' legt " +
             "'BannerTitle' als echtes Kind des Panels an. Dann diesen Haken raus, das Objekt " +
             "frei positionieren und die Szene speichern.")]
    [SerializeField] private bool autoPositionBannerTitle = true;

    [Tooltip("Abstand von der Panel-Oberkante bis zur Oberkante des Titels. " +
             "Muss zur Höhe des Holz-Banners in Quest.png passen. " +
             "Nur wirksam solange 'Auto Position Banner Title' an ist.")]
    [SerializeField] private float bannerTitleTop = 26f;

    [Tooltip("Höhe des Titelfelds im Banner.")]
    [SerializeField] private float bannerTitleHeight = 58f;

    [Tooltip("Seitlicher Rand — das Banner ist schmaler als das Panel.")]
    [SerializeField] private float bannerTitleSideInset = 96f;

    [SerializeField] private float bannerTitleFontSize = 22f;
    [SerializeField] private Color bannerTitleColor = new(0.98f, 0.93f, 0.78f, 1f);

    [Tooltip("Wenn das Banner den Titel zeigt, blendet der oberste Eintrag seinen eigenen aus — " +
             "sonst steht er doppelt da.")]
    [SerializeField] private bool hideInlineTitleOfFirstEntry = true;

    [Header("Nächster Schritt")]
    [Tooltip("Optional. Leer lassen — dann wird das Label zur Laufzeit angelegt.\n\n" +
             "Zeigt MissionData.nextStepHint wenn gerade KEINE Mission läuft, die Kette aber " +
             "weitergeht. Genau der Fall bei Akt-Auftakten (startedByDialogue): dort wartet " +
             "die Kette auf ein NPC-Gespräch und das Panel wäre sonst einfach leer.")]
    [SerializeField] private TMP_Text nextStepText;
    [SerializeField] private float nextStepFontSize = 20f;
    [SerializeField] private Color nextStepColor = new(0.32f, 0.24f, 0.14f);

    private readonly Dictionary<string, MissionEntryUI> entries = new();

    /// <summary>Missionen deren Eintrag gerade als Abhol-Karte stehen bleibt.</summary>
    private readonly HashSet<string> awaitingCollect = new();

    private const string BackgroundName = "PanelBackground";
    private const string NextStepName = "NextStepHint";
    private const string BannerTitleName = "BannerTitle";

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
        MissionManager.Instance.OnRewardsPending += OnRewardsPending;
        MissionManager.Instance.OnRewardsCollected += OnRewardsCollected;

        // Missionen die beim Start bereits aktiv sind (z.B. nach Scene-Load)
        foreach (var state in MissionManager.Instance.ActiveMissions)
        {
            CreateEntry(state.Data);
            SyncProgress(state);
        }

        // Nicht abgeholte Belohnungen nachtragen. Das Load-System feuert OnRewardsPending
        // schon in ApplyLoadedData — das kann vor diesem Start() liegen, dann wäre das
        // Event ins Leere gegangen und die Beute unsichtbar (aber weiter im Save).
        foreach (var data in MissionManager.Instance.PendingRewardMissions)
            OnRewardsPending(data);

        RefreshPanel();
    }

    private void OnDestroy()
    {
        if (MissionManager.Instance == null) return;
        MissionManager.Instance.OnMissionStarted -= OnMissionStarted;
        MissionManager.Instance.OnMissionCompleted -= OnMissionCompleted;
        MissionManager.Instance.OnObjectiveUpdated -= OnObjectiveUpdated;
        MissionManager.Instance.OnRewardsPending -= OnRewardsPending;
        MissionManager.Instance.OnRewardsCollected -= OnRewardsCollected;
    }

    private void OnMissionStarted(MissionData data)
    {
        CreateEntry(data);

        // Progress aus dem MissionState synchronisieren (wichtig nach Scene-Load/Reload)
        var state = MissionManager.Instance?.ActiveMissions
            .FirstOrDefault(m => m.Data.missionId == data.missionId);
        if (state != null) SyncProgress(state);

        RefreshPanel();
    }

    private void OnMissionCompleted(MissionData data)
    {
        // Liegt noch Beute bereit, bleibt der Eintrag als Abhol-Karte stehen —
        // OnRewardsPending hat ihn kurz vorher schon umgeschaltet.
        if (awaitingCollect.Contains(data.missionId))
        {
            RefreshPanel();
            return;
        }

        RemoveEntry(data.missionId);
    }

    /// <summary>
    /// Mission ist durch, die Belohnung wartet. Feuert VOR OnMissionCompleted.
    /// Beim Laden eines Spielstands existiert der Eintrag noch nicht — dann neu anlegen.
    /// </summary>
    private void OnRewardsPending(MissionData data)
    {
        if (!entries.TryGetValue(data.missionId, out var entry))
        {
            CreateEntry(data);
            entries.TryGetValue(data.missionId, out entry);
        }

        if (entry == null) return;

        awaitingCollect.Add(data.missionId);

        string id = data.missionId;
        entry.ShowCompleted(data, () => MissionManager.Instance?.TryCollectRewards(id));

        RefreshPanel();
    }

    private void OnRewardsCollected(MissionData data)
    {
        awaitingCollect.Remove(data.missionId);
        RemoveEntry(data.missionId);
    }

    private void RemoveEntry(string missionId)
    {
        if (entries.TryGetValue(missionId, out var entry) && entry != null)
            Destroy(entry.gameObject);

        entries.Remove(missionId);
        awaitingCollect.Remove(missionId);
        RefreshPanel();
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

    /// <summary>
    /// Blendet das Panel ein/aus und pflegt den "nächster Schritt"-Hinweis.
    ///
    /// Das Panel darf nicht mehr allein an entries.Count hängen: hält die Kette an einem
    /// Akt-Auftakt (startedByDialogue), läuft keine Mission — der Spieler soll aber trotzdem
    /// lesen können, wohin er als Nächstes muss.
    /// </summary>
    private void RefreshPanel()
    {
        if (panel == null)
            panel = gameObject;

        string hint = BuildNextStepHint();

        var label = EnsureNextStepLabel();
        if (label != null)
        {
            label.text = hint;
            label.gameObject.SetActive(!string.IsNullOrEmpty(hint));
        }

        RefreshBannerTitle(hint);

        panel.SetActive(entries.Count > 0 || !string.IsNullOrEmpty(hint));
    }

    /// <summary>
    /// Schreibt den Titel der obersten Mission ins Kopfbanner und blendet dafür den
    /// Inline-Titel dieses Eintrags aus, damit er nicht doppelt dasteht.
    /// </summary>
    private void RefreshBannerTitle(string hint)
    {
        var banner = EnsureBannerTitle();
        if (banner == null) return;

        MissionEntryUI first = FirstEntryInOrder();

        string title = first != null ? first.MissionTitle : null;
        if (string.IsNullOrWhiteSpace(title))
            title = string.IsNullOrEmpty(hint) ? null : "Nächster Schritt";

        banner.text = title;
        banner.gameObject.SetActive(!string.IsNullOrEmpty(title));

        // Nur der oberste Eintrag verliert seinen Titel — bei mehreren Missionen behalten
        // die übrigen ihren, sonst wären sie nicht mehr auseinanderzuhalten.
        if (contentRoot == null) return;

        bool isFirst = true;
        foreach (Transform child in contentRoot)
        {
            var entry = child.GetComponent<MissionEntryUI>();
            if (entry == null) continue;

            entry.SetInlineTitleVisible(!(isFirst && hideInlineTitleOfFirstEntry));
            isFirst = false;
        }
    }

    private MissionEntryUI FirstEntryInOrder()
    {
        if (contentRoot == null) return null;

        foreach (Transform child in contentRoot)
        {
            var entry = child.GetComponent<MissionEntryUI>();
            if (entry != null) return entry;
        }

        return null;
    }

    /// <summary>
    /// Legt den Banner-Titel an. Hängt am Panel (nicht am contentRoot) mit
    /// <c>ignoreLayout</c> — die VerticalLayoutGroup des Panels würde ihn sonst unter
    /// das Holzschild in den Textbereich schieben.
    /// </summary>
    private TMP_Text EnsureBannerTitle()
    {
        if (bannerTitleText != null)
        {
            ApplyBannerTitleLayout((RectTransform)bannerTitleText.transform);
            return bannerTitleText;
        }

        if (panel == null) return null;

        Transform existing = panel.transform.Find(BannerTitleName);
        if (existing != null)
        {
            bannerTitleText = existing.GetComponent<TMP_Text>();
            if (bannerTitleText != null)
            {
                ApplyBannerTitleLayout((RectTransform)existing);
                // Auch die wiedergefundene Referenz muss gespeichert werden, sonst sucht
                // der Code sie bei jedem Start neu und das Feld bleibt im Inspector leer.
                MarkEditorDirty(null);
                return bannerTitleText;
            }
        }

        var go = new GameObject(BannerTitleName, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(panel.transform, false);

        var ignore = GetOrAdd<LayoutElement>(go);
        ignore.ignoreLayout = true;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = bannerTitleFontSize;
        text.color = bannerTitleColor;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = bannerTitleFontSize;

        bannerTitleText = text;
        ApplyBannerTitleLayout(rect);

        MarkEditorDirty(go);
        return bannerTitleText;
    }

    /// <summary>
    /// Im Edit-Modus angelegte Objekte Unity bekanntmachen.
    ///
    /// Ohne das ist das GameObject zwar in der Hierarchy zu sehen, die Szene gilt aber als
    /// unverändert — Ctrl+S speichert es nicht, und die Zuweisung an bannerTitleText geht
    /// beim nächsten Domain-Reload verloren. Man baut den Titel also, positioniert ihn,
    /// und beim nächsten Öffnen ist er wieder weg.
    /// </summary>
    private void MarkEditorDirty(GameObject created)
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;

        if (created != null)
            UnityEditor.Undo.RegisterCreatedObjectUndo(created, "Banner-Titel anlegen");

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    /// <summary>
    /// Legt den Banner-Titel im Edit-Modus an und wählt ihn direkt aus, damit man ihn
    /// sofort im Scene-View zurechtschieben kann.
    /// </summary>
    [ContextMenu("Banner-Titel anlegen und auswählen")]
    private void CreateAndSelectBannerTitle()
    {
        if (panel == null) panel = gameObject;

        var text = EnsureBannerTitle();
        if (text == null)
        {
            Debug.LogWarning("[MissionsUI] Banner-Titel konnte nicht angelegt werden — kein Panel.", this);
            return;
        }

        // Ohne Text ist das Objekt im Scene-View unsichtbar und praktisch nicht greifbar.
        if (string.IsNullOrEmpty(text.text))
            text.text = "Missionstitel";

        text.gameObject.SetActive(true);

#if UNITY_EDITOR
        UnityEditor.Selection.activeGameObject = text.gameObject;
        Debug.Log("[MissionsUI] 'BannerTitle' liegt jetzt unter dem Panel und ist ausgewählt.\n" +
                  "Nächster Schritt: Haken 'Auto Position Banner Title' RAUS, dann frei ziehen und Szene speichern.", text);
#endif
    }

    private void ApplyBannerTitleLayout(RectTransform rect)
    {
        if (rect == null) return;

        // Von Hand positioniert: nicht anfassen. Sonst würde jedes RefreshPanel() die
        // im Editor gezogene Position wieder überschreiben — der Titel spränge im
        // Playmode zurück und man sucht den Fehler in der Grafik statt im Code.
        if (!autoPositionBannerTitle) return;

        // Oben im Panel aufhängen und in der Breite mitwachsen lassen; die Seiten-Insets
        // halten den Text innerhalb des Holzschilds statt über die Blätter zu laufen.
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(bannerTitleSideInset, -(bannerTitleTop + bannerTitleHeight));
        rect.offsetMax = new Vector2(-bannerTitleSideInset, -bannerTitleTop);
    }

    /// <summary>Text für den Hinweis, oder null wenn gerade keiner nötig ist.</summary>
    private string BuildNextStepHint()
    {
        // Eine laufende Mission sagt schon alles — der Hinweis wäre nur Rauschen.
        if (entries.Count > 0) return null;
        if (MissionManager.Instance == null) return null;

        var next = MissionManager.Instance.NextStoryMission;
        if (next == null) return null;

        // Fehlen die Voraussetzungen noch, wäre der Hinweis irreführend.
        if (!MissionManager.Instance.ArePrerequisitesMet(next)) return null;

        return string.IsNullOrWhiteSpace(next.nextStepHint) ? next.title : next.nextStepHint;
    }

    private TMP_Text EnsureNextStepLabel()
    {
        if (nextStepText != null) return nextStepText;
        if (contentRoot == null) return null;

        Transform existing = contentRoot.Find(NextStepName);
        if (existing != null)
        {
            nextStepText = existing.GetComponent<TMP_Text>();
            if (nextStepText != null) return nextStepText;
        }

        var go = new GameObject(NextStepName, typeof(RectTransform));
        go.transform.SetParent(contentRoot, false);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = nextStepFontSize;
        text.color = nextStepColor;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        nextStepText = text;
        return nextStepText;
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

        // Auch im Edit-Modus anlegen: nur so wird 'BannerTitle' ein echtes Kind des Panels,
        // das man im Scene-View auswählen und verschieben kann. Zur Laufzeit erzeugte
        // Objekte gibt es im Editor nicht — man hätte nichts zum Anfassen.
        EnsureBannerTitle();

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
            var bannerRect = bannerTitleText != null ? (RectTransform)bannerTitleText.transform : null;
            Debug.Log($"[MissionsUI] BannerTitle: {(bannerRect != null ? bannerRect.anchoredPosition + " / " + bannerRect.rect.size : "fehlt")}, " +
                      $"AutoPosition={autoPositionBannerTitle}" +
                      (autoPositionBannerTitle ? " (Haken raus zum Selberziehen)" : " (von Hand positioniert)"),
                      bannerTitleText);

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

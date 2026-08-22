using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Schaltet die Quest-Outline automatisch an den Objekten ein, die für die gerade
/// offenen Missions-Ziele gebraucht werden — damit man den richtigen NPC im
/// Marktplatz-Gewusel findet, ohne dass jemand das pro Mission von Hand
/// verdrahten muss.
///
/// Gehört auf dasselbe Objekt wie der <see cref="MissionManager"/> (SaveSystem,
/// DontDestroyOnLoad).
///
/// Ablauf: Der Director fragt beim MissionManager die aktiven Missionen ab,
/// bestimmt daraus die gerade *bearbeitbaren* Ziele und vergleicht sie mit allen
/// registrierten <see cref="HighlightTarget"/>s.
///
/// Wichtig ist die Regel für sequenzielle Missionen: dort kann laut
/// MissionManager.ReportProgress nur das erste unerledigte Objective Fortschritt
/// machen. Genau diese Logik wird hier gespiegelt — sonst würde Schritt 5 einer
/// Kette schon leuchten, während der Spieler noch bei Schritt 2 steht, und das
/// Highlight würde in die Irre führen statt zu helfen.
/// </summary>
[DefaultExecutionOrder(100)]
public class MissionHighlightDirector : MonoBehaviour
{
    public static MissionHighlightDirector Instance { get; private set; }

    [Tooltip("Aus = alle Highlights bleiben dunkel. Zum schnellen Abschalten im Playtest.")]
    [SerializeField] private bool enableHighlighting = true;

    private readonly HashSet<HighlightTarget> shouldGlow = new();
    private readonly List<MissionObjectiveData> activeObjectives = new();
    private bool subscribedToManager;

    /// <summary>
    /// Die gerade hervorgehobenen Ziele. Der Bildschirmrand-Pfeil
    /// (<see cref="OffScreenHighlightIndicator"/>) liest hier mit, statt die
    /// Missions-Auswertung ein zweites Mal nachzubauen — zwei Quellen für dieselbe
    /// Frage würden früher oder später auseinanderlaufen.
    /// </summary>
    public IReadOnlyCollection<HighlightTarget> Highlighted => shouldGlow;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        HighlightTarget.OnRegistryChanged += Refresh;
        TrySubscribeToManager();
        Refresh();
    }

    private void OnDisable()
    {
        HighlightTarget.OnRegistryChanged -= Refresh;
        UnsubscribeFromManager();
    }

    private void Update()
    {
        // Der MissionManager setzt seine Instance in Awake(). Die Awake-Reihenfolge
        // zweier Components ist in Unity nicht garantiert, also kann OnEnable hier
        // zu früh kommen. Statt auf Glück zu bauen, wird bis zum Erfolg nachgehakt.
        if (!subscribedToManager) TrySubscribeToManager();
    }

    private void TrySubscribeToManager()
    {
        var manager = MissionManager.Instance;
        if (manager == null || subscribedToManager) return;

        manager.OnMissionStarted    += HandleMissionChanged;
        manager.OnMissionCompleted  += HandleMissionChanged;
        manager.OnObjectiveUpdated  += HandleObjectiveUpdated;
        subscribedToManager = true;

        Refresh();
    }

    private void UnsubscribeFromManager()
    {
        var manager = MissionManager.Instance;
        if (manager == null || !subscribedToManager) return;

        manager.OnMissionStarted    -= HandleMissionChanged;
        manager.OnMissionCompleted  -= HandleMissionChanged;
        manager.OnObjectiveUpdated  -= HandleObjectiveUpdated;
        subscribedToManager = false;
    }

    private void HandleMissionChanged(MissionData _) => Refresh();
    private void HandleObjectiveUpdated(MissionData _, int __, int ___, int ____) => Refresh();

    /// <summary>
    /// Bewertet alles neu. Bewusst ein kompletter Durchlauf statt inkrementeller
    /// Buchführung: die Menge ist winzig (paar Missionen × paar Ziele × paar Objekte)
    /// und ein voller Neuaufbau kann nicht wie ein Diff aus dem Tritt geraten.
    /// </summary>
    public void Refresh()
    {
        shouldGlow.Clear();

        if (enableHighlighting)
        {
            CollectActiveObjectives();

            var targets = HighlightTarget.All;
            for (int t = 0; t < targets.Count; t++)
            {
                var target = targets[t];
                for (int o = 0; o < activeObjectives.Count; o++)
                {
                    if (target.Matches(activeObjectives[o]))
                    {
                        shouldGlow.Add(target);
                        break;
                    }
                }
            }
        }

        var all = HighlightTarget.All;
        for (int i = 0; i < all.Count; i++)
            all[i].SetHighlighted(shouldGlow.Contains(all[i]));
    }

    /// <summary>
    /// Sammelt die Ziele, an denen der Spieler JETZT arbeiten kann.
    /// Spiegelt die Auswahl-Logik aus MissionManager.ReportProgress.
    /// </summary>
    private void CollectActiveObjectives()
    {
        activeObjectives.Clear();

        var manager = MissionManager.Instance;
        if (manager == null) return;

        var missions = manager.ActiveMissions;
        for (int m = 0; m < missions.Count; m++)
        {
            var state = missions[m];
            var objectives = state.Data.objectives;
            if (objectives == null || objectives.Length == 0) continue;

            if (state.Data.sequentialObjectives)
            {
                // Nur das erste offene Ziel — alles danach ist noch gar nicht dran.
                for (int i = 0; i < objectives.Length; i++)
                {
                    if (!state.ObjectiveCompleted(i))
                    {
                        activeObjectives.Add(objectives[i]);
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < objectives.Length; i++)
                    if (!state.ObjectiveCompleted(i))
                        activeObjectives.Add(objectives[i]);
            }
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Vertont die Oberfläche. Zwei Wege führen hier rein:
///
/// <b>1. Automatisch.</b> Jeder <see cref="Button"/> in der Szene bekommt beim Laden einen
/// Klick-Sound angehängt. Ohne das müsste man in fünfzehn UI-Skripten je eine Zeile
/// nachrüsten und bei jedem neuen Button daran denken — das hält keine Woche.
///
/// <b>2. Über die statischen Kurzaufrufe</b> (<see cref="Purchase"/>, <see cref="Denied"/> …)
/// für alles, was kein Buttonklick ist. Die dürfen aus jedem Skript kommen und sind
/// gefahrlos, wenn es gar keinen UiSfx in der Szene gibt.
///
/// Ein paar Spiel-Events werden direkt abgegriffen (Kauf, Verkauf, Missionen), damit die
/// bestehenden Systeme unangetastet bleiben.
///
/// Setup: auf dasselbe GameObject wie den SfxManager legen und die Library zuweisen.
/// </summary>
[DefaultExecutionOrder(-50)]
public class UiSfx : MonoBehaviour
{
    public static UiSfx Instance { get; private set; }

    [SerializeField] private UiSfxLibrary library;

    [Tooltip("Aus = Buttons müssen ihre Sounds selbst auslösen. Nur abschalten, wenn die " +
             "automatische Verkabelung mit einem eigenen UI-System kollidiert.")]
    [SerializeField] private bool autoHookButtons = true;

    [Tooltip("Sekunden zwischen zwei Suchläufen nach neuen Buttons. Shop-Zeilen, Missions-" +
             "einträge und Speicherstände entstehen erst zur Laufzeit und sind beim ersten " +
             "Durchlauf noch nicht da.\n\n0 = nur beim Szenenwechsel suchen.")]
    [SerializeField] private float rescanInterval = 1f;

    // Bereits verkabelte Buttons. Ohne die Liste würde jeder Suchlauf einen weiteren
    // Listener anhängen und ein Klick nach einer Minute fünffach klingen.
    private readonly HashSet<Button> hooked = new();

    private float nextRescan;

    // Frame, in dem zuletzt eine Mission abgeschlossen wurde — unterdrückt den Ziel-Klang.
    private int missionCompletedFrame = -1;
    private Coroutine objectiveRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeGlobal();
        Instance = null;
    }

    private void Start()
    {
        SubscribeGlobal();
        SubscribeSceneSystems();
        Rescan();
    }

    private void Update()
    {
        if (!autoHookButtons || rescanInterval <= 0f) return;
        if (Time.unscaledTime < nextRescan) return;

        nextRescan = Time.unscaledTime + rescanInterval;
        Rescan();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Die alten Buttons sind mit der Szene weg. Die Menge mitzuschleppen würde nur
        // Leichen sammeln — und Unity-Objekte melden sich nach dem Zerstören als "null",
        // vergleichen sich in einer HashSet aber trotzdem noch.
        hooked.Clear();
        Rescan();

        // Der MissionManager ist szenengebunden: der aus der letzten Szene ist samt seiner
        // Abonnenten weg, der neue kennt uns noch nicht. Ohne das hier wären Missions-Klänge
        // nach dem ersten Szenenwechsel für den Rest der Sitzung still.
        SubscribeSceneSystems();
    }

    // ── Automatische Verkabelung ──────────────────────────────────────────────

    /// <summary>Sucht neue Buttons und hängt den Klick-Sound an. Kann jederzeit gerufen werden.</summary>
    public void Rescan()
    {
        if (!autoHookButtons) return;

        hooked.RemoveWhere(b => b == null);

        var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var button in buttons)
        {
            if (button == null || !hooked.Add(button)) continue;

            var target = button;
            var kind = target.GetComponent<UiSfxOverride>();

            button.onClick.AddListener(() => PlayForButton(target, kind));
        }
    }

    private void PlayForButton(Button button, UiSfxOverride kind)
    {
        if (button == null || !button.interactable) return;

        if (kind == null) { Play(library?.buttonClick); return; }

        switch (kind.kind)
        {
            case UiSfxKind.Silent:   return;
            case UiSfxKind.Back:     Play(Fallback(library?.buttonBack, library?.buttonClick)); return;
            case UiSfxKind.Open:     Play(library?.panelOpen);    return;
            case UiSfxKind.Close:    Play(library?.panelClose);   return;
            case UiSfxKind.Purchase: Play(library?.purchase);     return;
            case UiSfxKind.Denied:   Play(library?.denied);       return;
            default:                 Play(library?.buttonClick);  return;
        }
    }

    // ── Spiel-Events ──────────────────────────────────────────────────────────

    // Statische Events überleben den Szenenwechsel — genau einmal an- und abmelden.
    private void SubscribeGlobal()
    {
        PlayerInventory.OnSeedBoughtStatic += HandleSeedBought;

        // OnCropSoldStatic wird bewusst nicht abonniert: der Verkaufsklang hängt am
        // Münzflug und wird von dort ausgelöst (CropSfx.PlaySell), sobald die Münzen
        // ankommen. Über das Event käme er im Moment des Klicks — also zu früh.

        ToolRegistry.OnToolUpgradedStatic += HandleToolUpgraded;
        ToolRegistry.OnToolAcquiredStatic += HandleToolAcquired;

        PlantManager.OnStarterSeedFound += HandleStarterSeedFound;
    }

    private void UnsubscribeGlobal()
    {
        PlayerInventory.OnSeedBoughtStatic -= HandleSeedBought;

        ToolRegistry.OnToolUpgradedStatic -= HandleToolUpgraded;
        ToolRegistry.OnToolAcquiredStatic -= HandleToolAcquired;

        PlantManager.OnStarterSeedFound -= HandleStarterSeedFound;
    }

    private void HandleToolUpgraded(ToolType tool) => Play(library?.toolUpgraded);

    private void HandleToolAcquired(ToolType tool)
        => Play(Fallback(library?.toolAcquired, library?.purchase));

    private void HandleStarterSeedFound(PlantType type, Vector3 worldPos)
        => Play(Fallback(library?.starterSeedFound, library?.rewardCollected));

    // Instanz-Events eines szenengebundenen Managers. Erst abmelden, dann anmelden:
    // ist es derselbe Manager wie vorher (Szene neu geladen, Objekt überlebt), hätten
    // wir sonst zwei Abonnements und jeder Missions-Klang käme doppelt.
    private void SubscribeSceneSystems()
    {
        var missions = MissionManager.Instance;
        if (missions == null) return;

        missions.OnMissionStarted   -= HandleMissionStarted;
        missions.OnMissionCompleted -= HandleMissionCompleted;
        missions.OnObjectiveUpdated -= HandleObjectiveUpdated;

        missions.OnMissionStarted   += HandleMissionStarted;
        missions.OnMissionCompleted += HandleMissionCompleted;
        missions.OnObjectiveUpdated += HandleObjectiveUpdated;

        // OnRewardsCollected wird bewusst nicht abonniert: den Klang beim Abholen liefert
        // inzwischen der Münzflug selbst, und zwar zum Zeitpunkt des Eintreffens. Über das
        // Event käme er zusätzlich und einen Wimpernschlag daneben.
    }

    private void HandleSeedBought(PlantType type, int amount) => Play(library?.purchase);

    private void HandleMissionStarted(MissionData mission) => Play(library?.missionStarted);

    private void HandleMissionCompleted(MissionData mission)
    {
        missionCompletedFrame = Time.frameCount;
        Play(library?.missionCompleted);
    }

    /// <summary>
    /// Klingt nur beim <b>Erreichen</b> eines Ziels, nicht bei jedem Zwischenschritt.
    ///
    /// Das Event feuert bei jeder Änderung — bei "10 Karotten ernten" also zehnmal. Neun
    /// davon sind kein Ereignis, sondern nur eine Zahl, die sich bewegt; das hat die
    /// Anzeige schon abgedeckt. Erst der letzte Schritt ist eine Meldung wert.
    /// </summary>
    private void HandleObjectiveUpdated(MissionData mission, int index, int progress, int required)
    {
        if (required <= 0 || progress < required) return;

        // Mehrere Ziele können im selben Frame fertig werden (eine AoE-Ernte erfüllt
        // Ernte- und Verdienstziel gleichzeitig) — das bleibt eine Meldung.
        if (objectiveRoutine == null)
            objectiveRoutine = StartCoroutine(PlayObjectiveUnlessMissionEnds());
    }

    /// <summary>
    /// Wartet einen Frame, bevor der Ziel-Klang kommt.
    ///
    /// War es das letzte Ziel, folgt <c>OnMissionCompleted</c> noch im selben Frame — und
    /// zwar erst danach, weshalb sich beim Eintreffen des Ziel-Events noch nicht sagen
    /// lässt, ob die Mission damit endet. Einen Frame zu warten beantwortet die Frage von
    /// selbst. Zwei Klänge übereinander wären hier besonders unangenehm, weil der kleinere
    /// den großen Moment zerkratzt.
    /// </summary>
    private IEnumerator PlayObjectiveUnlessMissionEnds()
    {
        int frame = Time.frameCount;
        yield return null;

        objectiveRoutine = null;

        if (missionCompletedFrame == frame) yield break;

        Play(library?.objectiveCompleted);
    }

    // ── Statische Kurzaufrufe ─────────────────────────────────────────────────

    public static void ButtonClick()     => Instance?.Play(Lib?.buttonClick);
    public static void PanelOpen()       => Instance?.Play(Lib?.panelOpen);
    public static void PanelClose()      => Instance?.Play(Lib?.panelClose);
    public static void Purchase()        => Instance?.Play(Lib?.purchase);
    public static void Sell()            => Instance?.Play(Lib?.sell);
    public static void Denied()          => Instance?.Play(Lib?.denied);
    public static void MissionComplete() => Instance?.Play(Lib?.missionCompleted);
    public static void RewardCollected() => Instance?.Play(Lib?.rewardCollected);
    public static void FertilizerCollected() => Instance?.Play(Fallback(Lib?.fertilizerCollected, Lib?.rewardCollected));

    /// <summary>Komposter fertig gebraut — einmalige Benachrichtigung, unabhängig vom
    /// späteren Abholen (FertilizerCollected).</summary>
    public static void CompostReady() => Instance?.Play(Fallback(Lib?.compostReady, Lib?.rewardCollected));

    /// <summary>
    /// Klang für ankommende Münzen. Wird vom Münzflug selbst ausgelöst, damit er überall
    /// gleich klingt — beim Abholen einer Belohnung wie beim Verkauf.
    ///
    /// Fällt auf <c>rewardCollected</c> zurück, damit bereits zugewiesene Clips weiter
    /// greifen, ohne dass man sie doppelt einträgt.
    /// </summary>
    public static void CoinFlight()
        => Instance?.Play(Fallback(Lib?.coinFlight, Lib?.rewardCollected));

    /// <summary>
    /// Leise Variante für jede einzelne nachfolgende Münze.
    ///
    /// Umgeht den Frame-Duplikatschutz nicht — mehrere Münzen, die im selben Frame
    /// ankommen, bleiben also eine Wiedergabe. Genau das ist hier richtig: gleichzeitige
    /// identische Clips ergeben kein Prasseln, sondern Phasing.
    /// </summary>
    public static void CoinFlightTick(float volume)
    {
        var clips = Fallback(Lib?.coinFlight, Lib?.rewardCollected);
        if (clips == null || clips.Length == 0 || Instance == null) return;
        if (SfxManager.Instance == null || Instance.library == null) return;

        var clip = clips.Length == 1 ? clips[0] : clips[Random.Range(0, clips.Length)];

        // Mehr Tonhöhenstreuung als sonst: bei einer schnellen Folge desselben Clips ist
        // sie der einzige Grund, warum es nach vielen Münzen klingt und nicht nach einer
        // hängenden Datei.
        SfxManager.Instance.PlayUI(clip, Instance.library.volume * volume, 0.12f);
    }

    private static UiSfxLibrary Lib => Instance != null ? Instance.library : null;

    // ── Wiedergabe ────────────────────────────────────────────────────────────

    private void Play(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;
        if (library == null || SfxManager.Instance == null) return;

        var clip = clips.Length == 1 ? clips[0] : clips[Random.Range(0, clips.Length)];
        SfxManager.Instance.PlayUI(clip, library.volume, library.pitchJitter);
    }

    private static AudioClip[] Fallback(AudioClip[] first, AudioClip[] second)
        => first != null && first.Length > 0 ? first : second;
}

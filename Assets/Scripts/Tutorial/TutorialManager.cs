using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orchestriert den Tutorial-Ablauf: Dialogue, Blocking, Schrittfortschritt.
/// Sitzt auf dem SaveSystem-Objekt (DontDestroyOnLoad).
///
/// Setup:
///   - tutorialMission: MissionData Asset mit sequentialObjectives=true, in MissionManager.sideMissions eingetragen
///   - sequence: TutorialSequence Asset, steps[i] entspricht tutorialMission.objectives[i]
///   - TutorialManager muss NACH MissionManager und DialogueManager in der Awake-Reihenfolge stehen
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("Assets")]
    [SerializeField] private MissionData tutorialMission;
    [SerializeField] private TutorialSequence sequence;

    private int currentStepIndex = 0;
    private bool tutorialActive = false;
    private readonly HashSet<TutorialBlockedAction> currentlyBlocked = new();
    private DialogueData pendingDialogue = null;
    private bool plantGrowDialogueFired = false;
    private bool plantFullyGrownDialogueFired = false;
    private bool waitingForPlantGrown = false;

    public bool IsActive => tutorialActive;
    public MissionData TutorialMission => tutorialMission;

    // --- Blocking-API (von anderen Systemen abgefragt) ---

    /// <summary>
    /// Gibt true zurück wenn die Aktion gerade durch das Tutorial blockiert ist.
    /// Während ein Dialog läuft ist automatisch alles blockiert.
    /// </summary>
    public bool IsBlocked(TutorialBlockedAction action)
    {
        if (!tutorialActive) return false;
        return currentlyBlocked.Contains(action);
    }

    // --- Lifecycle ---

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        // Einen Frame warten damit alle Awake/OnEnable der anderen Manager durch sind
        yield return null;
        SubscribeToManagers();
    }

    private void OnDestroy()
    {
        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.OnMissionStarted -= OnMissionStarted;
            MissionManager.Instance.OnObjectiveUpdated -= OnObjectiveUpdated;
            MissionManager.Instance.OnMissionCompleted -= OnMissionCompleted;
        }

        if (DialogueManager.Instance != null)
            DialogueManager.Instance.OnDialogueEnded -= OnDialogueEnded;

        PlantManager.OnPlantGrew -= OnPlantGrew;
        PlantManager.OnPlantFullyGrown -= OnPlantFullyGrown;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void SubscribeToManagers()
    {
        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.OnMissionStarted += OnMissionStarted;
            MissionManager.Instance.OnObjectiveUpdated += OnObjectiveUpdated;
            MissionManager.Instance.OnMissionCompleted += OnMissionCompleted;

            // Save wurde möglicherweise schon vor dieser Subscription geladen →
            // OnMissionStarted wäre verpasst worden. Nachträglich prüfen:
            if (!tutorialActive && tutorialMission != null)
            {
                var already = MissionManager.Instance.ActiveMissions
                    .FirstOrDefault(m => m.Data?.missionId == tutorialMission.missionId);
                if (already != null)
                    OnMissionStarted(already.Data);
            }
        }
        else
        {
            Debug.LogWarning("[TutorialManager] MissionManager.Instance ist null beim Subscribe.");
        }

        if (DialogueManager.Instance != null)
            DialogueManager.Instance.OnDialogueEnded += OnDialogueEnded;

        PlantManager.OnPlantGrew += OnPlantGrew;
        PlantManager.OnPlantFullyGrown += OnPlantFullyGrown;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // --- Public API ---

    /// <summary>Zustand vollständig zurücksetzen — für Debug-Reset oder neues Spiel.</summary>
    public void ForceReset()
    {
        tutorialActive = false;
        currentStepIndex = 0;
        currentlyBlocked.Clear();
        pendingDialogue = null;
    }

    /// <summary>Intro-Dialog des aktuellen Schritts wiederholen (z.B. nach versehentlichem Skip).</summary>
    public void RepeatCurrentStepDialogue()
    {
        if (!tutorialActive || sequence == null) return;
        if (currentStepIndex >= sequence.steps.Length) return;

        var step = sequence.steps[currentStepIndex];
        if (step.introDialogue != null)
            DialogueManager.Instance?.StartDialogue(step.introDialogue);
    }

    /// <summary>Tutorial starten — wird von TutorialNpc beim neuen Spiel aufgerufen.</summary>
    public void BeginTutorial()
    {
        Debug.Log($"[TutorialManager] BeginTutorial() aufgerufen. tutorialMission={tutorialMission}, sequence={sequence}, tutorialActive={tutorialActive}");

        if (tutorialMission == null || sequence == null)
        {
            Debug.LogError("[TutorialManager] tutorialMission oder sequence ist nicht zugewiesen!");
            return;
        }

        bool missionStillActive = MissionManager.Instance?.ActiveMissions
            .Any(m => m.Data?.missionId == tutorialMission.missionId) == true;
        if (tutorialActive && missionStillActive)
        {
            Debug.Log("[TutorialManager] Tutorial läuft bereits → abgebrochen.");
            return;
        }

        tutorialActive = true;
        currentStepIndex = 0;
        currentlyBlocked.Clear();
        pendingDialogue = null;

        MissionManager.Instance?.StartMission(tutorialMission);
        ShowStepIntro(0);
    }

    // --- Event Handler ---

    private void OnMissionStarted(MissionData data)
    {
        // Tutorial aus Save laden: Mission war schon aktiv → Tutorial fortsetzen
        if (tutorialMission == null || data.missionId != tutorialMission.missionId) return;
        if (tutorialActive) return; // Läuft schon (BeginTutorial wurde vorher gerufen)

        tutorialActive = true;

        // Ersten unvollständigen Schritt finden
        var state = MissionManager.Instance?.ActiveMissions
            .FirstOrDefault(m => m.Data.missionId == tutorialMission.missionId);

        if (state == null) return;

        for (int i = 0; i < state.Data.objectives.Length; i++)
        {
            if (!state.ObjectiveCompleted(i))
            {
                currentStepIndex = i;
                ShowStepIntro(i); // Dialog des aktuellen Schritts zeigen
                Debug.Log($"[TutorialManager] Tutorial fortgesetzt bei Schritt {i}");
                return;
            }
        }
    }

    private void OnObjectiveUpdated(MissionData data, int objIdx, int current, int required)
    {
        if (!tutorialActive) return;
        if (tutorialMission == null || data.missionId != tutorialMission.missionId) return;
        if (objIdx != currentStepIndex) return;

        if (current < required)
        {
            // Objective noch nicht fertig → Mid-Dialogue prüfen
            TryFireMidDialogue(objIdx, current);
            return;
        }

        // Objective abgeschlossen:
        // Spieler hat Aktion ausgeführt während Dialog noch offen → Dialog schließen
        DialogueManager.Instance?.ForceEnd();

        // Rewards des abgeschlossenen Schritts auszahlen
        GrantStepRewards(objIdx);

        // Schritt abgeschlossen → nächsten starten
        currentStepIndex++;

        if (currentStepIndex < sequence.steps.Length)
            ShowStepIntro(currentStepIndex);
        // Wenn alle durch → OnMissionCompleted folgt automatisch
    }

    private void OnPlantGrew(PlantType _)
    {
        if (!tutorialActive || plantGrowDialogueFired) return;
        if (sequence == null || currentStepIndex >= sequence.steps.Length) return;

        var dlg = sequence.steps[currentStepIndex].onPlantGrowDialogue;
        if (dlg == null) return;

        plantGrowDialogueFired = true;
        DialogueManager.Instance?.ForceEnd();
        pendingDialogue = dlg;
        StartCoroutine(StartPendingDialogueNextFrame());
    }

    private void OnPlantFullyGrown(PlantType _)
    {
        if (!tutorialActive) return;
        if (sequence == null || currentStepIndex >= sequence.steps.Length) return;

        var step = sequence.steps[currentStepIndex];

        // Fall 1: Dieser Schritt wartet auf Pflanzenwachstum → Intro jetzt spielen
        if (waitingForPlantGrown && step.introDialogue != null)
        {
            waitingForPlantGrown = false;
            plantFullyGrownDialogueFired = true;
            DialogueManager.Instance?.ForceEnd();
            pendingDialogue = step.introDialogue;
            StartCoroutine(StartPendingDialogueNextFrame());
            return;
        }

        // Fall 2: Normaler onPlantFullyGrownDialogue (mid-step, nur einmal)
        if (!plantFullyGrownDialogueFired && step.onPlantFullyGrownDialogue != null)
        {
            plantFullyGrownDialogueFired = true;
            DialogueManager.Instance?.ForceEnd();
            pendingDialogue = step.onPlantFullyGrownDialogue;
            StartCoroutine(StartPendingDialogueNextFrame());
        }
    }

    private void TryFireMidDialogue(int stepIndex, int currentProgress)
    {
        if (sequence == null || stepIndex >= sequence.steps.Length) return;

        var mids = sequence.steps[stepIndex].midDialogues;
        if (mids == null) return;

        foreach (var mid in mids)
        {
            if (mid.dialogue != null && mid.atProgress == currentProgress)
            {
                // Laufenden Dialog erst schließen, dann neuen starten
                DialogueManager.Instance?.ForceEnd();
                pendingDialogue = mid.dialogue;
                StartCoroutine(StartPendingDialogueNextFrame());
                return;
            }
        }
    }

    private void OnMissionCompleted(MissionData data)
    {
        if (tutorialMission == null || data.missionId != tutorialMission.missionId) return;

        tutorialActive = false;
        currentlyBlocked.Clear();

        if (sequence?.completionDialogue != null)
            DialogueManager.Instance?.StartDialogue(sequence.completionDialogue);

        Debug.Log("[TutorialManager] Tutorial abgeschlossen!");
    }

    private void OnDialogueEnded()
    {
        // Nach jedem Dialog wird der Schritt spielbar (Blocking bleibt aktiv, Dialog-Block fällt weg)
        // Nichts weiter nötig — IsBlocked() prüft DialogueManager.IsActive selbst
    }

    // --- Interne Hilfsmethoden ---

    private void ShowStepIntro(int stepIndex)
    {
        ApplyBlockingForStep(stepIndex);

        // SelectTool-Schritt: Hotbar zurücksetzen damit der Event garantiert neu feuert
        if (tutorialMission != null
            && stepIndex < tutorialMission.objectives.Length
            && tutorialMission.objectives[stepIndex].type == MissionObjectiveType.SelectTool)
        {
            Hotbar.Instance?.SetTool(ToolType.None);
        }

        if (sequence == null || stepIndex >= sequence.steps.Length)
        {
            Debug.LogWarning($"[TutorialManager] ShowStepIntro({stepIndex}): sequence null oder Index out of range (steps.Length={sequence?.steps?.Length})");
            return;
        }

        var step = sequence.steps[stepIndex];

        if (step.delayIntroUntilPlantGrown)
        {
            // Intro wird erst gespielt wenn OnPlantFullyGrown feuert
            waitingForPlantGrown = true;
            Debug.Log($"[TutorialManager] Schritt {stepIndex}: Intro wartet auf Pflanzenwachstum.");
            return;
        }

        if (step.introDialogue == null)
        {
            Debug.LogWarning($"[TutorialManager] ShowStepIntro({stepIndex}): introDialogue ist null — kein Dialog startet.");
            return;
        }

        Debug.Log($"[TutorialManager] Starte Dialog für Schritt {stepIndex}: {step.introDialogue.name}");

        // Dialog als Pending speichern — falls gerade ein Scenewechsel läuft,
        // startet OnSceneLoaded ihn nach der neuen Scene. Andernfalls greift
        // der Coroutine-Fallback nach einem Frame.
        pendingDialogue = step.introDialogue;
        StartCoroutine(StartPendingDialogueNextFrame());
    }

    private IEnumerator StartPendingDialogueNextFrame()
    {
        yield return null; // einen Frame warten — neue Scene + DialogueUI sind dann bereit
        if (pendingDialogue == null) yield break; // bereits von OnSceneLoaded gestartet
        var dlg = pendingDialogue;
        pendingDialogue = null;
        DialogueManager.Instance?.StartDialogue(dlg);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!tutorialActive || pendingDialogue == null) return;
        StartCoroutine(StartPendingAfterSceneFrame());
    }

    private IEnumerator StartPendingAfterSceneFrame()
    {
        yield return null; // DialogueUI.Start() abwarten
        if (pendingDialogue == null) yield break;
        var dlg = pendingDialogue;
        pendingDialogue = null;
        DialogueManager.Instance?.StartDialogue(dlg);
    }

    private void ApplyBlockingForStep(int stepIndex)
    {
        currentlyBlocked.Clear();
        plantGrowDialogueFired = false;
        plantFullyGrownDialogueFired = false;
        waitingForPlantGrown = false;

        if (sequence == null || stepIndex >= sequence.steps.Length) return;

        var step = sequence.steps[stepIndex];
        if (step.blockedDuring == null) return;

        foreach (var action in step.blockedDuring)
            currentlyBlocked.Add(action);
    }

    private void GrantStepRewards(int stepIndex)
    {
        if (sequence == null || stepIndex >= sequence.steps.Length) return;

        var rewards = sequence.steps[stepIndex].rewards;
        if (rewards == null) return;

        foreach (var reward in rewards)
        {
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
            }
        }
    }
}

using System;
using UnityEngine;

/// <summary>
/// Ein Schritt der Story-Kette, für den dieser NPC etwas zu sagen hat.
/// </summary>
[Serializable]
public class StoryStep
{
    [Tooltip("missionId des Story-Schritts, z.B. story_4_cauliflower.")]
    public string missionId;

    [Tooltip("Was der NPC bei diesem Schritt sagt.\n\n" +
             "Ob er die Mission auch VERGIBT, entscheidet allein das Feld " +
             "'missionToStartAfter' im DialogueData:\n" +
             "  gesetzt = er gibt die Mission (Quest-Geber)\n" +
             "  leer    = er weist nur den Weg zum zuständigen NPC (Wegweiser)")]
    public DialogueData dialogue;
}

/// <summary>
/// Quest-NPC der Story-Kette. Deckt beide Rollen ab:
///
///  • <b>Quest-Geber</b> — sein Dialog hat missionToStartAfter gesetzt und startet die Mission.
///    Beispiel: der Samenverkäufer vergibt "Blumenkohl-Saison", weil die Mission wörtlich
///    darin besteht, bei ihm zu kaufen.
///  • <b>Wegweiser</b> — sein Dialog hat missionToStartAfter leer. Er erzählt nur, wohin es
///    als Nächstes geht. Das ist Onkel Ozans Rolle für alles, was am Markt passiert.
///
/// Gegenstück zu MissionData.startedByDialogue: dort hält die Kette an, hier läuft sie weiter.
///
/// <b>Warum das auch den Tutorial-Wiederholdialog übernimmt:</b> WorldClickHandler löst per
/// GetComponentInParent&lt;IClickable&gt;() genau EINE Komponente auf. Lägen auf dem NPC
/// mehrere Klick-Scripts, entschiede die Komponenten-Reihenfolge im Inspector, welches
/// überhaupt reagiert — ein Fehler, den man erst im Playtest merkt. Deshalb sitzt hier alles
/// in einem klaren Vorrang.
///
/// Setup:
///  1. Auf ein 3D-Objekt mit Collider legen (der Raycast braucht ihn)
///  2. steps[] füllen: pro Story-Schritt missionId + Dialog
///  3. optional smallTalkDialogue für "hab grad nichts Neues"
///  4. optional availableIndicator (Ausrufezeichen), wird automatisch ein-/ausgeblendet
/// </summary>
public class StoryDialogueNpc : MonoBehaviour, IClickable
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int SymbolId = Shader.PropertyToID("_Symbol");
    private static readonly int ScaleId = Shader.PropertyToID("_Scale");
    private const float DialogueSymbol = 7f;

    [Header("Story-Schritte")]
    [Tooltip("Alle Schritte für die dieser NPC etwas zu sagen hat. Reihenfolge egal — " +
             "es zählt, welche Mission gerade in der Kette dran ist.")]
    [SerializeField] private StoryStep[] steps;

    [Tooltip("Wenn der NPC gerade nichts zur Story beizutragen hat.")]
    [SerializeField] private DialogueData smallTalkDialogue;

    [Header("Tutorial")]
    [Tooltip("Während das Tutorial läuft: wiederholt den Dialog des aktuellen Schritts, " +
             "falls der Spieler ihn weggeklickt hat. Ersetzt die Komponente " +
             "TutorialDialogueRepeat — nicht beide auf dasselbe Objekt legen.")]
    [SerializeField] private bool repeatTutorialDialogue = true;

    [Header("Anzeige")]
    [Tooltip("Optionales dauerhaftes Erkennungs-Icon im Stil der Markt-NPCs.")]
    [SerializeField] private GameObject identityIconPrefab;
    [SerializeField, Min(0f)] private float identityIconHeightOffset = 0.32f;
    [SerializeField, Min(0.05f)] private float identityIconWorldSize = 0.55f;
    [SerializeField] private Color identityIconColor = new(1f, 0.72f, 0.25f, 1f);

    [Tooltip("Optional: wird eingeblendet solange dieser NPC eine Mission zu VERGEBEN hat. " +
             "Bei reinen Wegweiser-Dialogen bewusst nicht — sonst leuchtet Onkel Ozan " +
             "dauerhaft, obwohl der Spieler zum Markt soll.")]
    [SerializeField] private GameObject availableIndicator;

    [Tooltip("Prüftakt in Sekunden. Kein Update() — der Zustand ändert sich nur bei " +
             "Missionsabschlüssen, ein grober Takt reicht.")]
    [SerializeField] private float indicatorRefreshInterval = 0.5f;

    private void Start() => CreateIdentityIcon();

    private void OnEnable()
    {
        if (availableIndicator != null)
            InvokeRepeating(nameof(RefreshIndicator), 0f, Mathf.Max(0.1f, indicatorRefreshInterval));
    }

    private void OnDisable() => CancelInvoke(nameof(RefreshIndicator));

    private void CreateIdentityIcon()
    {
        const string iconName = "Story NPC Identity Icon";
        if (identityIconPrefab == null || transform.Find(iconName) != null)
            return;

        float topY = transform.position.y + 2f;
        foreach (Renderer characterRenderer in GetComponentsInChildren<Renderer>(true))
        {
            if (characterRenderer != null && characterRenderer.enabled)
                topY = Mathf.Max(topY, characterRenderer.bounds.max.y);
        }

        GameObject icon = Instantiate(identityIconPrefab, transform);
        icon.name = iconName;
        icon.transform.position = new Vector3(
            transform.position.x,
            topY + identityIconHeightOffset,
            transform.position.z);

        Renderer iconRenderer = icon.GetComponentInChildren<Renderer>();
        if (iconRenderer == null)
        {
            Destroy(icon);
            return;
        }

        var properties = new MaterialPropertyBlock();
        iconRenderer.GetPropertyBlock(properties);
        properties.SetColor(BaseColorId, identityIconColor);
        properties.SetFloat(ProgressId, 1f);
        properties.SetFloat(SymbolId, DialogueSymbol);
        properties.SetFloat(ScaleId, identityIconWorldSize);
        iconRenderer.SetPropertyBlock(properties);
    }

    /// <summary>
    /// Der Dialog für den aktuellen Story-Schritt, oder null.
    ///
    /// Bewusst über MissionManager.NextStoryMission statt über einen eigenen
    /// "schon geredet"-Bool: so bleibt der MissionManager die einzige Wahrheit und ein
    /// geladener Spielstand setzt jeden NPC automatisch richtig.
    /// </summary>
    private DialogueData GetCurrentStoryDialogue()
    {
        if (steps == null || steps.Length == 0) return null;
        if (MissionManager.Instance == null) return null;

        var next = MissionManager.Instance.NextStoryMission;
        if (next == null) return null;
        if (!MissionManager.Instance.ArePrerequisitesMet(next)) return null;

        // Läuft die Mission bereits (Spieler war schon beim zuständigen NPC), hat hier
        // niemand mehr was zu sagen — sonst würde Ozan weiter zum Markt schicken,
        // obwohl der Auftrag längst im Quest-Log steht.
        foreach (var active in MissionManager.Instance.ActiveMissions)
            if (active.Data.missionId == next.missionId) return null;

        foreach (var step in steps)
        {
            if (step?.dialogue == null) continue;
            if (step.missionId != next.missionId) continue;
            return step.dialogue;
        }

        return null;
    }

    /// <summary>Vergibt dieser NPC gerade selbst eine Mission? Nur dann leuchtet das "!".</summary>
    public bool HasMissionToGive()
    {
        var dialogue = GetCurrentStoryDialogue();
        return dialogue != null && dialogue.missionToStartAfter != null;
    }

    private void RefreshIndicator()
    {
        if (availableIndicator == null) return;

        bool show = HasMissionToGive() && TutorialManager.Instance?.IsActive != true;
        if (availableIndicator.activeSelf != show)
            availableIndicator.SetActive(show);
    }

    /// <summary>
    /// Spielt den fälligen Story-Dialog ab und meldet, ob es einen gab.
    ///
    /// Einstiegspunkt für den Marktplatz: der läuft <b>nicht</b> über WorldClickHandler/IClickable,
    /// sondern über FarmMarketNpcClickController, der direkt den Shop öffnet. Ein IClickable auf
    /// einem Markt-NPC würde dort schlicht nie feuern.
    /// </summary>
    public bool TryPlayStoryDialogue()
    {
        if (DialogueManager.Instance == null || DialogueManager.Instance.IsActive) return false;
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsActive) return false;

        var storyDialogue = GetCurrentStoryDialogue();
        if (storyDialogue == null) return false;

        DialogueManager.Instance.StartDialogue(storyDialogue);
        RefreshIndicator();
        return true;
    }

    public void OnClick()
    {
        if (DialogueManager.Instance == null || DialogueManager.Instance.IsActive) return;

        // 1. Tutorial hat Vorrang — solange es läuft, ist die Story-Kette ohnehin blockiert.
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsActive)
        {
            if (repeatTutorialDialogue)
                TutorialManager.Instance.RepeatCurrentStepDialogue();
            return;
        }

        // 2. Story-Schritt: Quest-Vergabe oder Wegweiser.
        var storyDialogue = GetCurrentStoryDialogue();
        if (storyDialogue != null)
        {
            DialogueManager.Instance.StartDialogue(storyDialogue);
            RefreshIndicator();
            return;
        }

        // 3. Sonst Smalltalk.
        if (smallTalkDialogue != null)
            DialogueManager.Instance.StartDialogue(smallTalkDialogue);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Markiert ein Objekt (NPC, Scheune, Auto, Komposter …) als mögliches Quest-Ziel.
/// Der <see cref="MissionHighlightDirector"/> schaltet die Outline automatisch ein,
/// solange ein passendes Missions-Objective offen ist.
///
/// Zuordnung passiert in dieser Reihenfolge — der Normalfall braucht KEINE IDs:
///
/// 1. <b>Über den Objective-Typ</b> (<see cref="objectiveTypes"/>): einmal am Objekt
///    eingestellt und es leuchtet ab sofort bei JEDER Mission, die so ein Ziel hat.
///    Deckt praktisch alles ab, weil es pro Szene nur eine Scheune, ein Auto, einen
///    Händler gibt: Scheune → OpenBarn, Auto → TravelToMarket. Null Aufwand pro Mission.
///
/// 2. <b>Zusätzlich über die Zone</b> (automatisch): Zonen sind der einzige Fall, wo
///    der Typ allein nicht reicht — es gibt mehrere ZoneBlocker, aber "Schalte Zone X
///    frei" meint genau einen. Beide Seiten haben die Info längst
///    (<c>MissionObjectiveData.targetZoneId</c> ↔ <c>GridZone.SaveId</c>), sie wird hier
///    nur abgeglichen. Nichts einzutragen.
///
/// 3. <b>Über eine ID</b> (<see cref="highlightId"/>): reines Notfall-Ventil, falls
///    doch mal mehrere Objekte denselben Typ bedienen und keine Zone die Sache klärt.
///    Trägt ein Objective eine ID, gilt NUR die ID — sonst würden alle Objekte desselben
///    Typs mitleuchten und die Eingrenzung wäre wirkungslos.
/// </summary>
[DisallowMultipleComponent]
public class HighlightTarget : MonoBehaviour
{
    [Tooltip("Bei welchen Objective-Typen soll dieses Objekt leuchten?\n" +
             "Das ist der Normalweg — für fast alles reicht der Typ allein, weil es " +
             "pro Szene nur eine Scheune / ein Auto / einen Händler gibt.")]
    [SerializeField] private MissionObjectiveType[] objectiveTypes;

    [Tooltip("NOTFALL-Feld, im Normalfall leer lassen.\n\n" +
             "Nur nötig, wenn mehrere Objekte denselben Objective-Typ bedienen und das " +
             "Objective genau eines davon meint — und sich das nicht schon über die " +
             "Zone auflösen lässt (die wird automatisch erkannt, siehe unten).\n\n" +
             "Wenn gesetzt: dieselbe ID am Objective unter 'targetHighlightId' eintragen.")]
    [SerializeField] private string highlightId;

    [Tooltip("Optional: Pflanzensorte, die dieses Objekt darstellt.\n" +
             "Nur für Pflanzen-Visuals nötig, damit 'Ernte 3 Karotten' nicht auch die " +
             "Kartoffeln zum Leuchten bringt.")]
    [SerializeField] private PlantType plantType;

    [Tooltip("Optional: Werkzeug, das dieses Element darstellt.\n" +
             "Damit trifft 'Wähle die Hacke' nur den Hacken-Slot statt der ganzen Hotbar.\n" +
             "Bei Hotbar-Slots setzt HotbarUI das beim Spawnen automatisch — die Slots " +
             "stammen alle aus demselben Prefab, ein fester Inspector-Wert könnte sie " +
             "also gar nicht unterscheiden.")]
    [SerializeField] private ToolType toolType = ToolType.None;

    [Tooltip("Zeigt der Bildschirmrand-Pfeil auf dieses Objekt, wenn es außerhalb des " +
             "Bildes liegt? Für UI-Elemente sinnlos (die sind immer im Bild).")]
    [SerializeField] private bool showOffScreenIndicator = true;

    /// <summary>
    /// Wie hervorgehoben wird — Weltkontur oder UI-Rahmen. Bewusst über das Interface
    /// statt über <see cref="HighlightOutline"/> direkt: Hotbar-Slots liegen auf einem
    /// Canvas und tauchen in der Kamera-Maske gar nicht auf, brauchen also eine andere
    /// Darstellung bei identischer Auswahl-Logik.
    /// </summary>
    private IHighlightVisual visual;

    /// <summary>
    /// Zone, zu der dieses Objekt gehört — automatisch aus dem Eltern-<see cref="GridZone"/>
    /// gelesen, damit für Zonen-Ziele niemand IDs von Hand vergeben muss.
    ///
    /// Zonen sind der einzige Objective-Typ, bei dem der Typ allein nicht reicht: es gibt
    /// mehrere ZoneBlocker, aber "Schalte Zone X frei" meint genau einen. Das Objective
    /// trägt dafür längst 'targetZoneId', und GridZone.SaveId liefert die Gegenseite —
    /// der Abgleich passiert also auf Daten, die beide Seiten ohnehin schon haben.
    /// </summary>
    private string zoneId;

    private static readonly List<HighlightTarget> registry = new();

    /// <summary>Alle gerade in der Szene lebenden Ziele.</summary>
    public static IReadOnlyList<HighlightTarget> All => registry;

    /// <summary>
    /// Feuert, wenn Ziele dazukommen oder verschwinden (Szenenwechsel, Spawns).
    /// Der Director hört darauf, um neu zu bewerten — sonst bliebe ein frisch
    /// geladener NPC dunkel, obwohl seine Mission längst läuft.
    /// </summary>
    public static event Action OnRegistryChanged;

    public string HighlightId => highlightId;

    /// <summary>Soll für dieses Ziel ein Pfeil am Bildschirmrand erscheinen?</summary>
    public bool ShowOffScreenIndicator => showOffScreenIndicator && !(visual is HighlightUIOutline);

    private void Awake()
    {
        visual = GetComponent<IHighlightVisual>();
        if (visual == null) visual = GetComponentInChildren<IHighlightVisual>();

        if (visual == null)
            Debug.LogWarning($"{nameof(HighlightTarget)} auf '{name}' findet weder " +
                             $"{nameof(HighlightOutline)} noch {nameof(HighlightUIOutline)} — " +
                             "das Objekt kann nicht leuchten.", this);

        var zone = GetComponentInParent<GridZone>();
        if (zone != null) zoneId = zone.SaveId;
    }

    private void OnEnable()
    {
        registry.Add(this);
        OnRegistryChanged?.Invoke();
    }

    private void OnDisable()
    {
        registry.Remove(this);
        OnRegistryChanged?.Invoke();
    }

    /// <summary>Ist dieses Objekt mit dem gegebenen Objective gemeint?</summary>
    public bool Matches(MissionObjectiveData objective)
    {
        if (objective == null) return false;

        // ID am Objective gesetzt -> exakte Zuordnung, Typ-Regel zählt nicht mehr.
        if (!string.IsNullOrWhiteSpace(objective.targetHighlightId))
            return string.Equals(objective.targetHighlightId.Trim(),
                                 highlightId?.Trim(),
                                 StringComparison.OrdinalIgnoreCase);

        if (objectiveTypes == null) return false;

        bool typeMatches = false;
        for (int i = 0; i < objectiveTypes.Length; i++)
        {
            if (objectiveTypes[i] == objective.type) { typeMatches = true; break; }
        }
        if (!typeMatches) return false;

        // Zonen-Eingrenzung. Ohne die würde bei "Schalte Zone 2 frei" JEDER ZoneBlocker
        // leuchten, der UnlockZone bei sich stehen hat — also auch Zone 1 und 3.
        // Nur prüfen, wenn das Objective eine Zone nennt UND dieses Objekt zu einer
        // Zone gehört: ein Nicht-Zonen-Objekt soll hier nicht durchs Raster fallen.
        if (!string.IsNullOrWhiteSpace(objective.targetZoneId) && !string.IsNullOrWhiteSpace(zoneId))
            return string.Equals(objective.targetZoneId.Trim(), zoneId.Trim(),
                                 StringComparison.OrdinalIgnoreCase);

        // Pflanzen-Eingrenzung, gleiche Idee wie bei Zonen: "Ernte 3 Karotten" soll nicht
        // jede reife Kartoffel im Feld anleuchten. Wieder nur prüfen, wenn beide Seiten
        // eine Sorte nennen.
        if (objective.targetPlantType != null && plantType != null)
            return objective.targetPlantType == plantType;

        // Werkzeug-Eingrenzung, gleiche Regel: nur wenn beide Seiten eins nennen.
        if (objective.targetTool != ToolType.None && toolType != ToolType.None)
            return objective.targetTool == toolType;

        return true;
    }

    /// <summary>
    /// Ersetzt die im Prefab hinterlegten Objective-Typen zur Laufzeit.
    ///
    /// Nötig, wenn aus einem Prefab mehrere *unterschiedliche* Dinge entstehen: die
    /// Hotbar spawnt aus demselben Slot-Prefab echte Werkzeug-Slots, den Baumodus-Knopf
    /// und die Kachel-Auswahl. Ohne eigene Typen würde der Baumodus-Knopf bei jedem
    /// "Wähle Werkzeug X" mitleuchten, weil er kein Werkzeug nennt und damit durch die
    /// Werkzeug-Prüfung durchfällt.
    ///
    /// Leeres Array = dieses Objekt kann über Typen gar nicht mehr gefunden werden.
    /// </summary>
    public void SetObjectiveTypes(params MissionObjectiveType[] types)
    {
        objectiveTypes = types ?? System.Array.Empty<MissionObjectiveType>();
        OnRegistryChanged?.Invoke();
    }

    /// <summary>
    /// Setzt das Werkzeug zur Laufzeit. Für Hotbar-Slots nötig, die alle aus demselben
    /// Prefab entstehen und ihre Bedeutung erst beim Spawnen bekommen.
    /// </summary>
    public void SetToolContext(ToolType tool)
    {
        if (toolType == tool) return;
        toolType = tool;

        // Neu bewerten lassen — der Slot kann durch die Änderung erst jetzt zum
        // aktuellen Ziel passen (oder eben nicht mehr).
        OnRegistryChanged?.Invoke();
    }

    public void SetHighlighted(bool on)
    {
        visual?.SetHighlighted(on);
    }

    // --- Test-Hilfen ---
    // Im Play Mode über das ⋮-Menü der Component aufrufbar. Damit lässt sich prüfen, ob
    // Rahmen/Kontur überhaupt korrekt sitzen, ohne dafür eine Mission bauen zu müssen.
    // Hält bis zum nächsten Missions-Ereignis — dann übernimmt wieder der Director.

    [ContextMenu("Test: Highlight an")]
    private void DebugHighlightOn() => SetHighlighted(true);

    [ContextMenu("Test: Highlight aus")]
    private void DebugHighlightOff() => SetHighlighted(false);
}

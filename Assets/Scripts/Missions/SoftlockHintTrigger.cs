using UnityEngine;

/// <summary>
/// Erkennt automatisch, wenn der Spieler wirklich feststeckt (kein Gold für auch nur den
/// günstigsten Samen, keiner mehr im Inventar, NICHTS Verkaufbares im Erntekorb UND nichts
/// wächst gerade auf dem Feld) und zeigt einmalig einen Hinweis-Dialog, der auf die
/// Not-Samen-Mechanik beim Gras-Mähen aufmerksam macht (siehe
/// <see cref="PlantManager.TryGatherGrass"/>).
///
/// Die letzten beiden Bedingungen sind bewusst dazugekommen: "0 Samen im Inventar" heißt
/// NICHT automatisch stecken geblieben — hat der Spieler gerade alle Samen ausgesät, wächst
/// irgendwo eine Ernte heran, die gleich Geld für neue Samen bringt. Genauso zählt Ernte,
/// die schon im Inventar liegt, aber noch nicht verkauft wurde. Ohne diese Checks feuerte
/// der Hinweis fälschlich genau in dem Moment, in dem der Spieler alles Richtige getan hat.
///
/// Bewusst kein MissionData: es gibt keinen passenden MissionObjectiveType für "Gras
/// mähen", und eine neue Mission landet nur still im Seitenpanel statt aufzufallen — genau
/// dann, wenn der Spieler frustriert ist, soll die Erklärung aber nicht zu übersehen sein.
/// Ein Dialog-Popup ist dieselbe Bühne, über die auch das Tutorial Mechaniken erklärt.
///
/// Setup: irgendwo in der SampleScene ablegen (z.B. neben dem SfxManager) und
/// <see cref="hintDialogue"/> auf GrasNotSamenHinweis.asset zeigen lassen.
/// </summary>
public class SoftlockHintTrigger : MonoBehaviour
{
    [Tooltip("Dialog, der einmalig gezeigt wird sobald der Spieler feststeckt. " +
             "GrasNotSamenHinweis.asset hierher ziehen.")]
    [SerializeField] private DialogueData hintDialogue;

    [Tooltip("Save-ID des günstigsten Samens — muss zu PlantManager.starterSeedId passen. " +
             "Bestimmt die Geld-Schwelle: darunter kann sich der Spieler nichts mehr kaufen.")]
    [SerializeField] private string cheapestSeedId = "carrot";

    [Tooltip("Wie oft (Sekunden) zusätzlich zu den Inventar-Events nachgeschaut wird. " +
             "Fängt den Fall ab, dass der Spieler genau dann feststeckt, während gerade " +
             "ein anderer Dialog läuft — ohne das würde der Hinweis sonst nie nachgeholt.")]
    [SerializeField] private float pollInterval = 2f;

    // STATIC, nicht pro Instanz: das Objekt liegt direkt in der SampleScene (kein
    // DontDestroyOnLoad-Prefab). Jede Fahrt zum Markt und zurück lädt die Szene neu und
    // damit auch dieses Feld — mit einem Instanzfeld hätte ein weiterhin klammer Spieler
    // den Hinweis bei jeder Rückkehr erneut bekommen ("feuert die ganze Zeit"). Static
    // hält den "einmalig"-Schutz für die ganze Session, wie ursprünglich beabsichtigt —
    // bleibt bewusst ungespeichert (siehe Klassenkommentar), nur eben session-weit korrekt.
    private static bool alreadyShown;

    private void OnEnable()
    {
        var inv = PlayerInventory.Instance;
        if (inv != null)
        {
            inv.OnMoneyChanged += HandleMoneyChanged;
            inv.OnSeedsChanged += HandleSeedsChanged;
            inv.OnCropsChanged += HandleCropsChanged;
        }

        InvokeRepeating(nameof(CheckNow), pollInterval, pollInterval);
        CheckNow();
    }

    private void OnDisable()
    {
        var inv = PlayerInventory.Instance;
        if (inv != null)
        {
            inv.OnMoneyChanged -= HandleMoneyChanged;
            inv.OnSeedsChanged -= HandleSeedsChanged;
            inv.OnCropsChanged -= HandleCropsChanged;
        }

        CancelInvoke(nameof(CheckNow));
    }

    private void HandleMoneyChanged(int _) => CheckNow();
    private void HandleSeedsChanged(PlantType _, int __) => CheckNow();
    private void HandleCropsChanged(PlantType _, int __) => CheckNow();

    private void CheckNow()
    {
        if (alreadyShown || hintDialogue == null) return;

        // SampleScene.PlayerInventory startet mit startingMoney=0 und leeren Seeds — das
        // sind reine Platzhalterwerte für den kurzen Moment zwischen Scene-Awake und dem
        // Moment, in dem FarmSaveManager den echten Spielstand einspielt (PlayerInventory.
        // ApplyLoadedData). Ohne diese Sperre sah IsStuck() in genau diesem Fenster IMMER
        // "0 Geld, 0 Samen" — der Dialog feuerte dann fälschlich, obwohl der Spieler laut
        // Save längst Geld und Samen hatte. FarmSaveManager.IsLoading deckt exakt dieses
        // Fenster ab (true ab Szenen-Load-Start bis ApplySaveData fertig ist).
        if (FarmSaveManager.Instance != null && FarmSaveManager.Instance.IsLoading) return;

        bool dialogueBusy = DialogueManager.Instance == null || DialogueManager.Instance.IsActive;
        bool stuck = IsStuck();

        // Absichtlich immer geloggt, nicht nur im Fehlerfall — sonst weiß man beim
        // Debuggen nie, ob der Poll überhaupt läuft oder das Objekt inaktiv ist.
        Debug.Log($"[SoftlockHintTrigger] Check — dialogueBusy={dialogueBusy}, stuck={stuck}, " +
                  $"money={PlayerInventory.Instance?.Money.ToString() ?? "kein PlayerInventory"}, " +
                  $"plantDatabase={(PlantDatabase.Instance != null ? "ok" : "FEHLT")}");

        if (dialogueBusy || !stuck) return;

        // Erst hier auf true setzen, nicht schon beim Prüfen — blockiert gerade ein
        // anderer Dialog, soll der Poll es beim nächsten Mal erneut versuchen dürfen.
        alreadyShown = true;
        DialogueManager.Instance.StartDialogue(hintDialogue);
    }

    private bool IsStuck()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return false;

        PlantType cheapest = PlantDatabase.Instance?.GetById(cheapestSeedId);

        // Kein hartkodierter Fallback wie "1" mehr: schlägt die Datenbank-Suche fehl, weiß
        // ich den echten Preis nicht — dann lieber die Geld-Schranke ignorieren (int.MaxValue,
        // "zu wenig Geld" ist immer wahr) als einen falschen Schwellwert zu raten, an dem der
        // Spieler bei z.B. 2 Gold fälschlich als "nicht klamm genug" durchrutscht.
        int threshold = cheapest != null ? cheapest.seedPrice : int.MaxValue;

        int totalSeeds = 0;
        foreach (var kvp in inv.GetAllSeeds())
            totalSeeds += kvp.Value;

        // Ernte im Inventar, die noch nicht verkauft wurde — kann sofort zu Geld gemacht
        // werden, zählt also wie Geld auf der Bank.
        int totalCrops = 0;
        foreach (var kvp in inv.GetAllCrops())
            totalCrops += kvp.Value;

        // Wächst irgendwo eine Pflanze (egal in welcher Phase)? Dann kommt in absehbarer
        // Zeit von selbst wieder Ernte rein — kein Grund, jetzt schon einzugreifen.
        int growingPlants = PlantManager.Instance != null ? PlantManager.Instance.ActivePlants.Count : 0;

        bool noMoneyForSeeds = inv.Money < threshold;
        bool noSeeds = totalSeeds <= 0;
        bool noSellableCrops = totalCrops <= 0;
        bool nothingGrowing = growingPlants <= 0;

        Debug.Log($"[SoftlockHintTrigger] IsStuck — money={inv.Money}, threshold={threshold} " +
                  $"({(cheapest != null ? cheapest.plantName : "PLANT NICHT GEFUNDEN: " + cheapestSeedId)}), " +
                  $"totalSeeds={totalSeeds}, totalCrops={totalCrops}, growingPlants={growingPlants}");

        return noMoneyForSeeds && noSeeds && noSellableCrops && nothingGrowing;
    }
}

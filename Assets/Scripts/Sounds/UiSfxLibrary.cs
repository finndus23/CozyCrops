using UnityEngine;

/// <summary>
/// Sammlung der Menü- und Feedback-Klänge. Ein Asset, das in allen Szenen dasselbe ist —
/// so klingt ein Kauf im Marktplatz genauso wie im Farm-Shop, ohne dass man die Clips
/// dreimal von Hand zuweist.
///
/// Anlegen über: Rechtsklick im Project-Fenster → Create → Cozy Crops → UI Sfx Library.
/// Ablegen unter Assets/Scripts/Sounds/.
///
/// Alle Felder sind optional. Was leer bleibt, bleibt stumm — kein Fehler.
/// </summary>
[CreateAssetMenu(fileName = "UiSfxLibrary", menuName = "Cozy Crops/UI Sfx Library")]
public class UiSfxLibrary : ScriptableObject
{
    [Header("Buttons")]
    [Tooltip("Standard-Klick. Wird automatisch an jeden Button in der Szene gehängt.\n\n" +
             "Mehrere Varianten eintragen — Buttons werden im Menü in schneller Folge " +
             "gedrückt, und ein einziger Clip fällt dabei sofort als Wiederholung auf.")]
    public AudioClip[] buttonClick;

    [Tooltip("Zurück/Abbrechen. Optional — bleibt es leer, wird buttonClick verwendet.")]
    public AudioClip[] buttonBack;

    [Header("Fenster")]
    public AudioClip[] panelOpen;
    public AudioClip[] panelClose;

    [Header("Wirtschaft")]
    [Tooltip("Kauf getätigt — Werkzeug, Saatgut, Upgrade.")]
    public AudioClip[] purchase;

    [Tooltip("Verkauf / Geld eingenommen. Klingt idealerweise wie Münzen.")]
    public AudioClip[] sell;

    [Tooltip("Aktion nicht möglich — zu wenig Gold, Lizenz fehlt, Tile gesperrt.\n" +
             "Kurz und trocken halten. Ein langer Fehlerton nervt beim zweiten Mal.")]
    public AudioClip[] denied;

    [Header("Werkzeuge")]
    [Tooltip("Werkzeug aufgewertet. Darf deutlich sein — kostet Gold und passiert selten. " +
             "Metallisch, aufsteigend.")]
    public AudioClip[] toolUpgraded;

    [Tooltip("Neues Werkzeug gekauft. Leer = es wird purchase verwendet.")]
    public AudioClip[] toolAcquired;

    [Header("Missionen")]
    public AudioClip[] missionStarted;

    [Tooltip("Ein einzelnes Ziel innerhalb einer Mission ist erfüllt (\"10/10 geerntet\").\n\n" +
             "Klar kleiner halten als missionCompleted — es ist ein Zwischenschritt. Ein " +
             "kurzer heller Ton reicht; kommt hier etwas Großes, wirkt der eigentliche " +
             "Abschluss danach wie ein Rückschritt.")]
    public AudioClip[] objectiveCompleted;

    [Tooltip("Die ganze Mission ist abgeschlossen. Darf auffallen — passiert selten und " +
             "ist der Moment, auf den die Ziele hingearbeitet haben.")]
    public AudioClip[] missionCompleted;

    [Tooltip("Ersatz für coinFlight, falls das leer ist — hier lag der Münz-Klang früher.\n\n" +
             "Wird nicht mehr vom Missions-Event ausgelöst: den Klang beim Abholen liefert " +
             "jetzt der Münzflug selbst, damit er beim Verkauf genauso kommt.")]
    public AudioClip[] rewardCollected;

    [Tooltip("Sichel findet beim Gras-Mähen einen Not-Samen (Anti-Softlock). Leer = " +
             "rewardCollected. Soll sich nach einem kleinen Glücksfund anhören, nicht wie " +
             "ein normaler Pickup — passiert selten und meist genau dann, wenn's brennt.")]
    public AudioClip[] starterSeedFound;

    [Tooltip("Dünger aus dem Komposter abgeholt (Klick auf den fertigen Komposter). " +
             "Leer = rewardCollected.")]
    public AudioClip[] fertilizerCollected;

    [Tooltip("Komposter ist FERTIG GEBRAUT — spielt einmalig genau in dem Moment, in dem " +
             "der Timer bei 0 ankommt, nicht erst beim Abholen. Soll auffallen, damit man's " +
             "über den Farmlärm hinweg mitbekommt, auch wenn man gerade woanders steht. " +
             "Leer = rewardCollected.")]
    public AudioClip[] compostReady;

    [Header("Münzflug")]
    [Tooltip("Klingt, sobald fliegende Münzen ankommen — bei Missions-Belohnungen genauso " +
             "wie beim Verkauf.\n\n" +
             "Der Klang gehört zur Animation, nicht zum Anlass: Münzen, die sichtbar " +
             "einschlagen, aber nichts von sich geben, wirken wie ein fehlendes Stück.")]
    public AudioClip[] coinFlight;

    [Header("Lautstärke")]
    [Range(0f, 1f)]
    public float volume = 0.8f;

    [Tooltip("Tonhöhenstreuung für UI-Klänge. Bewusst kleiner als bei Weltgeräuschen: " +
             "ein Button soll verlässlich klingen, nicht jedes Mal anders.")]
    [Range(0f, 0.3f)]
    public float pitchJitter = 0.03f;
}

using UnityEngine;

/// <summary>
/// Sagt dem <see cref="MusicDirector"/>, was in dieser Szene laufen soll. Ein Exemplar pro
/// Szene, jeweils mit eigenen Tracks im Inspector.
///
/// Der Director selbst kennt weder Szenen noch Baumodus — er kann nur abspielen und
/// überblenden. Diese Trennung ist der Grund, warum ein neuer Auslöser (Nacht, Regen,
/// Questszene) hier drei Zeilen kostet und nicht einen Umbau am Abspieler.
///
/// Setup: leeres GameObject in der Szene, Komponente drauf, Tracks zuweisen.
/// Ersetzt den alten MusicManager.
/// </summary>
public class MusicCues : MonoBehaviour
{
    [Header("Grundmusik")]
    [Tooltip("Läuft, solange nichts anderes obenauf liegt.")]
    [SerializeField] private MusicTrack sceneTrack;

    [Tooltip("Überblendzeit beim Betreten der Szene. Länger als bei Zustandswechseln — " +
             "ein Szenenwechsel darf sich Zeit lassen.")]
    [SerializeField] private float sceneFade = 2f;

    [Header("Baumodus")]
    [Tooltip("Läuft, solange der Baumodus aktiv ist. Leer = Musik bleibt unverändert.")]
    [SerializeField] private MusicTrack buildModeTrack;

    [Tooltip("Überblendzeit beim Umschalten des Baumodus. Kurz halten: der Wechsel ist die " +
             "Antwort auf einen Tastendruck und soll sich unmittelbar anfühlen.")]
    [SerializeField] private float buildModeFade = 0.5f;

    private void OnEnable()
    {
        // Statische Events — die gibt es auch, wenn der BuildModeManager sein Awake noch
        // nicht durchlaufen hat. Über BuildModeManager.Instance müsste man hier auf die
        // Reihenfolge der Skripte hoffen.
        BuildModeManager.OnBuildModeEnteredStatic += HandleBuildModeEntered;
        BuildModeManager.OnBuildModeExitedStatic  += HandleBuildModeExited;
    }

    private void OnDisable()
    {
        BuildModeManager.OnBuildModeEnteredStatic -= HandleBuildModeEntered;
        BuildModeManager.OnBuildModeExitedStatic  -= HandleBuildModeExited;

        // Beim Szenenwechsel darf die Baumodus-Musik nicht auf dem Stapel liegen bleiben —
        // der Director überlebt die Szene, dieses Objekt nicht.
        if (buildModeTrack != null)
            MusicDirector.Instance?.Pop(buildModeTrack, 0f);
    }

    private void Start()
    {
        if (MusicDirector.Instance == null)
        {
            Debug.LogWarning("[MusicCues] Kein MusicDirector vorhanden — die Szene bleibt stumm.");
            return;
        }

        MusicDirector.Instance.SetBase(sceneTrack, sceneFade);

        // Falls der Baumodus schon aktiv war, bevor diese Szene fertig geladen hat.
        if (BuildModeManager.Instance != null && BuildModeManager.Instance.IsActive)
            HandleBuildModeEntered();
    }

    private void HandleBuildModeEntered()
    {
        if (buildModeTrack == null) return;
        MusicDirector.Instance?.Push(buildModeTrack, buildModeFade);
    }

    private void HandleBuildModeExited()
    {
        if (buildModeTrack == null) return;
        MusicDirector.Instance?.Pop(buildModeTrack, buildModeFade);
    }
}

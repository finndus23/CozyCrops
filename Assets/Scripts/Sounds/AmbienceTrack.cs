using UnityEngine;

/// <summary>
/// Eine Ambience-Ebene: ein Dauerloop, der unter der Musik liegt.
///
/// Getrennt vom <see cref="MusicTrack"/>, obwohl beide fast gleich aussehen — die
/// Grundlautstärken liegen in völlig verschiedenen Bereichen, und die beiden Sorten sollen
/// im Project-Fenster nicht durcheinandergeraten.
///
/// Anlegen: Rechtsklick → Create → Cozy Crops → Ambience Track.
/// </summary>
[CreateAssetMenu(fileName = "AmbienceTrack", menuName = "Cozy Crops/Ambience Track")]
public class AmbienceTrack : ScriptableObject
{
    public AudioClip clip;

    [Tooltip("Grundlautstärke, 0–1.\n\n" +
             "Deutlich niedriger ansetzen als bei Musik — Faustwert 0.15–0.35. Ambience " +
             "soll auffallen, wenn man darauf achtet, und sonst nicht.")]
    [Range(0f, 1f)]
    public float volume = 0.25f;

    [Tooltip("Einblendzeit. -1 = Standardwert des AmbienceDirector.\n\n" +
             "Ruhig lang wählen (3–5 s): Ambience, die einsetzt wie ein Schalter, wirkt " +
             "wie ein Fehler. Musik darf einsetzen, Umgebung ist einfach da.")]
    public float fadeIn = -1f;

    public float fadeOut = -1f;
}

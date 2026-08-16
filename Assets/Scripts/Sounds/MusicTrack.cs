using UnityEngine;

/// <summary>
/// Ein Musikstück samt seiner Abspiel-Eigenschaften.
///
/// Warum ein Asset und nicht einfach ein AudioClip: Lautstärke und Überblendzeit gehören
/// zum Stück, nicht zur Stelle, an der es angefordert wird. Ein ruhiges Baumodus-Thema
/// braucht eine andere Grundlautstärke als das Farm-Thema — steht das an jeder Aufrufstelle
/// einzeln, weicht es früher oder später auseinander.
///
/// Anlegen: Rechtsklick im Project-Fenster → Create → Cozy Crops → Music Track.
/// Ablegen unter Assets/Scripts/Sounds/tracks/.
/// </summary>
[CreateAssetMenu(fileName = "MusicTrack", menuName = "Cozy Crops/Music Track")]
public class MusicTrack : ScriptableObject
{
    public AudioClip clip;

    [Tooltip("Grundlautstärke dieses Stücks, 0–1. Gleicht Pegelunterschiede zwischen " +
             "Dateien aus, damit nicht ein Track deutlich lauter hereinkommt als der andere.")]
    [Range(0f, 1f)]
    public float volume = 0.5f;

    public bool loop = true;

    [Tooltip("Überblendzeit beim Einblenden. -1 = Standardwert des MusicDirector.\n\n" +
             "Kurz (unter 0,5 s) für Wechsel, die auf eine Spieleraktion folgen — sonst " +
             "wirkt die Reaktion verzögert. Länger für atmosphärische Wechsel.")]
    public float fadeIn = -1f;

    [Tooltip("Überblendzeit beim Ausblenden. -1 = Standardwert des MusicDirector.")]
    public float fadeOut = -1f;
}

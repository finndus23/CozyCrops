using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Zentrale Wiedergabe für kurze Effekte. Hält einen Pool aus AudioSources und
/// spielt darüber One-Shots und Loops ab.
///
/// <b>Warum ein Pool und nicht AudioSource.PlayClipAtPoint():</b> die Unity-Methode legt
/// pro Aufruf ein GameObject mit AudioSource an und zerstört es nach der Cliplänge wieder.
/// Bei Farmarbeit im Sekundentakt ist das dauerhafter Garbage — genau die Sorte Allokation,
/// die auf einem schwächeren Laptop später als Ruckler auffällt.
///
/// <b>Warum Pitch-Jitter:</b> ohne ihn klingt der zehnte Hackschlag exakt wie der erste.
/// Das Ohr erkennt sofort, dass dieselbe Datei läuft, und die Aktion fühlt sich billig an.
/// Ein paar Prozent Tonhöhenstreuung reichen, damit es lebendig wirkt.
///
/// Läuft komplett ohne zugewiesene Clips — dann bleibt es eben still, nichts wirft Fehler.
/// So kann die Anbindung stehen, bevor die Sounds da sind.
/// </summary>
public class SfxManager : MonoBehaviour
{
    public static SfxManager Instance { get; private set; }

    [Header("Ausgabe")]
    [Tooltip("Optional. Leer = direkt an den AudioListener. Sobald es einen Lautstärkeregler " +
             "im Optionsmenü geben soll, hier die SFX-Gruppe des AudioMixers eintragen.")]
    [SerializeField] private AudioMixerGroup outputGroup;

    [Header("Stimmen")]
    [Tooltip("Wie viele Effekte gleichzeitig klingen dürfen. Ist alles belegt, wird die " +
             "älteste Stimme überschrieben — lieber ein abgeschnittener Klick als eine " +
             "Warteschlange, die den Ton hinter das Bild schiebt.")]
    [SerializeField] private int voiceCount = 12;

    [Header("Klang")]
    [Tooltip("Zufällige Tonhöhenstreuung pro Abspielung (±). 0 = alle Abspielungen identisch.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float pitchJitter = 0.08f;

    [Tooltip("An = der Effekt kommt aus der Richtung der Aktion. Bei der isometrischen " +
             "Kamera ein deutlicher Gewinn, weil man Aktionen am Bildrand hört.")]
    [SerializeField] private bool spatial = true;

    [Tooltip("Bis zu dieser Entfernung ist der Effekt in voller Lautstärke zu hören. " +
             "Großzügig halten — die Kamera ist weit weg, bei kleinen Werten wird alles leise.")]
    [SerializeField] private float minDistance = 12f;

    [SerializeField] private float maxDistance = 60f;

    [Tooltip("Sperre gegen doppelte Auslöser im selben Moment. Verhindert, dass ein Clip " +
             "durch zwei Events im selben Frame doppelt und damit deutlich lauter startet.")]
    [SerializeField] private float duplicateGuard = 0.04f;

    private readonly List<AudioSource> voices = new();
    private readonly Dictionary<AudioClip, float> lastPlayed = new();

    // Merkt sich pro Clip-Satz die zuletzt gezogene Variante, damit nicht zweimal
    // hintereinander dieselbe kommt — echter Zufall wirkt an der Stelle wie ein Fehler.
    private readonly Dictionary<AudioClip[], int> lastVariant = new();

    private int nextVoice;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildVoices();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildVoices()
    {
        for (int i = 0; i < Mathf.Max(1, voiceCount); i++)
        {
            var go = new GameObject($"Voice {i}");
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = outputGroup;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;

            voices.Add(source);
        }
    }

    // ── One-Shots ─────────────────────────────────────────────────────────────

    /// <summary>Spielt eine zufällige Variante aus <paramref name="clips"/> an einer Position.</summary>
    public void Play(AudioClip[] clips, Vector3 position, float volume = 1f)
    {
        var clip = PickVariant(clips);
        if (clip != null) Play(clip, position, volume);
    }

    public void Play(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;

        // Doppelte Auslöser im selben Moment abfangen. Zwei identische Clips exakt
        // gleichzeitig sind nicht doppelt so laut, sondern klingen durch Phasing schlicht
        // kaputt — und das passiert genau dann, wenn zwei Systeme am selben Ereignis hängen.
        if (lastPlayed.TryGetValue(clip, out float last) && Time.unscaledTime - last < duplicateGuard)
            return;

        lastPlayed[clip] = Time.unscaledTime;

        var voice = TakeVoice();
        ConfigureVoice(voice, position);

        voice.clip   = clip;
        voice.loop   = false;
        voice.volume = volume;
        voice.pitch  = 1f + Random.Range(-pitchJitter, pitchJitter);
        voice.Play();
    }

    // ── Loops ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Startet einen Dauerton (Gießen, Hacken …) und gibt die Stimme als Handle zurück.
    /// Der Aufrufer ist fürs Stoppen zuständig — <see cref="StopLoop"/>.
    ///
    /// Das Handle wird bewusst nicht aus dem normalen Ringpuffer genommen: sonst könnte
    /// eine später gestartete Stimme dieselbe Quelle übernehmen, und der Aufrufer würde
    /// beim Stoppen einen fremden Sound abwürgen.
    /// </summary>
    public AudioSource PlayLoop(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return null;

        var voice = TakeFreeVoice();
        if (voice == null) return null;

        ConfigureVoice(voice, position);

        voice.clip   = clip;
        voice.loop   = true;
        voice.volume = volume;
        voice.pitch  = 1f + Random.Range(-pitchJitter, pitchJitter);
        voice.Play();

        return voice;
    }

    /// <summary>
    /// Beendet einen Dauerton. <paramref name="fadeOut"/> verhindert das Knacken, das
    /// entsteht, wenn eine Welle mitten im Ausschlag hart abgeschnitten wird.
    /// </summary>
    public void StopLoop(AudioSource handle, float fadeOut = 0.08f)
    {
        if (handle == null || !handle.isPlaying) return;

        if (fadeOut <= 0f)
        {
            handle.Stop();
            handle.loop = false;
            return;
        }

        StartCoroutine(FadeOutAndStop(handle, fadeOut));
    }

    private System.Collections.IEnumerator FadeOutAndStop(AudioSource source, float duration)
    {
        float start = source.volume;
        float t = 0f;

        while (t < duration && source != null && source.isPlaying)
        {
            t += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(start, 0f, t / duration);
            yield return null;
        }

        if (source == null) yield break;

        source.Stop();
        source.loop = false;
        source.volume = start;
    }

    // ── Stimmenverwaltung ─────────────────────────────────────────────────────

    /// <summary>Nächste Stimme im Ringpuffer — notfalls wird die älteste überschrieben.</summary>
    private AudioSource TakeVoice()
    {
        // Erst nach einer freien suchen, damit ein laufender Effekt nicht unnötig
        // abgeschnitten wird, solange noch Luft ist.
        var free = TakeFreeVoice();
        if (free != null) return free;

        var voice = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Count;
        return voice;
    }

    private AudioSource TakeFreeVoice()
    {
        foreach (var voice in voices)
            if (!voice.isPlaying) return voice;

        return null;
    }

    private void ConfigureVoice(AudioSource voice, Vector3 position)
    {
        voice.transform.position = position;
        voice.spatialBlend = spatial ? 1f : 0f;
        voice.minDistance = minDistance;
        voice.maxDistance = maxDistance;
    }

    private AudioClip PickVariant(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        if (clips.Length == 1) return clips[0];

        lastVariant.TryGetValue(clips, out int last);

        int index = Random.Range(0, clips.Length);
        if (index == last) index = (index + 1) % clips.Length;

        lastVariant[clips] = index;
        return clips[index];
    }
}

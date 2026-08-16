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
    [Tooltip("Welt-Effekte: Werkzeuge, Bauen, Ernte. Leer = direkt an den AudioListener.")]
    [SerializeField] private AudioMixerGroup outputGroup;

    [Tooltip("Menü- und Rückmeldungsklänge. Leer = es wird outputGroup benutzt.\n\n" +
             "Getrennt regelbar zu machen lohnt sich: Welt-Effekte darf man weit " +
             "herunterziehen, ohne dass das Menü unbedienbar wird.")]
    [SerializeField] private AudioMixerGroup uiOutputGroup;

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

    [Tooltip("Zusätzliche Sperrzeit in Sekunden, in der derselbe Clip nicht erneut startet.\n\n" +
             "Der Schutz gegen doppelte Auslöser im selben Frame läuft unabhängig davon immer. " +
             "Dieser Wert ist nur für den Fall, dass zwei Systeme ein Ereignis ein paar Frames " +
             "versetzt melden.\n\n" +
             "0 lassen. Gemaltes Bauen und schnelle Werkzeugketten erzeugen absichtlich viele " +
             "gleiche Clips kurz hintereinander — eine Sperrzeit verschluckt genau die.")]
    [SerializeField] private float duplicateGuard = 0f;

    [Header("Stille am Clip-Anfang")]
    [Tooltip("Startet Clips hinter der Stille am Anfang. Viele Sound-Pakete haben vor dem " +
             "eigentlichen Geräusch ein paar Hundertstel Leerlauf — bei einem Klick ist das " +
             "als Verzögerung hörbar, weil der Ton nicht mehr auf denselben Frame fällt wie " +
             "das Bild.\n\n" +
             "Repariert nur die Wiedergabe. Sauberer ist, die Datei zu kürzen: das spart " +
             "zusätzlich Speicher und wirkt überall, auch außerhalb des SfxManagers.")]
    [SerializeField] private bool skipLeadingSilence = true;

    [Tooltip("Ab welchem Pegel ein Sample als Ton gilt. Höher stellen, wenn die Clips ein " +
             "leises Grundrauschen haben und die Erkennung deshalb nicht greift.")]
    [Range(0f, 0.05f)]
    [SerializeField] private float silenceThreshold = 0.005f;

    private readonly List<AudioSource> voices = new();
    private readonly Dictionary<AudioClip, float> lastPlayed = new();
    private readonly Dictionary<AudioClip, int> lastPlayedFrame = new();

    // Merkt sich pro Clip-Satz die zuletzt gezogene Variante, damit nicht zweimal
    // hintereinander dieselbe kommt — echter Zufall wirkt an der Stelle wie ein Fehler.
    private readonly Dictionary<AudioClip[], int> lastVariant = new();

    // Startversatz pro Clip. Die Analyse liest den kompletten Clip aus, deshalb wird das
    // Ergebnis gemerkt — einmal pro Clip und Sitzung, nicht bei jedem Klick.
    private readonly Dictionary<AudioClip, int> leadingSilence = new();

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
    public void Play(AudioClip[] clips, Vector3 position, float volume = 1f, float pitchScale = 1f)
    {
        var clip = PickVariant(clips);
        if (clip != null) Play(clip, position, volume, pitchScale);
    }

    /// <param name="pitchScale">
    /// Grundtonhöhe, auf die der Jitter aufschlägt. Unter 1 klingt schwerer und größer,
    /// über 1 leichter — praktisch, um aus einem Clip zwei Bedeutungen zu machen, statt
    /// einen zweiten Sound zu suchen.
    /// </param>
    public void Play(AudioClip clip, Vector3 position, float volume = 1f, float pitchScale = 1f)
    {
        if (clip == null) return;

        if (IsBlockedAsDuplicate(clip)) return;

        var voice = TakeVoice();
        ConfigureVoice(voice, position, spatial ? 1f : 0f);

        voice.clip   = clip;
        voice.loop   = false;
        voice.volume = volume;
        voice.pitch  = pitchScale + Random.Range(-pitchJitter, pitchJitter);
        voice.timeSamples = LeadingSilenceSamples(clip);
        voice.Play();
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spielt einen Effekt <b>ohne</b> Raumbezug — immer gleich laut, egal wo die Kamera steht.
    ///
    /// Menü- und Buttonklänge passieren nicht in der Welt, sondern "vor" dem Spieler. Liefe
    /// ein Klick über die 3D-Ausgabe, würde seine Lautstärke davon abhängen, wo die Kamera
    /// zufällig gerade steht — im Baumodus am Kartenrand also deutlich leiser als in der Mitte.
    /// </summary>
    /// <returns>
    /// Die benutzte Stimme, oder null wenn nichts abgespielt wurde. Nur nötig, wenn der
    /// Aufrufer den Klang später vorzeitig beenden will — etwa das Motorgeräusch, das nur
    /// bis zum Ende des Ladebildschirms laufen soll.
    /// </returns>
    public AudioSource PlayUI(AudioClip clip, float volume = 1f, float pitchJitterOverride = -1f)
    {
        if (clip == null) return null;

        if (IsBlockedAsDuplicate(clip)) return null;

        var voice = TakeVoice();
        ConfigureVoice(voice, Vector3.zero, 0f, ui: true);

        float jitter = pitchJitterOverride >= 0f ? pitchJitterOverride : pitchJitter;

        voice.clip   = clip;
        voice.loop   = false;
        voice.volume = volume;
        voice.pitch  = 1f + Random.Range(-jitter, jitter);
        voice.timeSamples = LeadingSilenceSamples(clip);
        voice.Play();

        return voice;
    }

    /// <summary>UI-Variante mit Varianten-Rotation.</summary>
    public AudioSource PlayUI(AudioClip[] clips, float volume = 1f)
    {
        var clip = PickVariant(clips);
        return clip != null ? PlayUI(clip, volume) : null;
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

        ConfigureVoice(voice, position, spatial ? 1f : 0f);

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

    // ── Duplikatschutz ────────────────────────────────────────────────────────

    /// <summary>
    /// Blockt denselben Clip, wenn er im selben Frame schon einmal gestartet wurde.
    ///
    /// Das Problem, um das es geht: zwei Systeme hängen am selben Ereignis und starten
    /// denselben Clip gleichzeitig. Zwei identische Wellen exakt übereinander sind nicht
    /// doppelt so laut, sondern klingen durch Phasing kaputt.
    ///
    /// Die Sperre gilt bewusst nur <b>innerhalb eines Frames</b>. Eine Sperrzeit über
    /// mehrere Frames würde auch das treffen, was ausdrücklich erwünscht ist: beim
    /// gemalten Bauen wird pro überstrichenem Tile derselbe Clip gestartet, und genau
    /// diese Kette soll man hören. Für Rhythmus sorgt dort der Pitch-Jitter, nicht
    /// das Verschlucken jeder zweiten Wiedergabe.
    /// </summary>
    private bool IsBlockedAsDuplicate(AudioClip clip)
    {
        int frame = Time.frameCount;

        if (lastPlayedFrame.TryGetValue(clip, out int lastFrame) && lastFrame == frame)
            return true;

        if (duplicateGuard > 0f
            && lastPlayed.TryGetValue(clip, out float last)
            && Time.unscaledTime - last < duplicateGuard)
            return true;

        lastPlayedFrame[clip] = frame;
        lastPlayed[clip] = Time.unscaledTime;
        return false;
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

    /// <summary>
    /// Richtet eine Stimme für die anstehende Wiedergabe ein.
    ///
    /// Die Mixer-Gruppe wird pro Abspielung gesetzt, nicht einmalig beim Anlegen: die
    /// Stimmen sind ein gemeinsamer Vorrat, und dieselbe Stimme spielt mal einen Weltklang
    /// und im nächsten Moment einen Menüklick. Zwei getrennte Vorräte anzulegen wäre die
    /// Alternative — dann läge aber der eine brach, während dem anderen die Stimmen ausgehen.
    /// </summary>
    private void ConfigureVoice(AudioSource voice, Vector3 position, float spatialBlend, bool ui = false)
    {
        voice.transform.position = position;
        voice.spatialBlend = spatialBlend;
        voice.minDistance = minDistance;
        voice.maxDistance = maxDistance;

        voice.outputAudioMixerGroup = ui && uiOutputGroup != null ? uiOutputGroup : outputGroup;
    }

    /// <summary>
    /// Sucht das erste Sample, das lauter als <see cref="silenceThreshold"/> ist, und gibt
    /// dessen Position zurück. Von dort startet die Wiedergabe — die Stille davor entfällt.
    ///
    /// Nur für kurze Effekte gedacht: die Analyse zieht den gesamten Clip in ein float-Array,
    /// bei Musik wären das schnell hunderte Megabyte. Lange Clips werden deshalb übersprungen.
    /// </summary>
    private int LeadingSilenceSamples(AudioClip clip)
    {
        if (!skipLeadingSilence || clip == null) return 0;

        if (leadingSilence.TryGetValue(clip, out int cached)) return cached;

        // Zu lang für die Analyse, oder die Daten liegen komprimiert im Speicher und
        // GetData käme gar nicht dran. Beides ist kein Fehler — dann eben ohne Versatz.
        if (clip.length > 3f || clip.samples <= 0 || clip.loadState != AudioDataLoadState.Loaded)
            return 0;

        var data = new float[clip.samples * clip.channels];
        if (!clip.GetData(data, 0)) return 0;

        int index = 0;
        while (index < data.Length && Mathf.Abs(data[index]) <= silenceThreshold)
            index++;

        // Komplett stiller Clip — nichts zu überspringen.
        if (index >= data.Length)
        {
            leadingSilence[clip] = 0;
            return 0;
        }

        int offset = index / clip.channels;

        // Ein Stück davor bleiben: Ein Klick beginnt mit einem sehr steilen Anstieg, und
        // exakt auf dem ersten hörbaren Sample einzusetzen schneidet dessen Anfang ab —
        // das knackt dann genauso, wie es vorher zu spät kam.
        offset = Mathf.Max(0, offset - 64);

        leadingSilence[clip] = offset;
        return offset;
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

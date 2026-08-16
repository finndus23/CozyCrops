using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Spielt die Hintergrundmusik und blendet zwischen Stücken über.
///
/// <b>Zwei AudioSources statt einer.</b> Eine einzelne Quelle kann nicht überblenden — sie
/// müsste das alte Stück stoppen, bevor das neue anfängt, und genau das hört man als
/// Schnitt. Also zwei "Decks" wie am Mischpult: eines läuft, beim Wechsel fährt das andere
/// hoch, während das erste heruntergeht.
///
/// <b>Ein Stapel statt einer Variable.</b> Der naive Weg wäre "Baumodus an → Bau-Musik,
/// Baumodus aus → Farm-Musik". Damit muss der Baumodus wissen, was ohne ihn liefe — und
/// sobald es einen zweiten Grund für einen Musikwechsel gibt (Nacht, Regen, Questszene),
/// muss er die auch alle kennen. Stattdessen legt jeder Auslöser sein Stück oben auf den
/// Stapel und nimmt es beim Verlassen wieder herunter. Was danach klingt, ist einfach das
/// nächste Element — niemand muss den Rest kennen.
///
/// <code>
/// Start:        [Farm]          → Farm
/// Baumodus an:  [Farm, Bau]     → Bau
/// Baumodus aus: [Farm]          → Farm
/// </code>
///
/// Setup: auf dasselbe GameObject wie den SfxManager. Ersetzt den alten MusicManager —
/// beide gleichzeitig laufen zu lassen ergibt doppelte Musik.
/// </summary>
public class MusicDirector : MonoBehaviour
{
    public static MusicDirector Instance { get; private set; }

    [Header("Ausgabe")]
    [Tooltip("Optional. Leer = direkt an den AudioListener. Für einen Musik-Regler im " +
             "Optionsmenü hier die Musik-Gruppe des AudioMixers eintragen.")]
    [SerializeField] private AudioMixerGroup outputGroup;

    [Header("Lautstärke")]
    [Tooltip("Gesamtlautstärke der Musik. Wird mit der Lautstärke des jeweiligen Tracks " +
             "multipliziert.")]
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 1f;

    [Header("Überblendung")]
    [Tooltip("Standard-Überblendzeit in Sekunden, wenn der Track nichts eigenes vorgibt.")]
    [SerializeField] private float defaultFade = 1.5f;

    private AudioSource deckA;
    private AudioSource deckB;

    /// <summary>Das Deck, das gerade zu hören ist (oder gerade eingeblendet wird).</summary>
    private AudioSource active;

    private Coroutine fadeRoutine;

    // Unterster Eintrag ist die Grundmusik der Szene, oberster das, was gerade klingt.
    private readonly List<MusicTrack> stack = new();

    private MusicTrack Current => stack.Count > 0 ? stack[^1] : null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        deckA = BuildDeck("Music Deck A");
        deckB = BuildDeck("Music Deck B");
        active = deckA;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private AudioSource BuildDeck(string label)
    {
        var go = new GameObject(label);
        go.transform.SetParent(transform, false);

        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.volume = 0f;
        source.outputAudioMixerGroup = outputGroup;

        // Musik ist nicht im Raum verortet — sie soll nicht leiser werden, wenn die Kamera
        // über die Karte fährt.
        source.spatialBlend = 0f;

        return source;
    }

    // ── Öffentliche Steuerung ─────────────────────────────────────────────────

    /// <summary>
    /// Setzt die Grundmusik — typischerweise einmal pro Szene. Alles, was gerade oben
    /// aufliegt (z.B. Baumodus), bleibt liegen und klingt weiter.
    /// </summary>
    public void SetBase(MusicTrack track, float fade = -1f)
    {
        if (stack.Count == 0) stack.Add(track);
        else stack[0] = track;

        Apply(fade);
    }

    /// <summary>Legt ein Stück oben auf und spielt es. Gegenstück: <see cref="Pop"/>.</summary>
    public void Push(MusicTrack track, float fade = -1f)
    {
        if (track == null) return;

        // Doppelt aufliegen darf es nicht — sonst braucht es zwei Pop-Aufrufe, um es
        // wieder loszuwerden, und die Musik bleibt hängen.
        stack.Remove(track);
        stack.Add(track);

        Apply(fade);
    }

    /// <summary>
    /// Nimmt ein Stück wieder herunter. Es wird gezielt dieses entfernt und nicht einfach
    /// das oberste: kommt ein zweiter Auslöser dazwischen, würde sonst der Baumodus beim
    /// Verlassen dessen Musik abräumen statt seiner eigenen.
    /// </summary>
    public void Pop(MusicTrack track, float fade = -1f)
    {
        if (track == null) return;

        int index = stack.LastIndexOf(track);
        if (index <= 0) return;   // Index 0 ist die Grundmusik und bleibt liegen.

        stack.RemoveAt(index);
        Apply(fade);
    }

    /// <summary>Blendet die Musik aus, ohne den Stapel zu verändern.</summary>
    public void Stop(float fade = -1f) => StartFade(null, fade);

    public void SetVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);

        var track = Current;
        if (active != null && active.isPlaying && track != null)
            active.volume = track.volume * musicVolume;
    }

    // ── Umsetzung ─────────────────────────────────────────────────────────────

    private void Apply(float fade) => StartFade(Current, fade);

    private void StartFade(MusicTrack track, float fade)
    {
        // Läuft das Stück schon, nicht neu anfangen — sonst springt die Musik bei jedem
        // Szenenwechsel zurück auf Anfang.
        if (track != null && active != null && active.isPlaying && active.clip == track.clip)
            return;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);

        float duration = fade >= 0f
            ? fade
            : (track != null && track.fadeIn >= 0f ? track.fadeIn : defaultFade);

        fadeRoutine = StartCoroutine(Crossfade(track, duration));
    }

    private IEnumerator Crossfade(MusicTrack track, float duration)
    {
        var from = active;
        var to = active == deckA ? deckB : deckA;

        // Wurde eine laufende Blende abgebrochen, hängt auf dem Zieldeck noch der Rest der
        // vorletzten Spur. Bei niedriger Lautstärke abzuwürgen ist unhörbar — weiterlaufen
        // zu lassen wäre es nicht.
        to.Stop();
        to.volume = 0f;

        float toTarget = 0f;

        if (track != null && track.clip != null)
        {
            toTarget = track.volume * musicVolume;

            to.clip = track.clip;
            to.loop = track.loop;
            to.Play();
        }

        active = to;

        float fromStart = from != null ? from.volume : 0f;
        float elapsed = 0f;

        // Ungedämpfte Zeit: die Musik soll auch weiterlaufen, wenn das Spiel für ein Menü
        // auf Zeitfaktor 0 steht.
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

            if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, t);
            to.volume = Mathf.Lerp(0f, toTarget, t);

            yield return null;
        }

        if (from != null)
        {
            from.Stop();
            from.volume = 0f;
        }

        to.volume = toTarget;
        fadeRoutine = null;
    }
}

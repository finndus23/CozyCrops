using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// Hält die Ambience-Ebenen unter der Musik. Anders als beim <see cref="MusicDirector"/>
/// laufen hier <b>mehrere Loops gleichzeitig</b> — Wind, Vögel und ein Bach sind kein
/// Entweder-oder, sie ergeben zusammen erst den Ort.
///
/// Deshalb auch kein Stapel wie bei der Musik: es gibt keinen "obersten" Klang, der die
/// anderen verdeckt. Stattdessen sagt jede Szene, welche Ebenen sie haben will, und der
/// Director blendet den Unterschied.
///
/// <b>Warum eine eigene Quelle pro Ebene:</b> nur so kann Wind bleiben, während Vögel
/// verschwinden. Eine gemeinsame Quelle könnte immer nur alles zusammen wechseln.
///
/// Setup: auf das SfxManager-Prefab legen.
/// </summary>
public class AmbienceDirector : MonoBehaviour
{
    public static AmbienceDirector Instance { get; private set; }

    [Header("Ausgabe")]
    [Tooltip("Optional, aber hier lohnt es sich besonders: eine eigene Mixer-Gruppe für " +
             "Ambience lässt sich getrennt von den Effekten regeln — und später unter " +
             "Dialogen absenken, ohne die Effekte mitzunehmen.")]
    [SerializeField] private AudioMixerGroup outputGroup;

    [Header("Lautstärke")]
    [Range(0f, 1f)]
    [SerializeField] private float ambienceVolume = 1f;

    [Header("Überblendung")]
    [Tooltip("Standard-Blendzeit, wenn die Ebene nichts eigenes vorgibt. Großzügig — " +
             "Umgebungsklang soll sich einschleichen, nicht einsetzen.")]
    [SerializeField] private float defaultFade = 3f;

    private readonly Dictionary<AmbienceTrack, AudioSource> active = new();
    private readonly Dictionary<AmbienceTrack, Coroutine> fades = new();

    // Nach einem Szenenwechsel bekommen die AmbienceCues der neuen Szene einen Frame Zeit,
    // ihre Ebenen anzumelden. Melden sie sich, bleiben gemeinsame Ebenen unterbrechungsfrei
    // stehen. Meldet sich niemand, war die neue Szene schlicht nicht vertont — dann ausblenden.
    private bool awaitingClaim;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        awaitingClaim = true;
        StartCoroutine(ClearIfUnclaimed());
    }

    private IEnumerator ClearIfUnclaimed()
    {
        // Zwei Frames: Start() der neuen Szene läuft nach sceneLoaded, aber im selben Frame.
        yield return null;
        yield return null;

        if (awaitingClaim) SetLayers(null);
    }

    // ── Steuerung ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Legt fest, welche Ebenen laufen sollen. Was fehlt, wird ausgeblendet; was dazukommt,
    /// eingeblendet; was in beiden Listen steht, läuft <b>unverändert weiter</b>.
    ///
    /// Letzteres ist der Punkt: wechselt man von der Farm zum Markt und beide wollen Wind,
    /// soll der Wind nicht kurz aussetzen. Der Spieler hört sonst die Szenengrenze.
    /// </summary>
    public void SetLayers(IEnumerable<AmbienceTrack> layers, float fade = -1f)
    {
        awaitingClaim = false;

        var wanted = new HashSet<AmbienceTrack>();
        if (layers != null)
            foreach (var layer in layers)
                if (layer != null && layer.clip != null) wanted.Add(layer);

        // Erst entfernen, was nicht mehr gewünscht ist.
        var stale = new List<AmbienceTrack>();
        foreach (var kvp in active)
            if (!wanted.Contains(kvp.Key)) stale.Add(kvp.Key);

        foreach (var track in stale) Stop(track, fade);

        // Dann ergänzen, was fehlt.
        foreach (var track in wanted) Play(track, fade);
    }

    /// <summary>Blendet eine einzelne Ebene ein. Läuft sie schon, passiert nichts.</summary>
    public void Play(AmbienceTrack track, float fade = -1f)
    {
        if (track == null || track.clip == null) return;
        if (active.ContainsKey(track)) return;

        var source = BuildSource(track);
        active[track] = source;

        float duration = fade >= 0f ? fade : (track.fadeIn >= 0f ? track.fadeIn : defaultFade);

        // Zufälliger Startpunkt im Clip. Startete jede Ebene bei 0, würden zwei Szenen mit
        // derselben Ebene identisch klingen — und der Spieler erkennt die Schleife an ihrem
        // Anfang, statt sie als Umgebung wahrzunehmen.
        source.time = Random.Range(0f, Mathf.Max(0f, track.clip.length - 0.1f));
        source.Play();

        StartFade(track, source, track.volume * ambienceVolume, duration, false);
    }

    /// <summary>Blendet eine einzelne Ebene aus und gibt ihre Quelle frei.</summary>
    public void Stop(AmbienceTrack track, float fade = -1f)
    {
        if (track == null || !active.TryGetValue(track, out var source)) return;

        active.Remove(track);

        float duration = fade >= 0f ? fade : (track.fadeOut >= 0f ? track.fadeOut : defaultFade);
        StartFade(track, source, 0f, duration, true);
    }

    public void SetVolume(float value)
    {
        ambienceVolume = Mathf.Clamp01(value);

        foreach (var kvp in active)
        {
            if (fades.ContainsKey(kvp.Key)) continue;   // Läuft gerade eine Blende: nicht reinpfuschen.
            kvp.Value.volume = kvp.Key.volume * ambienceVolume;
        }
    }

    // ── Umsetzung ─────────────────────────────────────────────────────────────

    private AudioSource BuildSource(AmbienceTrack track)
    {
        var go = new GameObject($"Ambience — {track.name}");
        go.transform.SetParent(transform, false);

        var source = go.AddComponent<AudioSource>();
        source.clip = track.clip;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
        source.outputAudioMixerGroup = outputGroup;

        // 2D: das Bett umgibt den Spieler, es kommt nicht von einem Punkt. Einzelne verortete
        // Geräusche macht AmbienceCues über den SfxManager.
        source.spatialBlend = 0f;

        return source;
    }

    private void StartFade(AmbienceTrack track, AudioSource source, float target,
                           float duration, bool destroyAfter)
    {
        if (fades.TryGetValue(track, out var running) && running != null)
            StopCoroutine(running);

        fades[track] = StartCoroutine(FadeTo(track, source, target, duration, destroyAfter));
    }

    private IEnumerator FadeTo(AmbienceTrack track, AudioSource source, float target,
                               float duration, bool destroyAfter)
    {
        float start = source.volume;
        float elapsed = 0f;

        while (elapsed < duration && source != null)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        fades.Remove(track);

        if (source == null) yield break;

        source.volume = target;

        if (!destroyAfter) yield break;

        source.Stop();
        Destroy(source.gameObject);
    }
}

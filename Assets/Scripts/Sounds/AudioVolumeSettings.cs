using UnityEngine;
using UnityEngine.Audio;

/// <summary>Die regelbaren Kanäle. Reihenfolge egal, Namen werden für PlayerPrefs benutzt.</summary>
public enum AudioChannel
{
    Master,
    Music,
    Ambience,
    Sfx,
    Ui
}

/// <summary>
/// Verbindet Lautstärkeregler mit dem AudioMixer und merkt sich die Einstellung.
///
/// <b>Warum das nicht ohne Umrechnung geht.</b> Ein Regler liefert 0–1, die Lautstärke einer
/// Mixer-Gruppe ist aber ein Dezibelwert von -80 bis +20 — und Dezibel ist logarithmisch.
/// Schreibt man den Reglerwert direkt hinein, liegt der gesamte Weg von 0 bis 1 innerhalb
/// des ersten unhörbaren Prozents. Der Regler scheint dann kaputt zu sein.
///
/// <b>Warum über exponierte Parameter.</b> Eine AudioMixerGroup hat keine Volume-Eigenschaft,
/// die man setzen könnte. Der Weg führt zwingend über einen im Mixer-Fenster exponierten
/// Parameter, der per Namen angesprochen wird. Stimmt der Name nicht, meldet Unity nichts —
/// deshalb prüft <see cref="Apply"/> den Rückgabewert und warnt selbst.
///
/// Setup: auf das SfxManager-Prefab legen, Mixer zuweisen, Parameternamen eintragen.
/// </summary>
public class AudioVolumeSettings : MonoBehaviour
{
    public static AudioVolumeSettings Instance { get; private set; }

    [SerializeField] private AudioMixer mixer;

    [Header("Exponierte Parameter")]
    [Tooltip("Die Namen müssen exakt so lauten wie im Mixer-Fenster unter 'Exposed " +
             "Parameters'. Groß-/Kleinschreibung zählt.")]
    [SerializeField] private string masterParam = "MasterVolume";
    [SerializeField] private string musicParam = "MusicVolume";
    [SerializeField] private string ambienceParam = "AmbienceVolume";
    [SerializeField] private string sfxParam = "SfxVolume";
    [SerializeField] private string uiParam = "UiVolume";

    [Header("Startwerte")]
    [Range(0f, 1f)] [SerializeField] private float defaultMaster = 1f;
    [Range(0f, 1f)] [SerializeField] private float defaultMusic = 0.6f;
    [Range(0f, 1f)] [SerializeField] private float defaultAmbience = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float defaultSfx = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float defaultUi = 0.8f;

    private const string PrefPrefix = "audio.volume.";

    /// <summary>Unterhalb davon gilt der Kanal als stumm — Log10(0) wäre minus unendlich.</summary>
    private const float MinLinear = 0.0001f;

    private const float SilenceDb = -80f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;
        ApplyAll();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Öffentliche API ───────────────────────────────────────────────────────

    /// <summary>Aktuell eingestellter Wert eines Kanals, 0–1.</summary>
    public float Get(AudioChannel channel)
        => PlayerPrefs.GetFloat(PrefPrefix + channel, DefaultFor(channel));

    /// <summary>Setzt einen Kanal und schreibt ihn in die Einstellungen.</summary>
    public void Set(AudioChannel channel, float value)
    {
        value = Mathf.Clamp01(value);

        PlayerPrefs.SetFloat(PrefPrefix + channel, value);
        Apply(channel, value);
    }

    public void Save() => PlayerPrefs.Save();

    public void ResetToDefaults()
    {
        foreach (AudioChannel channel in System.Enum.GetValues(typeof(AudioChannel)))
            Set(channel, DefaultFor(channel));

        Save();
    }

    // ── Umsetzung ─────────────────────────────────────────────────────────────

    private void ApplyAll()
    {
        foreach (AudioChannel channel in System.Enum.GetValues(typeof(AudioChannel)))
            Apply(channel, Get(channel));
    }

    private void Apply(AudioChannel channel, float linear)
    {
        if (mixer == null) return;

        string param = ParamFor(channel);
        if (string.IsNullOrWhiteSpace(param)) return;

        if (!mixer.SetFloat(param, LinearToDecibel(linear)))
            Debug.LogWarning($"[AudioVolumeSettings] Parameter '{param}' gibt es im Mixer nicht. " +
                             "Im Mixer-Fenster den Volume-Regler der Gruppe rechtsklicken → " +
                             "'Expose ... to script', dann oben rechts unter 'Exposed Parameters' " +
                             "exakt so umbenennen.");
    }

    /// <summary>
    /// Rechnet einen Regler (0–1) in Dezibel um.
    ///
    /// Die Umrechnung ist bewusst die reine Formel und keine Kurve nach Gehör: 0.5 landet
    /// damit bei -6 dB, also der halben Amplitude. Das entspricht dem, was ein Regler in
    /// der Bildschirmmitte erwarten lässt.
    /// </summary>
    public static float LinearToDecibel(float linear)
        => linear <= MinLinear ? SilenceDb : Mathf.Log10(linear) * 20f;

    public static float DecibelToLinear(float db)
        => db <= SilenceDb ? 0f : Mathf.Pow(10f, db / 20f);

    private string ParamFor(AudioChannel channel) => channel switch
    {
        AudioChannel.Master   => masterParam,
        AudioChannel.Music    => musicParam,
        AudioChannel.Ambience => ambienceParam,
        AudioChannel.Sfx      => sfxParam,
        AudioChannel.Ui       => uiParam,
        _                     => null
    };

    private float DefaultFor(AudioChannel channel) => channel switch
    {
        AudioChannel.Master   => defaultMaster,
        AudioChannel.Music    => defaultMusic,
        AudioChannel.Ambience => defaultAmbience,
        AudioChannel.Sfx      => defaultSfx,
        AudioChannel.Ui       => defaultUi,
        _                     => 1f
    };
}

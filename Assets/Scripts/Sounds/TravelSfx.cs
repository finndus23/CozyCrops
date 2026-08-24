using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Vertont die Fahrt zwischen Farm und Marktplatz.
///
/// Der <see cref="CarClickHandler"/> feuert bereits statische Events, bevor er die Szene
/// wechselt — es muss also nichts an ihm geändert werden.
///
/// Der Klang läuft über den SfxManager, und der überlebt den Szenenwechsel. Ein Motor, der
/// beim Klick anspringt, spielt also über den Ladebildschirm hinweg zu Ende, statt beim
/// Wechsel abgeschnitten zu werden — genau das kaschiert die Ladezeit.
///
/// Setup: auf das SfxManager-Prefab legen, Clips zuweisen.
/// </summary>
public class TravelSfx : MonoBehaviour
{
    [Tooltip("Klick aufs Auto Richtung Marktplatz. Motorstart oder Wegfahren.\n\n" +
             "Länge ruhig 1–2 Sekunden: der Ladebildschirm kommt direkt danach, und ein " +
             "durchlaufender Klang überbrückt ihn.")]
    [SerializeField] private AudioClip[] departToMarket;

    [Tooltip("Klick aufs Auto Richtung Farm. Leer = es wird departToMarket verwendet.")]
    [SerializeField] private AudioClip[] departToFarm;

    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.9f;

    [Tooltip("Ausblendzeit, sobald die Zielszene steht. Die Fahrt ist damit vorbei — läuft " +
             "der Motor in der neuen Szene weiter, klingt es, als stünde das Auto neben " +
             "einem.\n\n" +
             "Kurz, aber nicht null: hart abgeschnitten knackt es hörbar.")]
    [SerializeField] private float arrivalFade = 0.4f;

    [Tooltip("Mindestdauer, die der Klang läuft, bevor überhaupt ausgeblendet wird.\n\n" +
             "Ohne das hängt die Länge der Abfahrt daran, wie schnell der Rechner lädt: auf " +
             "einer SSD ist die Szene nach einem Wimpernschlag da und der Motor bricht " +
             "mitten im Anlassen ab. Die Fahrt soll sich nach Fahrt anfühlen, nicht nach " +
             "Ladezeit.\n\n" +
             "0 = sofort ausblenden, sobald die Szene steht.")]
    [SerializeField] private float minPlayTime = 1.2f;

    // Die laufende Abfahrt, damit sie beim Ankommen beendet werden kann.
    private AudioSource travelVoice;

    private float travelStartTime;
    private Coroutine pendingStop;

    private void OnEnable()
    {
        FarmMarketSceneTransition.OnTraveledToMarketStatic += HandleToMarket;
        FarmMarketSceneTransition.OnTraveledToFarmStatic   += HandleToFarm;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        FarmMarketSceneTransition.OnTraveledToMarketStatic -= HandleToMarket;
        FarmMarketSceneTransition.OnTraveledToFarmStatic   -= HandleToFarm;

        SceneManager.sceneLoaded -= OnSceneLoaded;

        StopTravel(0f);
    }

    /// <summary>
    /// Die Zielszene steht — die Fahrt ist zu Ende, egal ob der Clip noch läuft.
    ///
    /// Der Klang soll den Ladebildschirm überbrücken, nicht in die neue Szene hineinragen.
    /// Wie lange das Laden dauert, weiß man vorher nicht; deshalb wird hier abgeblendet
    /// statt die Cliplänge passend zu wählen.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (travelVoice == null) return;

        float played = Time.unscaledTime - travelStartTime;
        float remaining = minPlayTime - played;

        if (remaining <= 0f)
        {
            StopTravel(arrivalFade);
            return;
        }

        // Ungedämpfte Zeit: direkt nach einem Szenenwechsel steht Time.timeScale
        // gelegentlich noch auf 0, und dann liefe der Motor endlos weiter.
        pendingStop = StartCoroutine(StopAfter(remaining));
    }

    private IEnumerator StopAfter(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        pendingStop = null;
        StopTravel(arrivalFade);
    }

    private void StopTravel(float fade)
    {
        if (pendingStop != null)
        {
            StopCoroutine(pendingStop);
            pendingStop = null;
        }

        if (travelVoice == null) return;

        SfxManager.Instance?.StopLoop(travelVoice, fade);
        travelVoice = null;
    }

    private void HandleToMarket() => Play(departToMarket);

    private void HandleToFarm()
        => Play(departToFarm != null && departToFarm.Length > 0 ? departToFarm : departToMarket);

    private void Play(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0 || SfxManager.Instance == null) return;

        // Eine noch laufende Abfahrt zuerst beenden — sonst überlagern sich zwei Motoren,
        // wenn jemand hin- und herfährt, bevor der erste Clip durch ist.
        StopTravel(0.1f);

        // Bewusst 2D: das Auto steht am Kartenrand, und über die 3D-Ausgabe wäre die
        // Abfahrt je nach Kameraposition kaum zu hören. Sie ist außerdem eher eine
        // Bestätigung der Eingabe als ein Geräusch am Ort.
        travelVoice = SfxManager.Instance.PlayUI(clips, volume);
        travelStartTime = Time.unscaledTime;
    }
}

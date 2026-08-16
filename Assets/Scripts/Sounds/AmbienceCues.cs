using System.Collections;
using UnityEngine;

/// <summary>
/// Sagt dem <see cref="AmbienceDirector"/>, welche Umgebung diese Szene hat — und streut
/// einzelne Geräusche darüber.
///
/// <b>Warum beides.</b> Ein Dauerloop allein klingt nach Dauerloop: das Ohr findet die
/// Nahtstelle nach zwei, drei Durchläufen und hört ab dann nur noch die Wiederholung.
/// Einzelne Klänge in unregelmäßigen Abständen verhindern genau das — sie geben dem Ohr
/// etwas Neues, bevor es anfängt, das Muster zu suchen.
///
/// Setup: leeres GameObject pro Szene, Komponente drauf.
/// </summary>
public class AmbienceCues : MonoBehaviour
{
    [Header("Bett")]
    [Tooltip("Dauerhafte Ebenen dieser Szene. Ebenen, die auch die vorige Szene hatte, " +
             "laufen beim Wechsel unterbrechungsfrei weiter.")]
    [SerializeField] private AmbienceTrack[] layers;

    [Tooltip("Blendzeit beim Betreten der Szene. -1 = Standardwert des Directors.")]
    [SerializeField] private float fade = -1f;

    [Header("Streuer")]
    [Tooltip("Einzelne Geräusche, die in zufälligen Abständen kommen: Vogel, Knarzen, " +
             "entfernter Hund.\n\n" +
             "Diese Clips machen den Unterschied zwischen 'da läuft eine Datei' und " +
             "'der Ort lebt'. Lieber vier verschiedene als einen, der oft kommt.")]
    [SerializeField] private AudioClip[] oneShots;

    [Tooltip("Kürzester Abstand zwischen zwei Streuern, in Sekunden.")]
    [SerializeField] private float minInterval = 8f;

    [Tooltip("Längster Abstand. Deutlich über dem Minimum halten — gleichmäßige Abstände " +
             "fallen als Takt auf und wirken dadurch künstlich.")]
    [SerializeField] private float maxInterval = 25f;

    [Range(0f, 1f)]
    [SerializeField] private float oneShotVolume = 0.5f;

    [Header("Verortung der Streuer")]
    [Tooltip("An = der Klang kommt aus einer zufälligen Richtung um die Kamera herum, " +
             "nicht aus dem Nichts. Ein Vogel irgendwo links macht den Raum deutlich " +
             "größer als derselbe Vogel im Kopf des Spielers.")]
    [SerializeField] private bool positionAroundCamera = true;

    [Tooltip("Abstand zur Kamera, in dem die Streuer platziert werden.")]
    [SerializeField] private float minRadius = 8f;

    [SerializeField] private float maxRadius = 25f;

    private Coroutine loop;

    private void Start()
    {
        AmbienceDirector.Instance?.SetLayers(layers, fade);

        if (oneShots != null && oneShots.Length > 0)
            loop = StartCoroutine(OneShotLoop());
    }

    private void OnDisable()
    {
        if (loop == null) return;

        StopCoroutine(loop);
        loop = null;
    }

    private IEnumerator OneShotLoop()
    {
        float min = Mathf.Max(0.5f, minInterval);
        float max = Mathf.Max(min, maxInterval);

        // Nicht sofort loslegen: direkt beim Szenenstart ist der Spieler mit anderem
        // beschäftigt, und ein Vogel in Sekunde null wirkt wie ein Startgeräusch.
        yield return new WaitForSeconds(Random.Range(min, max));

        while (true)
        {
            PlayOneShot();
            yield return new WaitForSeconds(Random.Range(min, max));
        }
    }

    private void PlayOneShot()
    {
        if (SfxManager.Instance == null || oneShots == null || oneShots.Length == 0) return;

        var clip = oneShots[Random.Range(0, oneShots.Length)];
        if (clip == null) return;

        if (!positionAroundCamera || Camera.main == null)
        {
            SfxManager.Instance.PlayUI(clip, oneShotVolume);
            return;
        }

        // Zufällige Richtung in der Ebene, zufälliger Abstand. Die Höhe der Kamera zu
        // übernehmen reicht — bei der isometrischen Ansicht hört man den Unterschied
        // zwischen "über" und "unter" ohnehin nicht.
        Vector2 dir = Random.insideUnitCircle.normalized;
        float radius = Random.Range(minRadius, Mathf.Max(minRadius, maxRadius));

        Vector3 origin = Camera.main.transform.position;
        Vector3 position = origin + new Vector3(dir.x, 0f, dir.y) * radius;

        SfxManager.Instance.Play(clip, position, oneShotVolume);
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hängt einen UI-Slider an einen Lautstärke-Kanal. Auf das Slider-Objekt legen und den
/// Kanal auswählen — mehr ist nicht zu tun, die Verkabelung passiert selbst.
///
/// Der Slider muss auf <c>Min 0</c> / <c>Max 1</c> stehen (Unity-Standard). Die Umrechnung
/// in Dezibel macht <see cref="AudioVolumeSettings"/>.
/// </summary>
[RequireComponent(typeof(Slider))]
public class VolumeSlider : MonoBehaviour
{
    [SerializeField] private AudioChannel channel = AudioChannel.Master;

    [Tooltip("Beim Loslassen speichern statt bei jeder Bewegung. PlayerPrefs schreibt auf " +
             "die Festplatte — das bei jedem Pixel Reglerbewegung zu tun, ruckelt.")]
    [SerializeField] private bool saveOnRelease = true;

    private Slider slider;
    private bool applying;

    private void Awake() => slider = GetComponent<Slider>();

    private void OnEnable()
    {
        var settings = AudioVolumeSettings.Instance;
        if (settings == null)
        {
            Debug.LogWarning($"[VolumeSlider] Kein AudioVolumeSettings in der Szene — " +
                             $"Regler '{name}' bleibt wirkungslos.");
            return;
        }

        // Beim Setzen des Startwerts feuert onValueChanged mit. Ohne die Sperre würde der
        // Regler seinen eigenen gespeicherten Wert sofort wieder zurückschreiben.
        applying = true;
        slider.SetValueWithoutNotify(settings.Get(channel));
        applying = false;

        slider.onValueChanged.AddListener(HandleChanged);
    }

    private void OnDisable()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(HandleChanged);

        if (saveOnRelease) AudioVolumeSettings.Instance?.Save();
    }

    private void HandleChanged(float value)
    {
        if (applying) return;

        AudioVolumeSettings.Instance?.Set(channel, value);

        if (!saveOnRelease) AudioVolumeSettings.Instance?.Save();
    }
}

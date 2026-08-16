using UnityEngine;

/// <summary>
/// Spielt die frucht-eigenen Klänge. Die Clips liegen am jeweiligen <see cref="PlantType"/>,
/// nicht hier — eine neue Frucht braucht damit kein Anfassen dieser Klasse.
///
/// <b>Zusätzlich zum Werkzeug, nicht statt dessen.</b> Die Sichel klingt immer gleich, denn
/// die Bewegung ist dieselbe; was sich unterscheidet, ist das Geerntete. Beide Ebenen
/// übereinander ergeben aus wenigen Clips viele Kombinationen — deshalb ist
/// <see cref="PlantType.sfxVolume"/> auch bewusst niedriger vorbelegt.
///
/// Die Ernte-Events feuern <b>pro Tile</b>: bei 3x3-AoE also neunmal im selben Frame. Dass
/// daraus trotzdem ein Klang wird, erledigt der Duplikatschutz im SfxManager, der denselben
/// Clip innerhalb eines Frames nur einmal durchlässt.
///
/// Setup: auf das SfxManager-Prefab legen.
/// </summary>
public class CropSfx : MonoBehaviour
{
    [Tooltip("Aus = nur Werkzeug- und Verkaufsklänge, keine frucht-eigenen.")]
    [SerializeField] private bool enableCropSounds = true;

    [Tooltip("Zusätzlicher Faktor über alle Früchte. Zum schnellen Nachregeln, ohne jedes " +
             "PlantType-Asset einzeln anzufassen.")]
    [Range(0f, 1f)]
    [SerializeField] private float volumeScale = 1f;

    public static CropSfx Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        PlantManager.OnSeedPlanted   += HandlePlanted;
        PlantManager.OnCropHarvested += HandleHarvested;
    }

    private void OnDisable()
    {
        PlantManager.OnSeedPlanted   -= HandlePlanted;
        PlantManager.OnCropHarvested -= HandleHarvested;
    }

    private void HandlePlanted(PlantType plant)   => Play(plant, plant?.plantSfx);
    private void HandleHarvested(PlantType plant) => Play(plant, plant?.harvestSfx);

    /// <summary>
    /// Klang für einen Verkauf. Bewusst <b>nicht</b> an <c>OnCropSoldStatic</c> gehängt:
    /// der Verkaufsklang gehört zum Münzflug, und der beginnt erst, wenn die Münzen
    /// ankommen — nicht schon beim Klick. Der Aufrufer bestimmt also den Zeitpunkt.
    ///
    /// Hat die Frucht keinen eigenen Klang, übernimmt der allgemeine aus der UiSfxLibrary.
    /// </summary>
    public static void PlaySell(PlantType plant)
    {
        var clips = plant != null ? plant.sellSfx : null;

        if (clips != null && clips.Length > 0 && Instance != null)
        {
            Instance.Play(plant, clips);
            return;
        }

        UiSfx.Sell();
    }

    private void Play(PlantType plant, AudioClip[] clips)
    {
        if (!enableCropSounds || plant == null) return;
        if (clips == null || clips.Length == 0) return;
        if (SfxManager.Instance == null) return;

        // 2D: die Events liefern nur die Fruchtart, keine Position. Der Ort der Aktion
        // steckt ohnehin schon im Werkzeugklang, der von dort kommt — diese Ebene sagt
        // nur, worum es sich handelt.
        SfxManager.Instance.PlayUI(clips, plant.sfxVolume * volumeScale);
    }
}

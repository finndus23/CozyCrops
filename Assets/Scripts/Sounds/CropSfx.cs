using System.Collections;
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

    [Header("Fallback (falls die Frucht keinen eigenen Sound hat)")]
    [Tooltip("Wird gespielt, wenn PlantType.plantSfx leer ist — z.B. für neue Früchte, die " +
             "noch keine eigenen Klänge haben. So bleibt das Säen nie stumm.")]
    [SerializeField] private AudioClip[] fallbackPlantSfx;

    [Tooltip("Wird gespielt, wenn PlantType.harvestSfx leer ist.")]
    [SerializeField] private AudioClip[] fallbackHarvestSfx;

    [Header("AoE-Schwarm")]
    [Tooltip("Zeitversatz zwischen den Klängen, wenn mehrere Tiles im selben Frame " +
             "abgeerntet/besät werden (2x2, 3x3, 4x4 …).\n\n" +
             "0 = alle exakt gleichzeitig — dann blockt der frame-basierte Duplikatschutz " +
             "im SfxManager die meisten davon sogar komplett (gegen Phasing gedacht), aus " +
             "9 Ernte-Klängen würde also nur einer. Mit Versatz spielt jedes Tile seinen " +
             "eigenen Ton als kurzes Prasseln — dasselbe Prinzip wie beim Münzregen.")]
    [Min(0f)]
    [SerializeField] private float burstStagger = 0.045f;

    private int harvestBurstIndex;
    private int harvestBurstFrame = -1;
    private int plantBurstIndex;
    private int plantBurstFrame = -1;

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

    private void HandlePlanted(PlantType plant)
    {
        int index = NextBurstIndex(ref plantBurstFrame, ref plantBurstIndex);
        PlayBurst(plant, ResolveClips(plant?.plantSfx, fallbackPlantSfx), index);
    }

    private void HandleHarvested(PlantType plant)
    {
        int index = NextBurstIndex(ref harvestBurstFrame, ref harvestBurstIndex);
        PlayBurst(plant, ResolveClips(plant?.harvestSfx, fallbackHarvestSfx), index);
    }

    /// <summary>Frucht-eigener Sound, falls vorhanden — sonst der generische Fallback.</summary>
    private static AudioClip[] ResolveClips(AudioClip[] specific, AudioClip[] fallback)
        => (specific != null && specific.Length > 0) ? specific : fallback;

    /// <summary>
    /// Wievielter Aufruf dieser Art (Säen bzw. Ernten getrennt gezählt) ist das im
    /// aktuellen Frame? Bei einer AoE-Aktion feuert das zugehörige Event einmal pro Tile,
    /// alle im selben Frame — der Zähler beginnt bei jedem neuen Frame wieder bei 0.
    /// </summary>
    private static int NextBurstIndex(ref int lastFrame, ref int counter)
    {
        int frame = Time.frameCount;
        if (frame != lastFrame)
        {
            lastFrame = frame;
            counter = 0;
        }

        return counter++;
    }

    /// <summary>
    /// Erstes Tile eines Schwungs klingt sofort (bleibt bei Einzelaktionen genauso
    /// knackig wie vorher), jedes weitere folgt leicht versetzt — das Prasseln.
    /// </summary>
    private void PlayBurst(PlantType plant, AudioClip[] clips, int burstIndex)
    {
        if (!enableCropSounds || plant == null || clips == null || clips.Length == 0)
            return;

        if (burstIndex <= 0 || burstStagger <= 0f)
        {
            Play(plant, clips);
            return;
        }

        StartCoroutine(PlayDelayed(plant, clips, burstIndex * burstStagger));
    }

    private IEnumerator PlayDelayed(PlantType plant, AudioClip[] clips, float delay)
    {
        yield return new WaitForSeconds(delay);
        Play(plant, clips);
    }

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

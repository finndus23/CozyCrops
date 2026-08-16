using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Vertont den Baumodus: Umschalten, Zellen markieren, Tiles umwandeln.
///
/// Hängt ausschließlich an statischen Events. Dadurch ist die Reihenfolge der Awake-Aufrufe
/// egal und die Komponente kann auf dem dauerhaften SfxManager-Objekt liegen, statt in jeder
/// Szene neu platziert zu werden.
///
/// Setup: auf das SfxManager-Prefab legen, Clips zuweisen.
/// </summary>
public class BuildSfx : MonoBehaviour
{
    [Header("Baumodus")]
    [Tooltip("Baumodus betreten (B). Kurzer Modus-Wechsel-Klang, kein Fanfarengetöse.")]
    [SerializeField] private AudioClip[] enterBuildMode;

    [SerializeField] private AudioClip[] exitBuildMode;

    [Tooltip("Andere Tile-Art gewählt. Optional — schnell hintereinander gedrückt wird das " +
             "sonst schnell nervig.")]
    [SerializeField] private AudioClip[] selectTileType;

    [Header("Auswahl")]
    [Tooltip("Zelle markiert. Wird beim Ziehen pro Zelle einmal gespielt — kurz und leise " +
             "halten, das ist ein Ticken, kein Ereignis.")]
    [SerializeField] private AudioClip[] cellSelected;

    [Tooltip("Zelle wieder abgewählt. Leer = es wird cellSelected mit tieferer Tonhöhe " +
             "gespielt, das reicht als Unterscheidung völlig.")]
    [SerializeField] private AudioClip[] cellDeselected;

    [Header("Umwandeln — Impact je Tile-Art")]
    [Tooltip("Zu Ackerland umgewandelt. Erde, Umgraben.")]
    [SerializeField] private AudioClip[] impactFarmPlot;

    [Tooltip("Zu Weg umgewandelt. Kies, Stein, Holz — je nachdem wie euer Pfad aussieht.")]
    [SerializeField] private AudioClip[] impactPath;

    [Tooltip("Zurück zu Gras (Abriss). Weicher als die anderen beiden — es wird ja etwas " +
             "weggenommen, nicht gebaut.")]
    [SerializeField] private AudioClip[] impactGrass;

    [Header("Lautstärke")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.7f;

    [Tooltip("Zusätzliche Lautstärke bei großen Flächen. Zwanzig Felder auf einmal " +
             "umzuwandeln ist ein größeres Ereignis als eines — das darf man hören, " +
             "ohne dass zwanzig Clips übereinanderliegen.")]
    [Range(0f, 1f)]
    [SerializeField] private float areaBoost = 0.3f;

    [Tooltip("Ab dieser Feldzahl ist der Zuschlag voll ausgereizt.")]
    [SerializeField] private int areaBoostFullAt = 12;

    [Tooltip("Tonhöhe für das Abwählen, wenn kein eigener Clip hinterlegt ist.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float deselectPitch = 0.85f;

    private void OnEnable()
    {
        BuildModeManager.OnBuildModeEnteredStatic += HandleEnter;
        BuildModeManager.OnBuildModeExitedStatic  += HandleExit;

        SelectionManager.OnCellSelectionChangedStatic += HandleCellChanged;
        GridManager.OnTilesAppliedStatic              += HandleTilesApplied;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        BuildModeManager.OnBuildModeEnteredStatic -= HandleEnter;
        BuildModeManager.OnBuildModeExitedStatic  -= HandleExit;

        SelectionManager.OnCellSelectionChangedStatic -= HandleCellChanged;
        GridManager.OnTilesAppliedStatic              -= HandleTilesApplied;
    }

    private void Start() => BindSceneSystems();

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BindSceneSystems();

    /// <summary>
    /// Der BuildModeManager ist szenengebunden, diese Komponente überlebt die Szene.
    /// Nach jedem Wechsel muss also am neuen Manager neu abonniert werden — erst abmelden,
    /// damit es bei einem überlebenden Manager nicht doppelt klingt.
    /// </summary>
    private void BindSceneSystems()
    {
        var build = BuildModeManager.Instance;
        if (build == null) return;

        build.OnSelectedTileChanged -= HandleTileTypeChanged;
        build.OnSelectedTileChanged += HandleTileTypeChanged;
    }

    // ── Handler ───────────────────────────────────────────────────────────────

    private void HandleEnter() => PlayUI(enterBuildMode);
    private void HandleExit()  => PlayUI(exitBuildMode);

    private void HandleTileTypeChanged(TileType type) => PlayUI(selectTileType);

    private void HandleCellChanged(Vector2Int cell, bool selected)
    {
        if (SfxManager.Instance == null) return;

        var position = GridManager.Instance != null
            ? GridManager.Instance.GridToWorld(cell.x, cell.y)
            : Vector3.zero;

        if (selected)
        {
            Play(cellSelected, position, volume);
            return;
        }

        // Kein eigener Abwähl-Clip: denselben Klang tiefer spielen. Das liest sich als
        // "rückgängig", ohne dass man dafür einen zweiten Sound suchen muss.
        if (cellDeselected != null && cellDeselected.Length > 0)
        {
            Play(cellDeselected, position, volume);
            return;
        }

        PlayPitched(cellSelected, position, volume, deselectPitch);
    }

    private void HandleTilesApplied(TileType type, Vector3 center, int count)
    {
        var clips = ImpactFor(type);
        if (clips == null || clips.Length == 0) return;

        // Lautstärke wächst mit der Fläche, aber gedeckelt — sonst wird ein großer
        // Bauauftrag zum Knall.
        float t = areaBoostFullAt > 1
            ? Mathf.Clamp01((count - 1f) / (areaBoostFullAt - 1f))
            : 0f;

        Play(clips, center, Mathf.Clamp01(volume + areaBoost * t));
    }

    /// <summary>
    /// Der Impact gehört zur Art, in die umgewandelt wird — nicht zur Aktion. Ackerland
    /// klingt nach Erde, egal ob vorher Gras oder Weg dort war, und ein Abriss klingt nach
    /// Gras. Damit braucht es drei Sätze statt neun für alle Übergänge.
    /// </summary>
    private AudioClip[] ImpactFor(TileType type) => type switch
    {
        TileType.FarmPlot => impactFarmPlot,
        TileType.Path     => impactPath,
        TileType.Grass    => impactGrass,
        _                 => null
    };

    // ── Wiedergabe ────────────────────────────────────────────────────────────

    private void Play(AudioClip[] clips, Vector3 position, float vol)
    {
        if (clips == null || clips.Length == 0 || SfxManager.Instance == null) return;
        SfxManager.Instance.Play(clips, position, vol);
    }

    private void PlayUI(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0 || SfxManager.Instance == null) return;
        SfxManager.Instance.PlayUI(clips, volume);
    }

    private void PlayPitched(AudioClip[] clips, Vector3 position, float vol, float pitch)
    {
        if (clips == null || clips.Length == 0 || SfxManager.Instance == null) return;
        SfxManager.Instance.Play(clips, position, vol, pitch);
    }
}

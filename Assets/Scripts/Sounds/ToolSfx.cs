using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Vertont die Werkzeug-Aktionen. Hängt an den Job-Events des <see cref="ToolUseHandler"/>,
/// die Clips liegen auf dem jeweiligen <see cref="ToolData"/>.
///
/// <b>Ein Sound pro Job, nicht pro Tile</b> — das ist die eigentliche Entscheidung hier.
/// Naheliegend wäre <c>PlantManager.OnFieldTilled</c> gewesen, dort hängt schließlich auch
/// das Missions-System. Diese Events feuern aber <b>pro Tile</b>: bei 3x3-AoE also neunmal
/// im selben Frame. Neun identische Clips gleichzeitig sind nicht neunmal so schön, sondern
/// klingen durch Phasing kaputt und springen um rund 19 dB nach oben — und bei drei parallel
/// laufenden Werkzeugen kommt das dreifach.
///
/// <c>OnJobStarted</c>/<c>OnJobFinished</c> feuern dagegen genau einmal pro Klick, egal wie
/// viele Tiles dranhängen. Damit ist das Problem gelöst, statt es nachträglich mit
/// Cooldowns zuzukleistern.
///
/// Setup: irgendwo in der Farm-Szene auf ein GameObject legen (z.B. neben den
/// ToolUseHandler). Ohne zugewiesene Clips passiert schlicht nichts.
/// </summary>
public class ToolSfx : MonoBehaviour
{
    [Tooltip("Aus = nur der Treffer am Ende, kein Dauerton während der Aktion.")]
    [SerializeField] private bool playLoops = true;

    // Pro Job der laufende Dauerton. Dictionary statt eines einzelnen Feldes, weil
    // seit dem Umbau auf parallele Warteschlangen mehrere Jobs gleichzeitig laufen —
    // ein einzelner Handle würde beim zweiten Werkzeug den ersten Ton abwürgen.
    private readonly Dictionary<ToolJob, AudioSource> activeLoops = new();

    private ToolUseHandler handler;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Bind();
    }

    // Nachziehen, falls OnEnable vor dem Awake des Handlers lief.
    private void Start() => Bind();

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Der alte Handler ist mit seiner Szene verschwunden; die Referenz zeigt ins Leere.
        // Ohne das Zurücksetzen würde Bind() sie für gültig halten und nie neu abonnieren.
        handler = null;

        // Laufende Dauertöne gehören zu Jobs, die es nicht mehr gibt.
        StopAllLoops();

        Bind();
    }

    /// <summary>
    /// Abonniert den ToolUseHandler der aktuellen Szene.
    ///
    /// Diese Komponente liegt auf dem dauerhaften SfxManager-Objekt, der Handler ist
    /// dagegen szenengebunden. Einmalig in Start zu binden hieße: startet das Spiel im
    /// Hauptmenü, wo es keinen Handler gibt, bleiben die Werkzeuge die ganze Sitzung stumm.
    /// </summary>
    private void Bind()
    {
        if (handler != null) return;

        handler = ToolUseHandler.Instance;
        if (handler == null) return;

        handler.OnJobStarted  -= HandleJobStarted;
        handler.OnJobFinished -= HandleJobFinished;

        handler.OnJobStarted  += HandleJobStarted;
        handler.OnJobFinished += HandleJobFinished;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (handler != null)
        {
            handler.OnJobStarted  -= HandleJobStarted;
            handler.OnJobFinished -= HandleJobFinished;
        }

        StopAllLoops();
    }

    // ── Events ────────────────────────────────────────────────────────────────

    private void HandleJobStarted(ToolJob job)
    {
        if (!playLoops || job == null) return;

        var data = ToolRegistry.Instance?.GetData(job.Tool);
        if (data == null || data.useLoop == null) return;
        if (SfxManager.Instance == null) return;

        var voice = SfxManager.Instance.PlayLoop(data.useLoop, JobCenter(job), data.sfxVolume);
        if (voice != null) activeLoops[job] = voice;
    }

    private void HandleJobFinished(ToolJob job)
    {
        if (job == null) return;

        if (activeLoops.TryGetValue(job, out var voice))
        {
            SfxManager.Instance?.StopLoop(voice);
            activeLoops.Remove(job);
        }

        // Abgebrochene Jobs kriegen keinen Treffer-Sound: der Spieler hat die
        // Warteschlange geleert (Q) oder die Tile war nicht mehr gültig — da soll
        // es nicht klingen, als hätte die Aktion stattgefunden.
        if (job.State != ToolJobState.Finished) return;

        var data = ToolRegistry.Instance?.GetData(job.Tool);
        if (data == null || data.impactClips == null || data.impactClips.Length == 0) return;

        SfxManager.Instance?.Play(data.impactClips, JobCenter(job), data.sfxVolume);
    }

    // ── Hilfen ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mittelpunkt der bearbeiteten Fläche. Bei AoE käme der Ton sonst aus der Ecke,
    /// in die der Spieler zufällig geklickt hat, statt aus der Mitte der Aktion.
    /// </summary>
    private static Vector3 JobCenter(ToolJob job)
    {
        var grid = GridManager.Instance;
        if (grid == null || job.Tiles == null || job.Tiles.Count == 0)
            return Vector3.zero;

        var sum = Vector3.zero;
        foreach (var tile in job.Tiles)
            sum += grid.GridToWorld(tile.x, tile.y);

        return sum / job.Tiles.Count;
    }

    private void StopAllLoops()
    {
        foreach (var kvp in activeLoops)
            SfxManager.Instance?.StopLoop(kvp.Value, 0f);

        activeLoops.Clear();
    }
}

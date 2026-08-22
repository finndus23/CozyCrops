using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Hält die HighlightMask-RenderTexture der separaten Mask-Kamera synchron
/// zur Fenster-/Bildschirmauflösung (eine fest angelegte RT würde bei
/// Fenstergrößenänderung verzerren oder unscharf werden), schiebt die jeweils
/// aktuelle Textur-Referenz ins Composite-Material (eine neu erzeugte
/// RenderTexture ist ja eine neue Objektreferenz) — UND kopiert jeden Frame
/// Position/Rotation/Zoom der Hauptkamera auf diese Mask-Kamera.
///
/// Der letzte Punkt ist entscheidend: die Mask-Kamera muss exakt denselben
/// Bildausschnitt sehen wie die Hauptkamera, sonst landet die Silhouette in
/// der Maske an einer anderen Bildschirmposition als das echte Objekt und der
/// Rand "driftet" beim Bewegen/Zoomen der Hauptkamera (CameraController.cs
/// ändert transform.position und orthographicSize laufend über Pan/Zoom/
/// Clipping-Retreat).
///
/// Liegt auf der Mask-Kamera (siehe Setup-Anleitung).
/// </summary>
[RequireComponent(typeof(Camera))]
public class HighlightMaskCameraSync : MonoBehaviour
{
    [Tooltip("Kamera, deren Blickpunkt kopiert wird.\n\n" +
             "Kann leer bleiben — es wird ohnehin jeden Frame automatisch die Kamera " +
             "genommen, die gerade wirklich aufs Bild rendert. Wird eine gesetzte Kamera " +
             "abgeschaltet (z.B. die Hauptkamera beim Öffnen des Shops), springt die Maske " +
             "selbstständig auf die dann aktive Kamera um.")]
    [SerializeField] private Camera sourceCamera;
    [SerializeField] private Material outlineCompositeMaterial;
    [SerializeField] private string texturePropertyName = "_HighlightMask";
    [SerializeField, Range(0.25f, 1f)] private float resolutionScale = 1f;

    [Tooltip("Index des Renderers aus der Renderer-Liste des URP-Assets, den diese Kamera " +
             "benutzen soll.\n\n" +
             "MUSS ein Renderer OHNE die Full-Screen-Outline-Feature sein! Läuft die Feature " +
             "auch auf dieser Kamera, malt sie die Kontur zurück in die Maske hinein — die " +
             "gilt im nächsten Frame selbst als Maske und die Fläche wächst pro Frame weiter, " +
             "bis der halbe Bildschirm eingefärbt ist.\n\n" +
             "-1 = nicht per Script setzen (dann im Camera-Inspector unter Rendering > Renderer wählen).")]
    [SerializeField] private int maskRendererIndex = -1;

    private Camera cam;
    private RenderTexture rt;
    private int lastWidth, lastHeight;

    private Camera[] cameraBuffer = new Camera[8];
    private int highlightLayerMask = -1;

    void Awake()
    {
        cam = GetComponent<Camera>();
        ConfigureMaskCamera();
    }

    void OnEnable()
    {
        ConfigureMaskCamera();
        Rebuild();
    }

    /// <summary>
    /// Erzwingt die Einstellungen, die diese Kamera zwingend braucht, damit die Maske
    /// wirklich eine saubere Silhouette ist. Bewusst im Code statt nur im Inspector:
    /// eine frisch angelegte Unity-Kamera steht per Default auf Skybox-Hintergrund,
    /// und genau das macht die Maske unbrauchbar (siehe unten).
    /// </summary>
    private void ConfigureMaskCamera()
    {
        if (cam == null) cam = GetComponent<Camera>();

        // Schwarz löschen statt Skybox. Mit Skybox landet der Himmel-Farbverlauf im
        // Rot-Kanal der Maske — die Kantenerkennung im Composite-Shader findet dann
        // im ganzen Bild Helligkeitssprünge und zeichnet einen breiten, diffusen
        // "Glow" statt einer Kontur. Weil die Skybox beim Kameraschwenk mitwandert,
        // wabert dieser Fehler zusätzlich bei jeder Bewegung.
        cam.clearFlags = CameraClearFlags.SolidColor;

        // Die Maske speichert Abstände in Metern, 0 heißt "hier ist nichts". Echte
        // Geometrie liegt nie näher als die Near-Plane, also kann 0 nicht mit einem
        // gültigen Wert kollidieren.
        //
        // Vorher hing der Clear-Wert an SystemInfo.usesReversedZBuffer, weil Rohtiefen
        // gespeichert wurden. Genau diese Konventions-Abhängigkeit war die Fehlerquelle:
        // Maske und _CameraDepthTexture liefen gegenläufig, und der Vergleich ergab
        // immer "sichtbar". In Metern gibt es das Problem nicht mehr.
        cam.backgroundColor = Color.clear;

        // Nichts von dem hier gehört in eine reine Datenmaske.
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.useOcclusionCulling = false;

        var data = cam.GetUniversalAdditionalCameraData();
        if (data != null)
        {
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = false;

            // Renderer ohne die Outline-Feature erzwingen — sonst Rückkopplung
            // (siehe Tooltip an maskRendererIndex).
            if (maskRendererIndex >= 0)
                data.SetRenderer(maskRendererIndex);
        }
    }

    void LateUpdate()
    {
        // LateUpdate statt Update: CameraController bewegt/zoomt die Hauptkamera in
        // Update(). Unity garantiert, dass ALLE Update()-Aufrufe der Szene vor
        // JEDEM LateUpdate()-Aufruf laufen — hier ist die Hauptkamera also für
        // diesen Frame bereits an ihrer finalen Position, kein 1-Frame-Versatz.
        int w = Mathf.Max(1, Mathf.RoundToInt(Screen.width * resolutionScale));
        int h = Mathf.Max(1, Mathf.RoundToInt(Screen.height * resolutionScale));
        if (w != lastWidth || h != lastHeight) Rebuild();

        ScanCameras();
        SyncToSourceCamera();
    }

    /// <summary>
    /// Geht einmal pro Frame alle aktiven Kameras durch und erledigt dabei zwei Dinge:
    ///
    /// 1. <b>Nimmt jeder fremden Kamera den Highlight-Layer weg.</b> Sieht eine normale
    ///    Kamera den Layer, rendert sie die weiße Masken-Kopie direkt über das Objekt —
    ///    der NPC wirkt dann grau. Die Dialogue-/Shop-Kamera steht per Default auf
    ///    "Everything" und lief deshalb genau in dieses Problem.
    ///
    /// 2. <b>Sucht die Kamera, die gerade wirklich aufs Bild rendert.</b> Der Shop
    ///    schaltet die Hauptkamera ab (FarmMarketDialogueShopController: gameplayCamera
    ///    .enabled = false) und die Dialogue-Kamera an. Camera.main ist dann null und
    ///    ein fest gemerkter Verweis zeigt auf eine abgeschaltete Kamera — die Maske
    ///    bliebe am alten Ort stehen und die Kontur säße im Shop völlig woanders.
    ///
    /// Jeden Frame statt einmalig, weil Kameras jederzeit an- und abgeschaltet werden.
    /// Der Aufwand ist eine Handvoll Int-Vergleiche.
    /// </summary>
    private void ScanCameras()
    {
        if (highlightLayerMask < 0)
        {
            int layer = LayerMask.NameToLayer(HighlightOutline.HighlightLayerName);
            if (layer < 0) return;
            highlightLayerMask = 1 << layer;
        }

        int count = Camera.allCamerasCount;
        if (cameraBuffer.Length < count) cameraBuffer = new Camera[count];
        Camera.GetAllCameras(cameraBuffer);

        Camera best = null;
        for (int i = 0; i < count; i++)
        {
            var other = cameraBuffer[i];
            if (other == null || other == cam) continue;

            // Fremde Kamera darf den Masken-Layer nicht sehen.
            if ((other.cullingMask & highlightLayerMask) != 0)
                other.cullingMask &= ~highlightLayerMask;

            // Kameras die selbst in eine Textur rendern zeigen nicht das Spielbild.
            if (other.targetTexture != null) continue;

            // Höchste Tiefe gewinnt — das ist die, die zuletzt aufs Bild zeichnet.
            if (best == null || other.depth > best.depth) best = other;
        }

        // Ein explizit gesetztes sourceCamera-Feld hat Vorrang, solange die Kamera
        // auch wirklich läuft. Sobald sie abgeschaltet wird (Shop), übernimmt die
        // gefundene aktive Kamera.
        if (sourceCamera == null || !sourceCamera.isActiveAndEnabled)
            sourceCamera = best;
    }

    void OnDisable()
    {
        if (rt == null) return;
        cam.targetTexture = null;
        rt.Release();
        rt = null;
    }

    private void SyncToSourceCamera()
    {
        // ScanCameras() hat die aktive Kamera bereits ermittelt.
        if (sourceCamera == null) return;

        transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);

        cam.orthographic = sourceCamera.orthographic;
        if (sourceCamera.orthographic)
            cam.orthographicSize = sourceCamera.orthographicSize;
        else
            cam.fieldOfView = sourceCamera.fieldOfView;

        cam.nearClipPlane = sourceCamera.nearClipPlane;
        cam.farClipPlane = sourceCamera.farClipPlane;

        // Muss VOR der Hauptkamera rendern. Sonst liest deren Composite-Pass die
        // Maske vom vorherigen Frame und die Kontur hängt bei jeder Kamerabewegung
        // sichtbar hinterher.
        cam.depth = sourceCamera.depth - 1f;
    }

    private void Rebuild()
    {
        lastWidth  = Mathf.Max(1, Mathf.RoundToInt(Screen.width * resolutionScale));
        lastHeight = Mathf.Max(1, Mathf.RoundToInt(Screen.height * resolutionScale));

        if (rt != null)
        {
            cam.targetTexture = null;
            rt.Release();
        }

        // RFloat statt R8: die Maske speichert seit der Verdeckungs-Prüfung den
        // Tiefenwert des Objekts, nicht mehr nur 0/1. In 8 Bit wären das 256 Stufen
        // über die ganze Sichtweite — viel zu grob für einen Tiefenvergleich.
        rt = new RenderTexture(lastWidth, lastHeight, 16, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
        {
            name = "HighlightMaskRT",
            // Point statt Bilinear: Tiefenwerte darf man nicht interpolieren. Zwischen
            // Objekt und Hintergrund käme sonst eine Mischtiefe heraus, die es nirgends
            // gibt, und der Rand würde je nach Hintergrund mal als verdeckt gelten.
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        cam.targetTexture = rt;

        if (outlineCompositeMaterial != null)
            outlineCompositeMaterial.SetTexture(texturePropertyName, rt);
    }
}

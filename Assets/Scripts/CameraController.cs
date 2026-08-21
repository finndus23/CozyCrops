using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minZoom = 3f;
    [SerializeField] private float maxZoom = 20f;
    [SerializeField] private float dragSpeed = 1f;
    [SerializeField] private float rotateSpeed = 0.3f;
    [SerializeField] private string xAxisOnlySceneName = "Marketplace";

    [Header("Farm-Kameragrenzen")]
    [Tooltip("Begrenzt den Mittelpunkt der Kamera auf die Farm. Die große Landschaft bleibt Kulisse.")]
    [SerializeField] private bool limitFarmCameraMovement = true;
    [Tooltip("Wie weit der Kameramittelpunkt über die vollständig ausgebaute Farm hinaus darf.")]
    [Min(0f)]
    [SerializeField] private float farmCameraPadding = 24f;

    [Header("Marktplatz-Kameragrenzen")]
    [Tooltip("Begrenzt die Kamera in der Marktplatz-Szene (nur X-Achse, Z ist ohnehin gesperrt).\n" +
             "Anders als bei der Farm gibt es hier kein GridManager-Raster als Referenz — " +
             "die Werte unten also im Play Mode austesten: an den gewünschten linken/rechten " +
             "Rand fahren, transform.position.x im Inspector ablesen, hier eintragen.")]
    [SerializeField] private bool limitMarketplaceCameraMovement = true;
    [Tooltip("Kleinster erlaubter X-Wert für den Punkt, auf den die Bildschirmmitte zeigt.")]
    [SerializeField] private float marketplaceMinX = -20f;
    [Tooltip("Größter erlaubter X-Wert für den Punkt, auf den die Bildschirmmitte zeigt.")]
    [SerializeField] private float marketplaceMaxX = 40f;

    [Header("Kamera-Clipping")]
    [Tooltip("Schiebt eine orthografische Kamera beim Herauszoomen entlang ihrer Blickachse " +
             "zurueck. Dadurch geraten Boden und niedrige Objekte am unteren Bildrand nicht " +
             "vor die Near-Clipping-Plane.")]
    [SerializeField] private bool preventNearPlaneClipping = true;
    [Tooltip("Y-Hoehe der Bodenebene, die bis zum unteren Bildrand sichtbar bleiben soll.")]
    [SerializeField] private float groundPlaneHeight = 0f;
    [Tooltip("Zusaetzlicher Abstand vor der Near-Clipping-Plane. Deckt Pflanzen, Zaun und " +
             "andere niedrige Aufbauten auf dem Boden ab.")]
    [Min(0f)]
    [SerializeField] private float nearClipPadding = 5f;

    [Header("Schatten")]
    [Tooltip("Koppelt die Shadow Distance an den Zoom. Ein fester Wert kann nicht beides: " +
             "nah gestochen scharf UND weit rausgezoomt vollständig.")]
    [SerializeField] private bool zoomDrivenShadowDistance = true;
    [Tooltip("Shadow Distance = orthographicSize * dieser Faktor + Offset.")]
    [SerializeField] private float shadowDistancePerZoom = 4f;
    [SerializeField] private float shadowDistanceOffset = 10f;
    [SerializeField] private float minShadowDistance = 25f;
    [SerializeField] private float maxShadowDistance = 120f;

    private Camera cam;
    private float lockedMarketplaceZ;
    private float appliedClippingRetreat;

    private UniversalRenderPipelineAsset urpAsset;
    private float originalShadowDistance;
    private float lastAppliedShadowDistance = -1f;

    void Start()
    {
        cam = GetComponent<Camera>();
        ApplyNearPlaneProtection();
        lockedMarketplaceZ = transform.position.z;

        urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset != null)
            originalShadowDistance = urpAsset.shadowDistance;

        ApplyShadowDistance();
    }

    void OnDisable()
    {
        // Das URP-Asset ist ein Projekt-Asset, kein Szenen-Objekt. Ohne Zurücksetzen würde
        // der zuletzt im Playmode gesetzte Wert im Editor hängenbleiben und ins Asset wandern.
        if (urpAsset != null)
            urpAsset.shadowDistance = originalShadowDistance;
    }

    void Update()
    {
        HandleMovement();
        HandleZoom();
        HandleDrag();
        HandleRotate();
        ApplyNearPlaneProtection();
        ClampFarmCameraToBounds();
        ClampMarketplaceCameraToBounds();
        ApplyShadowDistance();
    }

    /// <summary>
    /// Bei einer orthografischen Kamera vergroessert Zoom nur den Ausschnitt, nicht den
    /// Abstand zur Welt. Ab einer gewissen Orthographic Size liegt der untere Rand des
    /// Sichtvolumens deshalb hinter der Kamera und wird von der Near Plane abgeschnitten.
    ///
    /// Entlang der Blickachse zurueckzugehen veraendert bei orthografischer Projektion
    /// nicht den Bildausschnitt. Es schafft lediglich genug Tiefe vor der Kamera. Der
    /// gemerkte Offset erlaubt, beim Hineinzoomen wieder an die Ausgangsposition zu gehen.
    /// </summary>
    private void ApplyNearPlaneProtection()
    {
        if (!preventNearPlaneClipping || cam == null || !cam.orthographic)
        {
            RemoveClippingRetreat();
            return;
        }

        Vector3 forward = transform.forward;
        if (forward.y >= -0.001f)
        {
            RemoveClippingRetreat();
            return; // Keine sinnvolle Boden-Schnittberechnung, wenn die Kamera nicht nach unten blickt.
        }

        // Position ohne den von dieser Methode zuletzt hinzugefuegten Tiefen-Offset.
        Vector3 basePosition = transform.position + forward * appliedClippingRetreat;

        float halfWidth = cam.orthographicSize * cam.aspect;
        float lowestFrustumY = basePosition.y
                               - Mathf.Abs(transform.up.y) * cam.orthographicSize
                               - Mathf.Abs(transform.right.y) * halfWidth;

        // Tiefe, in der die Bodenebene den untersten Viewport-Strahl schneidet.
        float groundDepthAtBottom = (groundPlaneHeight - lowestFrustumY) / forward.y;
        float minimumDepth = cam.nearClipPlane + Mathf.Max(0f, nearClipPadding);
        float requiredRetreat = Mathf.Max(0f, minimumDepth - groundDepthAtBottom);

        transform.position = basePosition - forward * requiredRetreat;
        appliedClippingRetreat = requiredRetreat;
    }

    private void RemoveClippingRetreat()
    {
        if (Mathf.Approximately(appliedClippingRetreat, 0f))
            return;

        transform.position += transform.forward * appliedClippingRetreat;
        appliedClippingRetreat = 0f;
    }

    /// <summary>
    /// Schatten-Reichweite folgt dem Zoom.
    ///
    /// Hintergrund: Die Shadow Distance stand fest auf 30 Units — berechnet für
    /// orthographicSize 5. Der Zoom geht aber bis 20, und bei size 20 sieht man entlang
    /// der Blickachse gut 55+ Units Boden. Alles dahinter verlor die Schatten mitten im
    /// Bild, mit sichtbarer Kante.
    ///
    /// Ein fester hoher Wert ist aber auch keine Lösung: die Shadowmap wird über die
    /// gesamte Distanz verteilt, weit heißt also überall matschig. Deshalb mitwachsen
    /// lassen — nah bleibt die Texel-Dichte hoch, weit sind die Schatten vollständig.
    /// </summary>
    private void ApplyShadowDistance()
    {
        if (!zoomDrivenShadowDistance || urpAsset == null || cam == null) return;

        float target = Mathf.Clamp(
            cam.orthographicSize * shadowDistancePerZoom + shadowDistanceOffset,
            minShadowDistance,
            maxShadowDistance);

        // Nur bei echter Änderung schreiben — das URP-Asset ist ein ScriptableObject,
        // jedes Set markiert es als dirty.
        if (Mathf.Abs(target - lastAppliedShadowDistance) < 0.5f) return;

        lastAppliedShadowDistance = target;
        urpAsset.shadowDistance = target;
    }

    void HandleMovement()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector2 input = Vector2.zero;
        if (keyboard.wKey.isPressed) input.y += 1;
        if (keyboard.sKey.isPressed) input.y -= 1;
        if (keyboard.aKey.isPressed) input.x -= 1;
        if (keyboard.dKey.isPressed) input.x += 1;

        Vector3 forward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
        Vector3 right = new Vector3(transform.right.x, 0, transform.right.z).normalized;

        Vector3 direction = IsXAxisOnlyScene()
            ? Vector3.right * input.x
            : forward * input.y + right * input.x;

        transform.position += direction * moveSpeed * Time.deltaTime;
        LockMarketplaceZIfNeeded();
    }

    void HandleZoom()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y * 0.10f;
        cam.orthographicSize -= scroll * zoomSpeed;
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
    }

    void HandleDrag()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.rightButton.isPressed) return;

        Vector2 delta = mouse.delta.ReadValue();
        float worldUnitsPerPixel = cam.orthographicSize * 2f / Screen.height;

        Vector3 forward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
        Vector3 right = new Vector3(transform.right.x, 0, transform.right.z).normalized;

        Vector3 dragDirection = IsXAxisOnlyScene()
            ? Vector3.right * -delta.x
            : right * -delta.x + forward * -delta.y;

        transform.position += dragDirection * worldUnitsPerPixel * dragSpeed;
        LockMarketplaceZIfNeeded();
    }

    void HandleRotate()
    {
        if (IsXAxisOnlyScene()) return;

        var mouse = Mouse.current;
        if (mouse == null || !mouse.middleButton.isPressed) return;

        float deltaX = mouse.delta.ReadValue().x;
        if (Mathf.Approximately(deltaX, 0f)) return;

        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 pivot = ray.GetPoint(distance);
            transform.RotateAround(pivot, Vector3.up, deltaX * rotateSpeed);
        }
    }

    private bool IsXAxisOnlyScene()
    {
        return SceneManager.GetActiveScene().name == xAxisOnlySceneName;
    }

    private void LockMarketplaceZIfNeeded()
    {
        if (!IsXAxisOnlyScene())
            return;

        Vector3 position = transform.position;
        position.z = lockedMarketplaceZ;
        transform.position = position;
    }

    /// <summary>
    /// Begrenzt den Punkt auf der Bodenebene, den die Bildschirmmitte zeigt. Das bleibt
    /// auch bei Rotation, Zoom und dem Clipping-Rueckzug stabiler als rohe X/Z-Grenzen
    /// auf der hoch und schraeg stehenden Kamera selbst.
    /// </summary>
    private void ClampFarmCameraToBounds()
    {
        if (!limitFarmCameraMovement || IsXAxisOnlyScene() || cam == null)
            return;

        GridManager grid = GridManager.Instance;
        if (grid == null)
            return;

        Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, groundPlaneHeight, 0f));
        if (!groundPlane.Raycast(centerRay, out float distance))
            return;

        Vector3 focus = centerRay.GetPoint(distance);
        Vector3 gridOrigin = grid.transform.position;
        float minX = gridOrigin.x + grid.FarmMinX * grid.CellSize - farmCameraPadding;
        float maxX = gridOrigin.x + grid.FarmMaxXExclusive * grid.CellSize + farmCameraPadding;
        float minZ = gridOrigin.z + grid.FarmMinZ * grid.CellSize - farmCameraPadding;
        float maxZ = gridOrigin.z + grid.FarmMaxZExclusive * grid.CellSize + farmCameraPadding;

        Vector3 clampedFocus = new Vector3(
            Mathf.Clamp(focus.x, minX, maxX),
            focus.y,
            Mathf.Clamp(focus.z, minZ, maxZ));

        transform.position += clampedFocus - focus;
    }

    /// <summary>
    /// Gegenstück zu <see cref="ClampFarmCameraToBounds"/> für den Marktplatz. Dort gibt es
    /// kein Grid als Referenz und die Kamera bewegt sich ohnehin nur auf der X-Achse
    /// (<see cref="LockMarketplaceZIfNeeded"/> hält Z fest) — deshalb reicht ein einfaches
    /// X-Clamping über denselben Bildschirmmitte-auf-Bodenebene-Ansatz wie bei der Farm.
    /// </summary>
    private void ClampMarketplaceCameraToBounds()
    {
        if (!limitMarketplaceCameraMovement || !IsXAxisOnlyScene() || cam == null)
            return;

        Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, groundPlaneHeight, 0f));
        if (!groundPlane.Raycast(centerRay, out float distance))
            return;

        Vector3 focus = centerRay.GetPoint(distance);
        float clampedX = Mathf.Clamp(focus.x, marketplaceMinX, marketplaceMaxX);

        transform.position += Vector3.right * (clampedX - focus.x);
    }
}

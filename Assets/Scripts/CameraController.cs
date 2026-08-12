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

    private UniversalRenderPipelineAsset urpAsset;
    private float originalShadowDistance;
    private float lastAppliedShadowDistance = -1f;

    void Start()
    {
        cam = GetComponent<Camera>();
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
        ApplyShadowDistance();
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
}

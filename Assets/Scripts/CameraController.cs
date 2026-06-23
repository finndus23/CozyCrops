using UnityEngine;
using UnityEngine.InputSystem;
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

    private Camera cam;
    private float lockedMarketplaceZ;

    void Start()
    {
        cam = GetComponent<Camera>();
        lockedMarketplaceZ = transform.position.z;
    }

    void Update()
    {
        HandleMovement();
        HandleZoom();
        HandleDrag();
        HandleRotate();
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
        if (BuildModeManager.Instance != null && BuildModeManager.Instance.IsActive) return;

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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Zeigt für hervorgehobene Quest-Ziele, die gerade außerhalb des Bildes liegen, einen
/// Pfeil am Bildschirmrand — sonst leuchtet der NPC zwar, aber der Spieler sieht nichts
/// und weiß nicht, in welche Richtung er fahren soll.
///
/// Gehört auf ein Canvas (Screen Space - Overlay oder Camera). Die Pfeil-Instanzen
/// werden gepoolt, weil sich die Anzahl der Ziele jederzeit ändern kann und
/// Instantiate/Destroy pro Frame Müll erzeugen würde.
/// </summary>
public class OffScreenHighlightIndicator : MonoBehaviour
{
    [Tooltip("Prefab für einen Pfeil. Beliebiges UI-Element mit RectTransform — " +
             "ein Image mit Pfeil-Sprite reicht. Die Spitze sollte nach RECHTS zeigen, " +
             "dann stimmt die Drehung.")]
    [SerializeField] private RectTransform indicatorPrefab;

    [Tooltip("Canvas, auf dem die Pfeile liegen. Leer = Canvas an diesem Objekt.")]
    [SerializeField] private Canvas canvas;

    [Tooltip("Abstand der Pfeile zum Bildschirmrand in Pixeln.")]
    [SerializeField] private float edgePadding = 70f;

    [Tooltip("Pfeil in Flugrichtung zum Ziel drehen.")]
    [SerializeField] private bool rotateTowardTarget = true;

    private readonly List<RectTransform> pool = new();
    private RectTransform canvasRect;

    private void Awake()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvasRect = canvas.transform as RectTransform;

        if (indicatorPrefab == null)
            Debug.LogWarning($"{nameof(OffScreenHighlightIndicator)}: kein Prefab gesetzt — " +
                             "es werden keine Pfeile angezeigt.", this);
    }

    private void LateUpdate()
    {
        int used = 0;

        var director = MissionHighlightDirector.Instance;
        var sourceCamera = Camera.main;

        if (director != null && indicatorPrefab != null && canvasRect != null && sourceCamera != null)
        {
            foreach (var target in director.Highlighted)
            {
                if (target == null || !target.ShowOffScreenIndicator) continue;

                if (TryGetEdgePosition(sourceCamera, target.transform.position,
                                       out var anchored, out var direction))
                {
                    var arrow = GetFromPool(used++);
                    arrow.anchoredPosition = anchored;
                    if (rotateTowardTarget)
                        arrow.localRotation = Quaternion.Euler(0f, 0f,
                            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                }
            }
        }

        // Übrige Pfeile aus dem Pool ausblenden statt zerstören.
        for (int i = used; i < pool.Count; i++)
            if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Liegt das Ziel außerhalb des Bildes? Wenn ja: wo am Rand soll der Pfeil sitzen?
    /// </summary>
    private bool TryGetEdgePosition(Camera cam, Vector3 worldPos,
                                    out Vector2 anchored, out Vector2 direction)
    {
        anchored = Vector2.zero;
        direction = Vector2.right;

        Vector3 screen = cam.WorldToScreenPoint(worldPos);

        // Hinter der Kamera kippt WorldToScreenPoint die Koordinaten (negatives z spiegelt
        // x/y). Ohne diese Korrektur zeigt der Pfeil bei Zielen im Rücken exakt in die
        // falsche Richtung.
        if (screen.z < 0f)
        {
            screen.x = Screen.width - screen.x;
            screen.y = Screen.height - screen.y;
        }

        bool onScreen = screen.z > 0f
                        && screen.x >= 0f && screen.x <= Screen.width
                        && screen.y >= 0f && screen.y <= Screen.height;
        if (onScreen) return false;

        // Vom Bildmittelpunkt aus in Richtung Ziel, dann auf das Rand-Rechteck begrenzen.
        Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 toTarget = new Vector2(screen.x, screen.y) - center;
        if (toTarget.sqrMagnitude < 0.0001f) return false;

        direction = toTarget.normalized;

        Vector2 limit = center - Vector2.one * edgePadding;
        // Wie weit darf man in Richtung 'direction' gehen, bevor eine der beiden Achsen
        // ihren Grenzwert reißt? Die kleinere der beiden Strecken gewinnt.
        float scaleX = Mathf.Abs(direction.x) > 0.0001f ? limit.x / Mathf.Abs(direction.x) : float.MaxValue;
        float scaleY = Mathf.Abs(direction.y) > 0.0001f ? limit.y / Mathf.Abs(direction.y) : float.MaxValue;
        Vector2 edgePoint = center + direction * Mathf.Min(scaleX, scaleY);

        // Screen -> Canvas. Bei Overlay-Canvas ist die Kamera null, sonst die Canvas-Kamera.
        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, edgePoint, uiCam, out anchored);
        return true;
    }

    private RectTransform GetFromPool(int index)
    {
        while (pool.Count <= index)
        {
            var instance = Instantiate(indicatorPrefab, canvasRect);
            instance.gameObject.SetActive(false);
            pool.Add(instance);
        }

        var arrow = pool[index];
        if (!arrow.gameObject.activeSelf) arrow.gameObject.SetActive(true);
        return arrow;
    }
}

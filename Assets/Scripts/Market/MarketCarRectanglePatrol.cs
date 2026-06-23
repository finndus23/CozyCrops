using UnityEngine;

[DisallowMultipleComponent]
public class MarketCarRectanglePatrol : MonoBehaviour
{
    [Header("Rectangle Corners")]
    [Tooltip("Vier echte Szene-Punkte in Fahrreihenfolge. Wenn gesetzt, faehrt das Auto diese Transforms im Kreis ab.")]
    [SerializeField] private Transform[] cornerWaypoints = new Transform[4];

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float turnSpeed = 360f;
    [SerializeField] private float cornerReachDistance = 0.15f;
    [SerializeField] private bool snapToFirstCornerOnStart = false;
    [SerializeField] private bool keepStartHeight = true;

    [Header("Model Alignment")]
    [Tooltip("Nutzen, falls das Auto-Modell nicht mit seiner lokalen Z-Achse nach vorne zeigt.")]
    [SerializeField] private Vector3 visualRotationOffset = Vector3.zero;

    private int targetCornerIndex = 1;
    private float startHeight;

    private void Awake()
    {
        startHeight = transform.position.y;
    }

    private void Start()
    {
        if (!HasValidCorners())
        {
            Debug.LogWarning($"[{nameof(MarketCarRectanglePatrol)}] Bitte genau vier Corner-Waypoints fuer '{name}' setzen.");
            enabled = false;
            return;
        }

        if (snapToFirstCornerOnStart)
            transform.position = GetCornerPosition(0);

        targetCornerIndex = FindNextCornerIndex();
    }

    private void Update()
    {
        Vector3 target = GetCornerPosition(targetCornerIndex);
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= cornerReachDistance * cornerReachDistance)
        {
            targetCornerIndex = (targetCornerIndex + 1) % cornerWaypoints.Length;
            return;
        }

        MoveTowards(target, toTarget.normalized);
    }

    private void MoveTowards(Vector3 target, Vector3 direction)
    {
        Vector3 nextPosition = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
        if (keepStartHeight)
            nextPosition.y = startHeight;

        transform.position = nextPosition;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(visualRotationOffset);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    private int FindNextCornerIndex()
    {
        int nearestIndex = 0;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < cornerWaypoints.Length; i++)
        {
            float distance = (GetCornerPosition(i) - transform.position).sqrMagnitude;
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearestIndex = i;
        }

        return (nearestIndex + 1) % cornerWaypoints.Length;
    }

    private Vector3 GetCornerPosition(int index)
    {
        Transform corner = cornerWaypoints[Mathf.Clamp(index, 0, cornerWaypoints.Length - 1)];
        Vector3 position = corner.position;

        if (keepStartHeight)
            position.y = startHeight;

        return position;
    }

    private bool HasValidCorners()
    {
        if (cornerWaypoints == null || cornerWaypoints.Length != 4)
            return false;

        foreach (Transform corner in cornerWaypoints)
        {
            if (corner == null)
                return false;
        }

        return true;
    }
}

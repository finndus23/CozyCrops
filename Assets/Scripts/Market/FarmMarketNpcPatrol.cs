using UnityEngine;

[DisallowMultipleComponent]
public class FarmMarketNpcPatrol : MonoBehaviour
{
    [Header("Path")]
    [Tooltip("Optionale echte Szene-Wegpunkte. Wenn gesetzt, nutzt der NPC diese Transform-Positionen statt der lokalen Wegpunkte.")]
    [SerializeField] private Transform[] worldWaypoints;

    [Tooltip("Lokale Wegpunkte relativ zur Startposition. Wenn leer, wird automatisch ein kurzer Hin-und-zurueck-Pfad erstellt.")]
    [SerializeField] private Vector3[] localWaypoints = { Vector3.zero, new Vector3(2f, 0f, 0f) };
    [SerializeField] private float moveSpeed = 1.2f;
    [SerializeField] private float turnSpeed = 540f;
    [SerializeField] private float waypointReachDistance = 0.08f;
    [SerializeField] private float waitAtWaypointSeconds = 1f;
    [SerializeField] private bool loop = true;

    [Header("Dialogue")]
    [SerializeField] private Vector3 dialogueEulerAngles = new Vector3(0f, 180f, 0f);

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string idleTrigger = "idle";
    [SerializeField] private string walkTrigger = "walk";
    [SerializeField] private string moveIntParameter = "move";
    [SerializeField] private int idleMoveValue = 0;
    [SerializeField] private int walkMoveValue = 1;

    private Vector3 startPosition;
    private int waypointIndex = 1;
    private int travelDirection = 1;
    private float waitUntil;
    private bool isInDialogue;
    private bool isWalking;

    private void Awake()
    {
        startPosition = transform.position;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null && transform.parent != null)
            animator = transform.parent.GetComponentInChildren<Animator>();

        if (localWaypoints == null || localWaypoints.Length == 0)
            localWaypoints = new[] { Vector3.zero, new Vector3(2f, 0f, 0f) };

        waypointIndex = HasWorldWaypoints ? 0 : localWaypoints.Length > 1 ? 1 : 0;
        isWalking = true;
        PlayIdle();
    }

    private void Update()
    {
        if (isInDialogue || WaypointCount < 2)
            return;

        if (Time.time < waitUntil)
        {
            PlayIdle();
            return;
        }

        Vector3 target = GetWorldWaypoint(waypointIndex);
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= waypointReachDistance * waypointReachDistance)
        {
            transform.position = new Vector3(target.x, transform.position.y, target.z);
            AdvanceWaypoint();
            waitUntil = Time.time + waitAtWaypointSeconds;
            PlayIdle();
            return;
        }

        MoveTowards(target, toTarget.normalized);
    }

    public void EnterDialogue()
    {
        isInDialogue = true;
        PlayIdle();
        transform.rotation = Quaternion.Euler(dialogueEulerAngles);
    }

    public void ExitDialogue()
    {
        isInDialogue = false;
        waitUntil = Time.time + waitAtWaypointSeconds;
        PlayIdle();
    }

    private void MoveTowards(Vector3 target, Vector3 direction)
    {
        Vector3 nextPosition = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
        transform.position = new Vector3(nextPosition.x, transform.position.y, nextPosition.z);

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        PlayWalk();
    }

    private void AdvanceWaypoint()
    {
        if (loop)
        {
            waypointIndex = (waypointIndex + 1) % WaypointCount;
            return;
        }

        if (waypointIndex >= WaypointCount - 1)
            travelDirection = -1;
        else if (waypointIndex <= 0)
            travelDirection = 1;

        waypointIndex += travelDirection;
    }

    private Vector3 GetWorldWaypoint(int index)
    {
        if (HasWorldWaypoints)
        {
            Transform waypoint = worldWaypoints[Mathf.Clamp(index, 0, worldWaypoints.Length - 1)];
            if (waypoint != null)
                return waypoint.position;
        }

        Vector3 localWaypoint = localWaypoints[Mathf.Clamp(index, 0, localWaypoints.Length - 1)];
        return startPosition + localWaypoint;
    }

    private bool HasWorldWaypoints => worldWaypoints != null && worldWaypoints.Length > 0 && worldWaypoints[0] != null;

    private int WaypointCount => HasWorldWaypoints ? worldWaypoints.Length : localWaypoints.Length;

    private void PlayIdle()
    {
        if (!isWalking)
            return;

        SetAnimatorMoveValue(idleMoveValue);
        SetAnimatorTrigger(idleTrigger);
        isWalking = false;
    }

    private void PlayWalk()
    {
        if (isWalking)
            return;

        SetAnimatorMoveValue(walkMoveValue);
        SetAnimatorTrigger(walkTrigger);
        isWalking = true;
    }

    private void SetAnimatorMoveValue(int value)
    {
        if (animator == null || string.IsNullOrWhiteSpace(moveIntParameter))
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name != moveIntParameter || parameter.type != AnimatorControllerParameterType.Int)
                continue;

            animator.SetInteger(moveIntParameter, value);
            return;
        }
    }

    private void SetAnimatorTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name != triggerName || parameter.type != AnimatorControllerParameterType.Trigger)
                continue;

            animator.ResetTrigger(triggerName);
            animator.SetTrigger(triggerName);
            return;
        }
    }
}

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

    [Header("Random Walking Pauses")]
    [Tooltip("Zufaelliger Abstand zwischen kurzen Pausen, waehrend der NPC laeuft.")]
    [SerializeField] private Vector2 randomPauseIntervalSeconds = new Vector2(6f, 12f);
    [Tooltip("Dauer einer zufaelligen Pause waehrend des Laufens.")]
    [SerializeField, Min(0f)] private float randomPauseDurationSeconds = 2f;

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
    private Vector3[] runtimeWorldWaypoints;
    private int waypointIndex = 1;
    private int travelDirection = 1;
    private float waitUntil;
    private float nextRandomPauseAt;
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
        ScheduleNextRandomPause();
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

        if (Time.time >= nextRandomPauseAt)
        {
            waitUntil = Time.time + randomPauseDurationSeconds;
            ScheduleNextRandomPause(randomPauseDurationSeconds);
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
            ScheduleNextRandomPause(waitAtWaypointSeconds);
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
        ScheduleNextRandomPause(waitAtWaypointSeconds);
        PlayIdle();
    }

    /// <summary>
    /// Liefert die aktuell konfigurierte Route als feste Weltpositionen. Dadurch kann
    /// der Marktplatz die Routen beim Laden zwischen den NPCs tauschen, unabhängig davon,
    /// ob sie im Inspector als echte Transforms oder lokale Punkte angelegt wurden.
    /// </summary>
    public Vector3[] GetRouteWorldPoints()
    {
        var route = new Vector3[WaypointCount];
        for (int i = 0; i < route.Length; i++)
            route[i] = GetWorldWaypoint(i);

        return route;
    }

    /// <summary>
    /// Weist eine beim Szenenstart gemischte Route zu und setzt den NPC direkt auf deren
    /// ersten Punkt. So läuft er nicht erst quer durch Gebäude zu seiner neuen Strecke.
    /// </summary>
    public void SetRuntimeRoute(Vector3[] route, bool reverse)
    {
        if (route == null || route.Length < 2)
            return;

        runtimeWorldWaypoints = new Vector3[route.Length];
        for (int i = 0; i < route.Length; i++)
        {
            int sourceIndex = reverse ? route.Length - 1 - i : i;
            runtimeWorldWaypoints[i] = route[sourceIndex];
        }

        Vector3 routeStart = runtimeWorldWaypoints[0];
        transform.position = new Vector3(routeStart.x, transform.position.y, routeStart.z);
        waypointIndex = 1;
        travelDirection = 1;
        waitUntil = Time.time + Random.Range(0f, 0.75f);
        ScheduleNextRandomPause(waitUntil - Time.time);
        PlayIdle();
    }

    private void ScheduleNextRandomPause(float delayBeforeCounting = 0f)
    {
        float minimum = Mathf.Max(0f, randomPauseIntervalSeconds.x);
        float maximum = Mathf.Max(minimum, randomPauseIntervalSeconds.y);
        nextRandomPauseAt = Time.time + delayBeforeCounting + Random.Range(minimum, maximum);
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
        if (HasRuntimeWorldWaypoints)
            return runtimeWorldWaypoints[Mathf.Clamp(index, 0, runtimeWorldWaypoints.Length - 1)];

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

    private bool HasRuntimeWorldWaypoints => runtimeWorldWaypoints != null && runtimeWorldWaypoints.Length > 0;

    private int WaypointCount => HasRuntimeWorldWaypoints
        ? runtimeWorldWaypoints.Length
        : HasWorldWaypoints ? worldWaypoints.Length : localWaypoints.Length;

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

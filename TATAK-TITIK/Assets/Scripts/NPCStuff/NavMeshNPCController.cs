using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshNPCController : MonoBehaviour
{
    [Header("Optional Patrol")]
    [Tooltip("If set, NPC will patrol these points in order when UsePatrol is true.")]
    public Transform[] patrolPoints;
    public bool usePatrol = false;
    public float patrolWait = 1f; // pause at each patrol point
    public bool loopPatrol = true;

    [Header("Agent Settings")]
    public NavMeshAgent agent;
    [Tooltip("Considered 'arrived' when remainingDistance <= stoppingDistance + arriveThreshold (meters)")]
    public float arriveThreshold = 0.2f; // small tolerance (meters)

    [Header("Rotation / Look")]
    [Tooltip("If assigned, this transform will be rotated instead of the GameObject root (useful when model is a child).")]
    public Transform modelRoot = null;
    [Tooltip("Rotation speed in degrees per second. Higher = snappier.")]
    public float rotationSpeed = 720f; // degrees/sec

    [Tooltip("When true, the NPC will look ahead along the NavMesh path toward the destination (preferred).")]
    public bool preferPathDirection = true;
    [Tooltip("How far ahead along the path (meters) the NPC will look. Bigger values = looking further down the path.")]
    public float lookAheadDistance = 1.5f;
    [Tooltip("If true, average direction across the next 'smoothSegments' segments to reduce snapping on cornered paths.")]
    public bool smoothPathDirection = true;
    [Tooltip("Number of upcoming path corners to include in the smoothing average (1 = only the first segment).")]
    [Range(1, 8)]
    public int smoothSegments = 3;

    [Tooltip("If true, fall back to agent.velocity/steeringTarget when path info is unavailable.")]
    public bool fallbackToMovement = true;

    [Header("Visual / Bounce")]
    [Tooltip("The transform that visually represents the NPC (the one we'll move up/down). If null, falls back to modelRoot then root.")]
    public Transform modelVisual = null;
    [Tooltip("Maximum vertical offset for the bounce (meters).")]
    public float bounceAmplitude = 0.1f;
    [Tooltip("How fast the bounce cycles (times per second).")]
    public float bounceFrequency = 5f;
    [Tooltip("Minimum agent speed (m/s) to consider the NPC as 'moving'.")]
    public float moveSpeedThreshold = 0.05f;

    [Header("Debug")]
    public bool debugGizmos = false;

    // runtime
    int patrolIndex = 0;
    Coroutine patrolRoutine = null;

    const float k_RotationDirSqrThreshold = 0.0001f;

    // bounce runtime
    private float bounceTimer = 0f;
    private float originalVisualLocalY = 0f;
    private bool visualInitialized = false;

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        // We'll control rotation ourselves
        if (agent != null)
            agent.updateRotation = false;
    }

    void Start()
    {
        // Setup patrol if requested
        if (usePatrol && patrolPoints != null && patrolPoints.Length > 0)
            StartPatrol();

        // Determine modelVisual fallback and cache original local Y
        if (modelVisual == null)
        {
            if (modelRoot != null) modelVisual = modelRoot;
            else modelVisual = transform;
        }

        if (modelVisual != null)
        {
            originalVisualLocalY = modelVisual.localPosition.y;
            visualInitialized = true;
        }
    }

    void Update()
    {
        HandleRotation();
        HandleBounce();
    }

    // -------------------
    // Rotation / Look
    // -------------------
    void HandleRotation()
    {
        Transform rotRoot = modelRoot != null ? modelRoot : transform;

        Vector3 desiredDir;
        bool haveDesired = ComputeForwardDirection(rotRoot.position, out desiredDir);

        if (haveDesired)
        {
            Quaternion targetRot = Quaternion.LookRotation(desiredDir);
            rotRoot.rotation = Quaternion.RotateTowards(rotRoot.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// Compute a forward-looking direction based on path look-ahead, smoothing or fallbacks.
    /// Returns true if a meaningful direction was produced.
    /// </summary>
    bool ComputeForwardDirection(Vector3 origin, out Vector3 outDir)
    {
        outDir = Vector3.zero;
        if (agent == null) return false;

        // Prefer looking ahead along the path
        if (preferPathDirection && agent.hasPath && agent.path != null && agent.path.corners != null && agent.path.corners.Length > 0)
        {
            Vector3 forwardPoint;
            if (smoothPathDirection)
            {
                // Average directions across next 'smoothSegments' segments weighted by segment length until lookAheadDistance reached
                if (TryGetSmoothedForwardPoint(origin, lookAheadDistance, smoothSegments, out forwardPoint))
                {
                    Vector3 dir = forwardPoint - origin;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > k_RotationDirSqrThreshold)
                    {
                        outDir = dir.normalized;
                        return true;
                    }
                }
            }
            else
            {
                if (TryGetPathForwardPoint(origin, lookAheadDistance, out forwardPoint))
                {
                    Vector3 dir = forwardPoint - origin;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > k_RotationDirSqrThreshold)
                    {
                        outDir = dir.normalized;
                        return true;
                    }
                }
            }
        }

        // fallback options
        if (fallbackToMovement)
        {
            // prefer agent.velocity
            Vector3 vel = agent.velocity;
            vel.y = 0f;
            if (vel.sqrMagnitude > k_RotationDirSqrThreshold)
            {
                outDir = vel.normalized;
                return true;
            }

            // agent.steeringTarget as last resort
            if (agent.hasPath)
            {
                Vector3 steer = agent.steeringTarget - origin;
                steer.y = 0f;
                if (steer.sqrMagnitude > k_RotationDirSqrThreshold)
                {
                    outDir = steer.normalized;
                    return true;
                }
            }
        }

        return false;
    }

    // -------------------
    // Bounce handling
    // -------------------
    void HandleBounce()
    {
        if (!visualInitialized || modelVisual == null || agent == null) return;

        // Consider NPC moving when agent has velocity above threshold and not intentionally stopped and not arrived.
        bool isMoving = agent.velocity.sqrMagnitude >= (moveSpeedThreshold * moveSpeedThreshold) && !agent.isStopped && !HasArrived();

        if (isMoving)
        {
            // advance timer and apply vertical offset (same math as your player script)
            bounceTimer += Time.deltaTime * bounceFrequency;
            float bounceOffset = Mathf.Sin(bounceTimer * Mathf.PI * 2f) * bounceAmplitude;

            Vector3 localPos = modelVisual.localPosition;
            localPos.y = originalVisualLocalY + bounceOffset;
            modelVisual.localPosition = localPos;
        }
        else
        {
            // reset timer and ensure visual returns to original Y
            bounceTimer = 0f;
            Vector3 localPos = modelVisual.localPosition;
            localPos.y = originalVisualLocalY;
            modelVisual.localPosition = localPos;
        }
    }

    /// <summary>
    /// Walks the path corners and returns a point ahead along the path by 'ahead' meters from origin.
    /// </summary>
    bool TryGetPathForwardPoint(Vector3 origin, float ahead, out Vector3 outPoint)
    {
        outPoint = origin;
        NavMeshPath path = agent.path;
        if (path == null || path.corners == null || path.corners.Length == 0)
        {
            // fallback to destination
            if (agent.hasPath)
            {
                outPoint = agent.destination;
                return true;
            }
            return false;
        }

        Vector3[] corners = path.corners;

        float remaining = ahead;
        Vector3 current = origin;

        for (int i = 0; i < corners.Length; ++i)
        {
            Vector3 corner = corners[i];
            Vector3 flatSeg = new Vector3(corner.x - current.x, 0f, corner.z - current.z);
            float segLen = flatSeg.magnitude;

            if (segLen >= remaining)
            {
                if (segLen <= 0.0001f)
                    outPoint = corner;
                else
                    outPoint = Vector3.Lerp(current, corner, remaining / segLen);
                return true;
            }
            else
            {
                remaining -= segLen;
                current = corner;
            }
        }

        // if we run out of corners, return final corner
        outPoint = corners[corners.Length - 1];
        return true;
    }

    /// <summary>
    /// Computes a weighted average forward point across the next 'maxSegments' corners to reduce snapping.
    /// This walks segments and accumulates a point that represents looking 'ahead' along multiple short segments.
    /// </summary>
    bool TryGetSmoothedForwardPoint(Vector3 origin, float ahead, int maxSegments, out Vector3 outPoint)
    {
        outPoint = origin;
        NavMeshPath path = agent.path;
        if (path == null || path.corners == null || path.corners.Length == 0)
        {
            if (agent.hasPath)
            {
                outPoint = agent.destination;
                return true;
            }
            return false;
        }

        Vector3[] corners = path.corners;

        // Build segment list starting at origin
        float remaining = ahead;
        Vector3 current = origin;
        int usedSegments = 0;

        // We'll compute a weighted average position along the next up to maxSegments segments.
        // Weight each segment's end-point by how much of the 'ahead' window it contributes.
        Vector3 accumPos = Vector3.zero;
        float accumWeight = 0f;

        for (int i = 0; i < corners.Length && usedSegments < maxSegments; ++i)
        {
            Vector3 corner = corners[i];
            Vector3 flatSeg = new Vector3(corner.x - current.x, 0f, corner.z - current.z);
            float segLen = flatSeg.magnitude;

            if (segLen <= 0.0001f)
            {
                current = corner;
                continue;
            }

            if (segLen >= remaining)
            {
                // partial segment: contributes remaining length at a fractional point
                Vector3 sample = Vector3.Lerp(current, corner, remaining / segLen);
                float weight = remaining; // weight proportional to length contributed
                accumPos += sample * weight;
                accumWeight += weight;
                usedSegments++;
                remaining = 0f;
                break; // we've covered look-ahead distance
            }
            else
            {
                // full segment contributes its endpoint
                float weight = segLen;
                accumPos += corner * weight;
                accumWeight += weight;
                remaining -= segLen;
                current = corner;
                usedSegments++;
            }
        }

        if (accumWeight <= 0f)
        {
            // fallback to final corner
            outPoint = corners[corners.Length - 1];
            return true;
        }

        outPoint = accumPos / accumWeight;
        return true;
    }

    // -------------------
    // Public API (same surface as before)
    // -------------------
    public void MoveTo(Vector3 worldPos)
    {
        StopPatrolIfRunning();
        if (agent == null) return;
        agent.isStopped = false;
        agent.SetDestination(worldPos);
    }

    public void MoveTo(Transform target)
    {
        if (target == null) return;
        MoveTo(target.position);
    }

    public void StopMoving()
    {
        if (agent == null) return;
        agent.isStopped = true;
    }

    public void ResumeMoving()
    {
        if (agent == null) return;
        agent.isStopped = false;
    }

    public void StartPatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        StopPatrolIfRunning();
        usePatrol = true;
        patrolIndex = 0;
        patrolRoutine = StartCoroutine(PatrolCoroutine());
    }

    public void StopPatrol()
    {
        usePatrol = false;
        StopPatrolIfRunning();
    }

    void StopPatrolIfRunning()
    {
        if (patrolRoutine != null)
        {
            StopCoroutine(patrolRoutine);
            patrolRoutine = null;
        }
    }

    IEnumerator PatrolCoroutine()
    {
        while (usePatrol && patrolPoints != null && patrolPoints.Length > 0)
        {
            Transform targetPoint = patrolPoints[patrolIndex];
            if (targetPoint != null)
            {
                if (agent == null) yield break;

                agent.isStopped = false;
                agent.SetDestination(targetPoint.position);

                while (agent.pathPending)
                    yield return null;

                while (!HasArrived())
                    yield return null;

                yield return new WaitForSeconds(patrolWait);
            }

            patrolIndex++;
            if (patrolIndex >= patrolPoints.Length)
            {
                if (loopPatrol) patrolIndex = 0;
                else break;
            }
        }

        patrolRoutine = null;
    }

    bool HasArrived()
    {
        if (agent == null) return false;
        if (agent.pathPending) return false;
        if (!agent.hasPath) return true;
        return agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, arriveThreshold);
    }

    void OnDrawGizmosSelected()
    {
        if (!debugGizmos) return;

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null) continue;
                Gizmos.DrawWireSphere(patrolPoints[i].position, 0.25f);
                if (i + 1 < patrolPoints.Length && patrolPoints[i + 1] != null)
                    Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[i + 1].position);
            }
            if (loopPatrol && patrolPoints.Length > 1 && patrolPoints[0] != null)
                Gizmos.DrawLine(patrolPoints[patrolPoints.Length - 1].position, patrolPoints[0].position);
        }

        if (agent != null && agent.hasPath)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(agent.destination, 0.15f);
            Gizmos.DrawLine(transform.position, agent.destination);

            // draw look-ahead
            Vector3 p;
            if (TryGetPathForwardPoint(transform.position, lookAheadDistance, out p))
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(p, 0.08f);
                Gizmos.DrawLine(transform.position, p);
            }
        }
    }
}

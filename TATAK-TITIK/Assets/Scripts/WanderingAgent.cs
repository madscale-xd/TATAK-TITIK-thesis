using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public class WanderingAgent : MonoBehaviour
{
    [Header("Wander Settings")]
    [Tooltip("Max distance from the object's position to pick wander destinations.")]
    public float wanderRadius = 10f;

    [Tooltip("How often (seconds) the agent will pick a new destination.")]
    public float wanderInterval = 3f;

    [Tooltip("Which NavMesh area mask to sample from. Use NavMesh.AllAreas to include all.")]
    public int areaMask = NavMesh.AllAreas;

    [Header("Behavior")]
    [Tooltip("If true, the agent will pause briefly after reaching a destination.")]
    public bool waitAtDestination = true;
    public float minWaitTime = 1f;
    public float maxWaitTime = 3f;

    [Tooltip("Optional randomized delay before starting to desynchronize multiple agents.")]
    public bool randomizeStartDelay = true;

    [Header("Agent Reference")]
    [Tooltip("If left null the component will try to get the NavMeshAgent on this GameObject at Awake.")]
    public NavMeshAgent agent;

    // internal
    bool isRunning = false;

    void OnValidate()
    {
        wanderRadius = Mathf.Max(0f, wanderRadius);
        wanderInterval = Mathf.Max(0.01f, wanderInterval);
        minWaitTime = Mathf.Max(0f, minWaitTime);
        maxWaitTime = Mathf.Max(minWaitTime, maxWaitTime);
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
    }

    void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        if (agent == null)
        {
            Debug.LogWarning("WanderingAgent requires a NavMeshAgent component.", this);
            enabled = false;
            return;
        }

        float startDelay = randomizeStartDelay ? Random.Range(0f, wanderInterval) : 0f;
        StartCoroutine(WanderRoutine(startDelay));
    }

    IEnumerator WanderRoutine(float initialDelay)
    {
        isRunning = true;
        yield return new WaitForSeconds(initialDelay);

        while (isActiveAndEnabled)
        {
            // if agent is idle or reached its destination, pick a new one
            if (!agent.pathPending && (agent.remainingDistance <= agent.stoppingDistance || !agent.hasPath))
            {
                Vector3 dest;
                if (RandomNavmeshPoint(transform.position, wanderRadius, out dest))
                {
                    agent.SetDestination(dest);
                }

                if (waitAtDestination)
                {
                    float wait = Random.Range(minWaitTime, maxWaitTime);
                    yield return new WaitForSeconds(wait);
                }
            }

            // wait a short while before checking again to avoid busy-looping
            yield return new WaitForSeconds(wanderInterval);
        }

        isRunning = false;
    }

    /// <summary>
    /// Tries several random points inside a sphere and samples the NavMesh to find a valid position.
    /// </summary>
    bool RandomNavmeshPoint(Vector3 center, float range, out Vector3 result)
    {
        for (int i = 0; i < 30; i++)
        {
            Vector3 randomPos = center + Random.insideUnitSphere * range;
            NavMeshHit hit;
            // sample within a small snapping distance (2 meters) to the navmesh
            if (NavMesh.SamplePosition(randomPos, out hit, 2f, areaMask))
            {
                result = hit.position;
                return true;
            }
        }

        result = center;
        return false;
    }

    [ContextMenu("Find New Destination Now")]
    void FindNewDestinationNow()
    {
        if (agent == null) return;
        Vector3 dest;
        if (RandomNavmeshPoint(transform.position, wanderRadius, out dest))
            agent.SetDestination(dest);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.25f);
        Gizmos.DrawSphere(transform.position, wanderRadius);
    }
}

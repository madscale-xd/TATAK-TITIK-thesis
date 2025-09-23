using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class KikoTask5Trigger : MonoBehaviour
{
    [Header("NPC references (assign both)")]
    public NPCManager kiko;
    public NPCManager babaylan;

    [Header("Destination checks (optional)")]
    [Tooltip("If true, the NPC must be close to the specified Transform target to count as 'arrived'. If false, agent state is used.")]
    public bool requireSpecificTargets = true;
    public Transform kikoTarget;
    public Transform babaylanTarget;
    [Tooltip("Distance tolerance (meters) when requireSpecificTargets is used.")]
    public float targetTolerance = 0.5f;

    [Header("Polling / stability")]
    [Tooltip("How often (seconds) to sample NPC state.")]
    public float pollInterval = 0.15f;
    [Tooltip("Seconds that both NPCs must remain idle/at-destination before firing.")]
    public float requiredStableSeconds = 0.6f;
    [Tooltip("If true, this trigger will only fire once.")]
    public bool triggerOnce = true;
    [Tooltip("If true, GameObject will be disabled after successful trigger.")]
    public bool disableAfterTrigger = false;

    [Header("Baybayin Manager (called on success)")]
    [Tooltip("Optional: assign BaybayinManager so Task5() is invoked when success condition is reached.")]
    public BaybayinManager baybayinManager;

    [Header("Event")]
    public UnityEvent onBothIdle;

    [Header("Debug")]
    public bool debugLogs = false;

    bool hasTriggered = false;
    Coroutine monitorCoroutine = null;

    void OnEnable()
    {
        // start monitoring automatically if both assigned
        if (kiko == null || babaylan == null)
        {
            if (debugLogs) Debug.LogWarning("[KikoTask5Trigger] Kiko and/or Babaylan not assigned. Waiting for assignment before monitoring.");
            return;
        }

        StartMonitoring();
    }

    void OnDisable()
    {
        StopMonitoring();
    }

    public void StartMonitoring()
    {
        if (hasTriggered && triggerOnce) return;
        if (monitorCoroutine != null) StopCoroutine(monitorCoroutine);
        monitorCoroutine = StartCoroutine(MonitorBothIdleCoroutine());
    }

    public void StopMonitoring()
    {
        if (monitorCoroutine != null)
        {
            StopCoroutine(monitorCoroutine);
            monitorCoroutine = null;
        }
    }

    IEnumerator MonitorBothIdleCoroutine()
    {
        float stableTimer = 0f;

        while (true)
        {
            if (hasTriggered && triggerOnce)
                yield break;

            // quick null guards
            if (kiko == null || babaylan == null)
            {
                if (debugLogs) Debug.LogWarning("[KikoTask5Trigger] NPC references missing while monitoring. Stopping.");
                yield break;
            }

            bool kikoNotTalking = !kiko.IsTalking();
            bool babaNotTalking = !babaylan.IsTalking();

            bool kikoAtDestination = requireSpecificTargets
                ? IsAtTarget(kiko, kikoTarget)
                : IsAgentIdleOrAtDestination(kiko);
            bool babaAtDestination = requireSpecificTargets
                ? IsAtTarget(babaylan, babaylanTarget)
                : IsAgentIdleOrAtDestination(babaylan);

            if (debugLogs)
            {
                Debug.Log($"[KikoTask5Trigger] kikoNotTalking={kikoNotTalking} babaNotTalking={babaNotTalking} kikoAtDest={kikoAtDestination} babaAtDest={babaAtDestination} stableTimer={stableTimer:F2}");
            }

            if (kikoNotTalking && babaNotTalking && kikoAtDestination && babaAtDestination)
            {
                // accumulate stable time
                stableTimer += pollInterval;
                if (stableTimer >= requiredStableSeconds)
                {
                    // Success — call BaybayinManager.Task5() and the UnityEvent
                    if (debugLogs) Debug.Log("[KikoTask5Trigger] Both NPCs idle & arrived — firing onBothIdle and BaybayinManager.Task5().");

                    // 1) BaybayinManager Task5 (optional)
                    if (baybayinManager != null)
                    {
                        try
                        {
                            baybayinManager.Task5();
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[KikoTask5Trigger] Exception when calling BaybayinManager.Task5(): {ex}");
                        }
                    }
                    else
                    {
                        if (debugLogs) Debug.Log("[KikoTask5Trigger] baybayinManager not assigned; skipping Task5 call.");
                    }

                    // 2) Invoke inspector event
                    try
                    {
                        onBothIdle?.Invoke();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[KikoTask5Trigger] Exception when invoking onBothIdle: {ex}");
                    }

                    hasTriggered = true;

                    if (disableAfterTrigger)
                        gameObject.SetActive(false);

                    if (triggerOnce)
                        yield break;
                    else
                        stableTimer = 0f; // if not one-shot, reset and continue monitoring
                }
            }
            else
            {
                // reset stability timer
                stableTimer = 0f;
            }

            yield return new WaitForSeconds(pollInterval);
        }
    }

    // Improved IsAtTarget - replace your existing method with this
    bool IsAtTarget(NPCManager npc, Transform target)
    {
        if (npc == null || target == null) return false;

        // Prefer the visual/model position if Nav controller exposes it (feet/visual)
        Vector3 npcPos;
        if (npc.navController != null && npc.navController.modelRoot != null)
            npcPos = npc.navController.modelRoot.position;
        else
            npcPos = npc.transform.position;

        // If the target has a collider, use ClosestPoint so being *inside* a trigger counts.
        Collider targetCol = target.GetComponent<Collider>();
        Vector3 targetPoint = (targetCol != null) ? targetCol.ClosestPoint(npcPos) : target.position;

        float dist = Vector3.Distance(npcPos, targetPoint);

        if (debugLogs)
        {
            Debug.Log($"[KikoTask5Trigger] IsAtTarget check: npc={npc.name}, npcPos={npcPos}, target={target.name}, " +
                    $"targetPoint={targetPoint}, dist={dist:F3}, tolerance={targetTolerance:F3}");
        }

        return dist <= Mathf.Max(0.01f, targetTolerance);
    }

    bool IsAgentIdleOrAtDestination(NPCManager npc)
    {
        if (npc == null) return true;

        var nav = npc.navController;
        if (nav == null)
            return true;

        var agent = nav.agent;
        if (agent == null)
            return true;

        if (agent.pathPending)
            return false;

        if (!agent.hasPath)
            return true;

        float remaining = agent.remainingDistance;
        float stop = agent.stoppingDistance;
        if (float.IsNaN(remaining) || float.IsInfinity(remaining))
            return agent.velocity.sqrMagnitude <= 0.01f;

        if (remaining <= (stop + 0.1f))
            return true;

        return false;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying)
        {
            if (kiko != null && babaylan != null && monitorCoroutine == null && !hasTriggered)
                StartMonitoring();
        }
    }
#endif
}

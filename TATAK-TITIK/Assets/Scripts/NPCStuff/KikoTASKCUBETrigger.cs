using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Waypoint trigger that fires only when:
///  - the specified NPC (targetNPC) reaches the trigger (or any NPC if targetNPC is null),
///  AND
///  - a specified BaybayinManager task is currently started (BaybayinManager.IsTaskStarted(requiredTask)).
///  
/// Hook onReached in the inspector.
/// </summary>
[RequireComponent(typeof(Collider))]
public class KikoTASKCUBETrigger : MonoBehaviour
{
    [Tooltip("If assigned, only this NPCManager instance will trigger the waypoint. If null, any NPCManager will match.")]
    public NPCManager targetNPC;

    [Tooltip("If non-empty, this trigger will only fire when BaybayinManager.IsTaskStarted(requiredTask) returns true.")]
    public string requiredTask = "";

    [Tooltip("If true, the waypoint will fire only once and then ignore further collisions.")]
    public bool triggerOnce = true;

    [Tooltip("Optional: automatically disable the GameObject after triggering (handy for one-time cubes).")]
    public bool disableAfterTrigger = false;

    [Tooltip("Event invoked when the NPC reaches the waypoint and task gating passes.")]
    public UnityEvent onReached;

    [Tooltip("Reference to your BaybayinManager used for IsTaskStarted(). If left null the script will try to FindObjectOfType at runtime.")]
    public BaybayinManager BayMan;

    [Tooltip("Enable to get debug logs.")]
    public bool debugLogs = false;

    bool hasTriggered = false;

    void Reset()
    {
        // make the collider a trigger by default in the editor for convenience
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col == null)
            Debug.LogWarning($"[KikoTASK18Trigger:{name}] No Collider found (expected one).");
        else if (!col.isTrigger)
            Debug.LogWarning($"[KikoTASK18Trigger:{name}] Collider is not set to 'isTrigger'. Recommended: set it to isTrigger.");

        if (BayMan == null)
        {
            BayMan = FindObjectOfType<BaybayinManager>();
            if (BayMan == null && !string.IsNullOrWhiteSpace(requiredTask))
                Debug.LogWarning($"[KikoTASK18Trigger:{name}] BayMan not assigned and none found in scene, but requiredTask='{requiredTask}' is set. Trigger will remain disabled until BayMan is available.");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered && triggerOnce) return;

        // find NPCManager in the entering object's parents (including itself)
        var npcMgr = other.GetComponentInParent<NPCManager>();
        if (npcMgr == null)
        {
            if (debugLogs) Debug.Log($"[KikoTASK18Trigger:{name}] Entered by '{other.name}' but no NPCManager found in parent chain.");
            return;
        }

        // require targetNPC match if provided
        if (targetNPC != null && npcMgr != targetNPC)
        {
            if (debugLogs) Debug.Log($"[KikoTASK18Trigger:{name}] NPC '{npcMgr.name}' did not match required targetNPC '{targetNPC.name}'.");
            return;
        }

        // Check task gating
        if (!IsTaskAllowed())
        {
            if (debugLogs)
            {
                if (string.IsNullOrWhiteSpace(requiredTask))
                    Debug.Log($"[KikoTASK18Trigger:{name}] Task gating skipped (no requiredTask).");
                else
                    Debug.Log($"[KikoTASK18Trigger:{name}] NPC matched but requiredTask '{requiredTask}' is not started.");
            }

            return;
        }

        // Passed both conditions: invoke event
        if (debugLogs) Debug.Log($"[KikoTASK18Trigger:{name}] Matched NPC '{npcMgr.name}' and task gating passed. Invoking onReached.");

        try
        {
            onReached?.Invoke();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[KikoTASK18Trigger:{name}] Exception invoking onReached: {ex}");
        }

        hasTriggered = true;

        if (disableAfterTrigger)
            gameObject.SetActive(false);
    }

    /// <summary>
    /// Returns true when the requiredTask is empty (no gating) or when BayMan.IsTaskStarted(requiredTask) is true.
    /// If BayMan is missing and a task is required, returns false.
    /// </summary>
    private bool IsTaskAllowed()
    {
        if (string.IsNullOrWhiteSpace(requiredTask))
            return true; // no gating required

        if (BayMan == null)
        {
            // try to recover once at runtime
            BayMan = FindObjectOfType<BaybayinManager>();
            if (BayMan == null)
            {
                Debug.LogWarning($"[KikoTASK18Trigger:{name}] requiredTask='{requiredTask}' but BayMan reference is null and not found in scene. Treating as not started.");
                return false;
            }
        }

        try
        {
            return BayMan.IsTaskStarted(requiredTask);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[KikoTASK18Trigger:{name}] Exception while checking BayMan.IsTaskStarted('{requiredTask}'): {ex}. Treating as not started.");
            return false;
        }
    }

    // Public helper to reset the trigger (if you want to re-arm it from code)
    public void ResetTriggeredState()
    {
        hasTriggered = false;
    }
}

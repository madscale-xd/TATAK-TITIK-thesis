using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class KikoTASK9Trigger : MonoBehaviour
{
    [Tooltip("If assigned, only this NPCManager instance will trigger the waypoint.")]
    public NPCManager targetNPC;

    [Tooltip("If assigned, only the NPC with this NPC ID (NPCDialogueTrigger.GetNPCID()) will trigger the waypoint.")]
    public string targetNPCID = "";

    [Tooltip("If true, the waypoint will fire only once and then ignore further collisions.")]
    public bool triggerOnce = true;

    [Tooltip("Optional: automatically disable the GameObject after triggering (handy for one-time cubes).")]
    public bool disableAfterTrigger = false;

    [Tooltip("Event invoked when the NPC reaches the waypoint and the required Baybayin task has started.")]
    public UnityEvent onReached;

    [Tooltip("Enable to get debug logs.")]
    public bool debugLogs = false;

    [Tooltip("The Baybayin task ID that must be started for this trigger to fire.")]
    public string requiredTaskID = "task8";

    bool hasTriggered = false;
    public BaybayinManager BayMan;

    void Reset()
    {
        // Make sure collider is trigger by default for convenience in editor
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void Awake()
    {
        // sanity check for collider
        var col = GetComponent<Collider>();
        if (col == null)
            Debug.LogWarning($"[KikoTASK9Trigger:{name}] No Collider found (expected one).");
        else if (!col.isTrigger)
            Debug.LogWarning($"[KikoTASK9Trigger:{name}] Collider is not set to 'isTrigger'. Recommended: set it to isTrigger.");
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered && triggerOnce)
            return;

        // Look for an NPCManager in the entering object's parents (including itself)
        var npcMgr = other.GetComponentInParent<NPCManager>();

        bool match = false;

        // 1) If a specific NPCManager was assigned, require identity match
        if (targetNPC != null)
        {
            if (npcMgr != null && npcMgr == targetNPC)
                match = true;
        }

        // 2) If targetNPCID provided, check NPCDialogueTrigger on the incoming root
        if (!match && !string.IsNullOrWhiteSpace(targetNPCID))
        {
            var dialogueTrigger = other.GetComponentInParent<NPCDialogueTrigger>();
            if (dialogueTrigger != null && string.Equals(dialogueTrigger.GetNPCID(), targetNPCID))
                match = true;
        }

        // 3) If neither filter provided, accept any NPCManager
        if (!match && targetNPC == null && string.IsNullOrWhiteSpace(targetNPCID))
        {
            if (npcMgr != null)
                match = true;
        }

        if (!match)
        {
            if (debugLogs) Debug.Log($"[KikoTASK9Trigger:{name}] Entered by '{other.name}' but did not match target filters.");
            return;
        }

        // At this point we matched the NPC — now require the Baybayin task to have started
        bool taskStarted = false;
        if (BayMan != null)
        {
            try
            {
                taskStarted = BayMan.IsTaskStarted(requiredTaskID);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[KikoTASK9Trigger:{name}] Exception while checking BaybayinManager.IsTaskStarted: {ex}");
                taskStarted = false;
            }
        }
        else
        {
            if (debugLogs) Debug.LogWarning($"[KikoTASK9Trigger:{name}] BayMan is null - treating task '{requiredTaskID}' as not started.");
        }

        if (!taskStarted)
        {
            if (debugLogs) Debug.Log($"[KikoTASK9Trigger:{name}] NPC matched but required task '{requiredTaskID}' hasn't started yet. onReached not invoked.");
            return;
        }

        // All checks passed — invoke
        if (debugLogs) Debug.Log($"[KikoTASK9Trigger:{name}] Matched NPC '{(npcMgr != null ? npcMgr.name : other.name)}' and task '{requiredTaskID}' started. Invoking onReached.");

        try
        {
            onReached?.Invoke();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[KikoTASK9Trigger:{name}] Exception invoking onReached: {ex}");
        }

        hasTriggered = true;

        if (disableAfterTrigger)
            gameObject.SetActive(false);
    }
}

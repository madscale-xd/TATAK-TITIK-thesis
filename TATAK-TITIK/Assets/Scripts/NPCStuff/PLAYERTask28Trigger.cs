using System;
using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PLAYERTask28Trigger : MonoBehaviour
{
    [Tooltip("Tag the player uses (object entering trigger must have this tag).")]
    public string playerTag = "Player";

    [Tooltip("The BaybayinManager task name that must be started before this trigger is allowed.")]
    public string requiredTaskLock = "task27";

    [Tooltip("Optional reference to BaybayinManager. If null the script will attempt FindObjectOfType at runtime.")]
    public BaybayinManager BayMan;

    [Tooltip("If true the trigger will only fire once.")]
    public bool triggerOnce = true;

    [Tooltip("Enable debug logging.")]
    public bool debugLogs = false;

    bool hasTriggered = false;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col == null)
            Debug.LogWarning($"[HAMOGTrigger:{name}] No Collider found on this GameObject (expected isTrigger).");
        else if (!col.isTrigger)
            Debug.LogWarning($"[HAMOGTrigger:{name}] Collider is not set to isTrigger. Recommended: set it to isTrigger.");

        if (BayMan == null)
            BayMan = FindObjectOfType<BaybayinManager>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered && triggerOnce) return;
        if (!other.CompareTag(playerTag)) return;

        if (BayMan == null)
        {
            BayMan = FindObjectOfType<BaybayinManager>();
            if (BayMan == null)
            {
                Debug.LogWarning("[HAMOGTrigger] BaybayinManager not found in scene; cannot proceed.");
                return;
            }
        }

        bool allowed = false;
        try
        {
            allowed = BayMan.IsTaskStarted(requiredTaskLock);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAMOGTrigger] Exception while checking BayMan.IsTaskStarted('{requiredTaskLock}'): {ex}. Blocking trigger.");
            return;
        }

        if (!allowed)
        {
            if (debugLogs) Debug.Log($"[HAMOGTrigger:{name}] Player entered but required task '{requiredTaskLock}' is not started yet.");
            return;
        }

        // Allowed: call Task28 on BayMan (try direct call, fallback to reflection)
        try
        {
            // try direct strongly-typed call first
            BayMan.Task28();
            if (debugLogs) Debug.Log($"[HAMOGTrigger:{name}] Called BayMan.Task28() directly.");
        }
        catch (MissingMethodException)
        {
            // fall through to reflection attempt below
            if (debugLogs) Debug.Log($"[HAMOGTrigger:{name}] BayMan.Task28() not found directly — attempting reflection.");
            TryInvokeTaskByName("Task28");
        }
        catch (Exception ex)
        {
            if (debugLogs) Debug.LogWarning($"[HAMOGTrigger:{name}] Direct call to BayMan.Task28() threw: {ex}. Attempting reflection fallback.");
            TryInvokeTaskByName("Task28");
        }

        hasTriggered = true;
    }

    private void TryInvokeTaskByName(string methodName)
    {
        if (BayMan == null) return;
        try
        {
            MethodInfo mi = BayMan.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi != null && mi.GetParameters().Length == 0)
            {
                mi.Invoke(BayMan, null);
                if (debugLogs) Debug.Log($"[HAMOGTrigger:{name}] Invoked '{methodName}' on BayMan via reflection.");
            }
            else
            {
                Debug.LogWarning($"[HAMOGTrigger:{name}] Could not find a parameterless method named '{methodName}' on BaybayinManager.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAMOGTrigger:{name}] Exception invoking '{methodName}' via reflection: {ex}");
        }
    }

    // Optional: allow external code to re-arm this trigger
    public void ResetTrigger()
    {
        hasTriggered = false;
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Reflection;

/// <summary>
/// Interaction trigger: when player is in range and presses E,
/// call BaybayinManager.Task27() — but only if baybayinManager.IsTaskTriggered(requiredTaskTrigger) returns true.
/// No DialogueEventsManager. No tag-based checks. Polls BaybayinManager while in range to show/hide prompt.
/// Attach to the interactive GameObject (Collider must be isTrigger = true).
/// </summary>
[RequireComponent(typeof(Collider))]
public class KikoTask27Trigger : MonoBehaviour
{
    [Tooltip("BaybayinManager instance to notify when Task27 should run. Assign in inspector or leave null to FindObjectOfType.")]
    public BaybayinManager baybayinManager;

    [Tooltip("UI prompt manager that shows/hides the press-E prompt. Assign in inspector or leave null to FindObjectOfType.")]
    public ItemPromptManager itemPromptManager;

    [Tooltip("Task/trigger id that must be active in BaybayinManager before this interaction is usable.")]
    public string requiredTaskTrigger = "Babaylan7";

    [Tooltip("Text shown when usable.")]
    public string usablePrompt = "Press E to use";

    [Tooltip("If true, the interaction will only work once.")]
    public bool triggerOnce = true;

    [Tooltip("Automatically hide the prompt and disable this GameObject after triggering.")]
    public bool disableAfterTrigger = true;

    [Tooltip("Optional DayNightCycle reference. If assigned, we'll advance time similar to bed flow (SetTimeOfDay(2f,10f)).")]
    public DayNightCycle DNC;

    [Tooltip("Enable debug logging.")]
    public bool debugLogs = false;

    // runtime
    bool playerInRange = false;
    bool hasTriggered = false;
    Collider playerCollider = null;
    bool promptShown = false;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col == null)
            Debug.LogWarning($"[KikoTask27Trigger:{name}] No Collider found (expected isTrigger).");
        else if (!col.isTrigger)
            Debug.LogWarning($"[KikoTask27Trigger:{name}] Collider is not set to isTrigger. Recommended: set to isTrigger.");

        if (baybayinManager == null)
            baybayinManager = FindObjectOfType<BaybayinManager>();

        if (itemPromptManager == null)
            itemPromptManager = FindObjectOfType<ItemPromptManager>();

        if (DNC == null)
            DNC = FindObjectOfType<DayNightCycle>();
    }

    void OnTriggerEnter(Collider other)
    {
        // NOTE: intentionally NOT using tag-based checks — any collider entering will count as "in range".
        if (hasTriggered && triggerOnce) return;

        playerInRange = true;
        playerCollider = other;

        if (itemPromptManager == null)
            itemPromptManager = FindObjectOfType<ItemPromptManager>();

        // Immediately update prompt visibility based on BaybayinManager state
        UpdatePromptVisibility();
        if (debugLogs) Debug.Log($"[KikoTask27Trigger:{name}] OnTriggerEnter. InRange={playerInRange}, PromptShown={promptShown}");
    }

    void OnTriggerExit(Collider other)
    {
        // mirror OnTriggerEnter — clear state if the same collider left (or simply clear when anything leaves)
        if (!playerInRange) return;

        playerInRange = false;
        playerCollider = null;

        if (itemPromptManager != null && promptShown)
            itemPromptManager.HidePrompt();

        promptShown = false;

        if (debugLogs) Debug.Log($"[KikoTask27Trigger:{name}] OnTriggerExit. Prompt hidden.");
    }

    void Update()
    {
        if (!playerInRange || hasTriggered) return;

        if (itemPromptManager == null)
            itemPromptManager = FindObjectOfType<ItemPromptManager>();

        // Poll BaybayinManager to decide whether to show the prompt while player remains in range
        UpdatePromptVisibility();

        if (Input.GetKeyDown(KeyCode.E))
        {

            // Double-check allowed at the moment of press
            bool allowed = baybayinManager != null && SafeIsTaskTriggered(requiredTaskTrigger);
            if (!allowed)
            {
                if (debugLogs) Debug.Log($"[KikoTask27Trigger:{name}] E pressed but required '{requiredTaskTrigger}' not triggered yet.");
                return;
            }

            // Success — trigger Task27
            TriggerTask27();
        }
    }

    private void UpdatePromptVisibility()
    {
        bool allowed = baybayinManager != null && SafeIsTaskTriggered(requiredTaskTrigger);

        if (allowed && !promptShown)
        {
            itemPromptManager?.ShowPrompt(usablePrompt);
            promptShown = true;
            if (debugLogs) Debug.Log($"[KikoTask27Trigger:{name}] Prompt shown (allowed).");
        }
        else if (!allowed && promptShown)
        {
            itemPromptManager?.HidePrompt();
            promptShown = false;
            if (debugLogs) Debug.Log($"[KikoTask27Trigger:{name}] Prompt hidden (not allowed).");
        }
    }

    // Safe wrapper in case BaybayinManager.IsTaskTriggered throws
    private bool SafeIsTaskTriggered(string id)
    {
        try
        {
            // Assumes BaybayinManager implements IsTaskTriggered(string)
            var mi = baybayinManager?.GetType().GetMethod("IsTaskTriggered", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi != null)
            {
                var result = mi.Invoke(baybayinManager, new object[] { id });
                if (result is bool b) return b;
            }
            else
            {
                // fallback: try IsTaskStarted if available
                mi = baybayinManager?.GetType().GetMethod("IsTaskStarted", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null)
                {
                    var result = mi.Invoke(baybayinManager, new object[] { id });
                    if (result is bool b2) return b2;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[KikoTask27Trigger:{name}] Exception calling IsTaskTriggered/IsTaskStarted: {ex}");
        }
        return false;
    }

    void TriggerTask27()
    {
        if (debugLogs) Debug.Log($"[KikoTask27Trigger:{name}] Triggering Task27 interaction.");

        if (baybayinManager != null)
        {
            // try direct call first
            try
            {
                var mi = baybayinManager.GetType().GetMethod("Task27", BindingFlags.Instance | BindingFlags.Public);
                if (mi != null)
                {
                    mi.Invoke(baybayinManager, null); // call public method via reflection
                    if (debugLogs) Debug.Log($"[KikoTask27Trigger:{name}] Called BaybayinManager.Task27() via reflection (public).");
                }
                else
                {
                    // try non-public or different binding
                    mi = baybayinManager.GetType().GetMethod("Task27", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (mi != null && mi.GetParameters().Length == 0)
                    {
                        mi.Invoke(baybayinManager, null);
                        if (debugLogs) Debug.Log($"[KikoTask27Trigger:{name}] Invoked BaybayinManager.Task27() via reflection (non-public).");
                    }
                    else
                    {
                        Debug.LogWarning($"[KikoTask27Trigger:{name}] BaybayinManager does not appear to have a parameterless Task27() method.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KikoTask27Trigger:{name}] Exception invoking Task27 on BaybayinManager: {ex}");
            }
        }
        else
        {
            if (debugLogs) Debug.Log("[KikoTask27Trigger] No BaybayinManager assigned to notify.");
        }

        // Also move/advance DNC like the bed flow (best-effort)
        if (DNC != null)
        {
            try
            {
                DNC.SetTimeOfDay(2f, 10f);
                if (debugLogs) Debug.Log($"[KikoTask27Trigger:{name}] DNC.SetTimeOfDay(2f,10f) called.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KikoTask27Trigger:{name}] Exception calling DNC.SetTimeOfDay: {ex}");
            }
        }

        // hide prompt
        if (itemPromptManager != null && promptShown)
            itemPromptManager.HidePrompt();

        promptShown = false;
        hasTriggered = true;

        if (disableAfterTrigger)
            gameObject.SetActive(false);
    }

    // Optional: allow external code to re-arm this trigger
    public void ResetTrigger()
    {
        hasTriggered = false;
        if (itemPromptManager != null && promptShown)
        {
            itemPromptManager.HidePrompt();
            promptShown = false;
        }
    }
}

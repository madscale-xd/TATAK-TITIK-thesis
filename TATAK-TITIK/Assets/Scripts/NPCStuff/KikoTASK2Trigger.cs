using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Reflection;

/// <summary>
/// Bed interaction trigger: when player is in range and presses E,
/// call BaybayinManager.MarkTask2Completed() — but only if baybayinManager.IsTaskTriggered(requiredTaskTrigger) returns true.
/// No DialogueEventsManager. No tag-based checks. Polls BaybayinManager while in range to show/hide prompt.
/// Attach to the Bed GameObject (Collider must be isTrigger = true).
/// </summary>
[RequireComponent(typeof(Collider))]
public class KikoTask2Trigger : MonoBehaviour
{
    [Tooltip("BaybayinManager instance to notify when Task2 completes. Assign in inspector or leave null to FindObjectOfType.")]
    public BaybayinManager baybayinManager;

    [Tooltip("UI prompt manager that shows/hides the press-E prompt. Assign in inspector or leave null to FindObjectOfType.")]
    public ItemPromptManager itemPromptManager;

    [Tooltip("Task/trigger id that must be active in BaybayinManager before this bed can be used.")]
    public string requiredTaskTrigger = "Babaylan4";

    [Tooltip("Text shown when bed is usable.")]
    public string usablePrompt = "Press E to sleep";

    [Tooltip("If true, the bed interaction will only work once.")]
    public bool triggerOnce = true;

    [Tooltip("Automatically hide the prompt and disable this GameObject after triggering.")]
    public bool disableAfterTrigger = true;

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
            Debug.LogWarning($"[KikoTask2Trigger:{name}] No Collider found (expected isTrigger).");
        else if (!col.isTrigger)
            Debug.LogWarning($"[KikoTask2Trigger:{name}] Collider is not set to isTrigger. Recommended: set to isTrigger.");

        if (baybayinManager == null)
            baybayinManager = FindObjectOfType<BaybayinManager>();

        if (itemPromptManager == null)
            itemPromptManager = FindObjectOfType<ItemPromptManager>();
    }

    void OnTriggerEnter(Collider other)
    {
        // intentionally NOT using tag checks
        if (hasTriggered && triggerOnce) return;

        playerInRange = true;
        playerCollider = other;

        if (itemPromptManager == null)
            itemPromptManager = FindObjectOfType<ItemPromptManager>();

        // Immediately update prompt visibility based on BaybayinManager state
        UpdatePromptVisibility();
        if (debugLogs) Debug.Log($"[KikoTask2Trigger:{name}] OnTriggerEnter. InRange={playerInRange}, PromptShown={promptShown}");
    }

    void OnTriggerExit(Collider other)
    {
        if (!playerInRange) return;

        playerInRange = false;
        playerCollider = null;

        if (itemPromptManager != null && promptShown)
            itemPromptManager.HidePrompt();

        promptShown = false;

        if (debugLogs) Debug.Log($"[KikoTask2Trigger:{name}] OnTriggerExit. Prompt hidden.");
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
                if (debugLogs) Debug.Log($"[KikoTask2Trigger:{name}] E pressed but required '{requiredTaskTrigger}' not triggered yet.");
                return;
            }

            // Success — mark task 2 completed
            TriggerTask2Complete();
        }
    }

    private void UpdatePromptVisibility()
    {
        bool allowed = baybayinManager != null && SafeIsTaskTriggered(requiredTaskTrigger);

        if (allowed && !promptShown)
        {
            itemPromptManager?.ShowPrompt(usablePrompt);
            promptShown = true;
            if (debugLogs) Debug.Log($"[KikoTask2Trigger:{name}] Prompt shown (allowed).");
        }
        else if (!allowed && promptShown)
        {
            itemPromptManager?.HidePrompt();
            promptShown = false;
            if (debugLogs) Debug.Log($"[KikoTask2Trigger:{name}] Prompt hidden (not allowed).");
        }
    }

    // Safe wrapper in case BaybayinManager.IsTaskTriggered throws
    private bool SafeIsTaskTriggered(string id)
    {
        try
        {
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
            Debug.LogWarning($"[KikoTask2Trigger:{name}] Exception calling IsTaskTriggered/IsTaskStarted: {ex}");
        }
        return false;
    }

    void TriggerTask2Complete()
    {
        if (debugLogs) Debug.Log($"[KikoTask2Trigger:{name}] Triggering Task2 complete.");

        if (baybayinManager != null)
        {
            try
            {
                // try to call MarkTask2Completed (public or non-public) via reflection
                var mi = baybayinManager.GetType().GetMethod("MarkTask2Completed", BindingFlags.Instance | BindingFlags.Public);
                if (mi != null)
                {
                    mi.Invoke(baybayinManager, null);
                    if (debugLogs) Debug.Log($"[KikoTask2Trigger:{name}] Called BaybayinManager.MarkTask2Completed() via reflection (public).");
                }
                else
                {
                    mi = baybayinManager.GetType().GetMethod("MarkTask2Completed", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (mi != null && mi.GetParameters().Length == 0)
                    {
                        mi.Invoke(baybayinManager, null);
                        if (debugLogs) Debug.Log($"[KikoTask2Trigger:{name}] Invoked BaybayinManager.MarkTask2Completed() via reflection (non-public).");
                    }
                    else
                    {
                        Debug.LogWarning($"[KikoTask2Trigger:{name}] BaybayinManager does not appear to have a parameterless MarkTask2Completed() method.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KikoTask2Trigger:{name}] Exception calling MarkTask2Completed on BaybayinManager: {ex}");
            }
        }
        else
        {
            if (debugLogs) Debug.Log("[KikoTask2Trigger] No BaybayinManager assigned to notify.");
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

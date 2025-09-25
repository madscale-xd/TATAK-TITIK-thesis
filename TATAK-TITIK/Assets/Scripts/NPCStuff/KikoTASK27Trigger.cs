using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Collections;
using System;

/// <summary>
/// Bed-like interaction trigger: when player is in range and presses E,
/// call BaybayinManager.Task27() — but only if a required NPC ID
/// (default "Babaylan7") is already in DialogueEventsManager's triggered set.
///
/// Attach to the interactive GameObject (Collider must be isTrigger = true).
/// </summary>
[RequireComponent(typeof(Collider))]
public class KikoTask27Trigger : MonoBehaviour
{
    [Tooltip("BaybayinManager instance to notify when Task27 should run. Assign in inspector or leave null to FindObjectOfType.")]
    public BaybayinManager baybayinManager;

    [Tooltip("UI prompt manager that shows/hides the press-E prompt. Assign in inspector or leave null to FindObjectOfType.")]
    public ItemPromptManager itemPromptManager;

    [Tooltip("NPC ID that must be triggered before this interaction can be used.")]
    public string requiredTriggeredNPCID = "Babaylan7";

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

    DialogueEventsManager dem => DialogueEventsManager.Instance;

    private void OnEnable()
    {
        if (DialogueEventsManager.Instance != null)
            DialogueEventsManager.Instance.OnTriggeredAdded += HandleDemTriggered;
    }

    private void OnDisable()
    {
        if (DialogueEventsManager.Instance != null)
            DialogueEventsManager.Instance.OnTriggeredAdded -= HandleDemTriggered;
    }

    private void HandleDemTriggered(string npcId)
    {
        // if the required NPC just triggered and player is in range, show prompt
        if (!playerInRange) return;
        if (string.Equals(npcId, requiredTriggeredNPCID, System.StringComparison.OrdinalIgnoreCase))
        {
            if (itemPromptManager == null) itemPromptManager = FindObjectOfType<ItemPromptManager>();
            if (itemPromptManager != null)
                itemPromptManager.ShowPrompt(usablePrompt);
            if (debugLogs) Debug.Log($"[KikoTask27Trigger:{name}] DEM triggered '{npcId}' while player in range -> prompt shown.");
        }
    }

    void Update()
    {
        if (!playerInRange || hasTriggered) return;

        if (itemPromptManager == null)
            itemPromptManager = FindObjectOfType<ItemPromptManager>();

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            var demLocal = DialogueEventsManager.Instance;
            if (demLocal == null)
            {
                Debug.LogWarning("[KikoTask27Trigger] DialogueEventsManager.Instance is null on E press.");
                return;
            }

            if (!demLocal.IsTriggered(requiredTriggeredNPCID))
            {
                if (debugLogs) Debug.Log($"[KikoTask27Trigger:{name}] E pressed but required '{requiredTriggeredNPCID}' not triggered.");
                // Optional: give feedback to the player
                // itemPromptManager?.ShowTemporaryMessage("You can't do that yet.");
                return;
            }

            // Success — trigger Task27 flow
            TriggerTask27();
        }
    }

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
        if (!other.CompareTag("Player")) return;
        // already used and configured to only allow once -> do nothing
        if (hasTriggered && triggerOnce) return;

        playerInRange = true;
        playerCollider = other;

        // ensure itemPromptManager reference
        if (itemPromptManager == null)
            itemPromptManager = FindObjectOfType<ItemPromptManager>();

        // Check DEM for required NPC id and show prompt only when allowed
        var demLocal = DialogueEventsManager.Instance;
        bool requiredTriggered = demLocal != null && demLocal.IsTriggered(requiredTriggeredNPCID);

        if (debugLogs) Debug.Log($"[KikoTask27Trigger:{name}] Player entered. RequiredTriggered={requiredTriggered}");

        if (requiredTriggered && !hasTriggered)
        {
            if (itemPromptManager != null)
                itemPromptManager.ShowPrompt(usablePrompt);
        }
        // If not requiredTriggered yet, we still keep playerInRange = true and rely on HandleDemTriggered to show prompt later
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (!playerInRange) return;

        playerInRange = false;
        playerCollider = null;

        if (itemPromptManager != null)
            itemPromptManager.HidePrompt();
    }

    void TriggerTask27()
    {
        if (debugLogs) Debug.Log($"[KikoTask27Trigger:{name}] Triggering Task27 interaction.");

        // Call BayMan.Task27() if available
        if (baybayinManager != null)
        {
            try
            {
                // Prefer calling Task27 method directly if it exists
                var mi = baybayinManager.GetType().GetMethod("Task27", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (mi != null && mi.GetParameters().Length == 0)
                {
                    mi.Invoke(baybayinManager, null);
                }
                else
                {
                    // fallback to trying a strongly-typed call if method exists
                    try { baybayinManager.Task27(); }
                    catch (Exception) { /* silent: method might be non-public or missing - already attempted via reflection */ }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[KikoTask27Trigger:{name}] Exception calling Task27 on BaybayinManager: {ex}");
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
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[KikoTask27Trigger:{name}] Exception calling DNC.SetTimeOfDay: {ex}");
            }
        }

        // hide prompt
        if (itemPromptManager != null)
            itemPromptManager.HidePrompt();

        hasTriggered = true;

        if (disableAfterTrigger)
            gameObject.SetActive(false);
    }
}

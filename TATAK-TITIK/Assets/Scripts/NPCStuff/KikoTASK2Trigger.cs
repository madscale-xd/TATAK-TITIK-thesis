using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Collections;
using System;

/// <summary>
/// Bed-interaction trigger: when player is in range and presses E,
/// call BaybayinManager.MarkTask2Completed() — but only if a required NPC ID
/// (default "Babaylan4") is already in DialogueEventsManager's triggered set.
///
/// Attach to the Bed GameObject (Collider must be isTrigger = true).
/// </summary>
[RequireComponent(typeof(Collider))]
public class KikoTask2Trigger : MonoBehaviour
{
    [Tooltip("BaybayinManager instance to notify when Task2 completes. Assign in inspector or leaves null to FindObjectOfType.")]
    public BaybayinManager baybayinManager;

    [Tooltip("UI prompt manager that shows/hides the press-E prompt. Assign in inspector or leave null to FindObjectOfType.")]
    public ItemPromptManager itemPromptManager;

    [Tooltip("NPC ID that must be triggered before this bed can be used.")]
    public string requiredTriggeredNPCID = "Babaylan4";

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
        // if the required NPC just triggered and player is in the bed range, show prompt
        if (!playerInRange) return;
        if (string.Equals(npcId, requiredTriggeredNPCID, StringComparison.OrdinalIgnoreCase))
        {
            if (itemPromptManager == null) itemPromptManager = FindObjectOfType<ItemPromptManager>();
            if (itemPromptManager != null)
                itemPromptManager.ShowPrompt(usablePrompt);
            if (debugLogs) Debug.Log($"[KikoTask2Trigger:{name}] DEM triggered '{npcId}' while player in range -> prompt shown.");
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
                Debug.LogWarning("[KikoTask2Trigger] DialogueEventsManager.Instance is null on E press.");
                return;
            }

            if (!demLocal.IsTriggered(requiredTriggeredNPCID))
            {
                if (debugLogs) Debug.Log($"[KikoTask2Trigger:{name}] E pressed but required '{requiredTriggeredNPCID}' not triggered.");
                // Optional: give feedback to the player
                // itemPromptManager?.ShowTemporaryMessage("You can't sleep yet.");
                return;
            }

            // Success — mark task 2 completed
            TriggerTask2Complete();
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
            Debug.LogWarning($"[KikoTask2Trigger:{name}] No Collider found (expected isTrigger).");
        else if (!col.isTrigger)
            Debug.LogWarning($"[KikoTask2Trigger:{name}] Collider is not set to isTrigger. Recommended: set to isTrigger.");

        if (baybayinManager == null)
            baybayinManager = FindObjectOfType<BaybayinManager>();

        if (itemPromptManager == null)
            itemPromptManager = FindObjectOfType<ItemPromptManager>();
    }

   // Replace OnTriggerEnter with this version
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

        if (debugLogs) Debug.Log($"[KikoTask2Trigger:{name}] Player entered. RequiredTriggered={requiredTriggered}");

        if (requiredTriggered && !hasTriggered)
        {
            if (itemPromptManager != null)
                itemPromptManager.ShowPrompt(usablePrompt);
        }
        // If not requiredTriggered yet, we still keep playerInRange = true and rely on HandleDemTriggered to show prompt later
    }

    // Replace OnTriggerExit to always clear state
    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (!playerInRange) return;

        playerInRange = false;
        playerCollider = null;

        if (itemPromptManager != null)
            itemPromptManager.HidePrompt();
    }

    void TriggerTask2Complete()
    {
        if (debugLogs) Debug.Log($"[KikoTask2Trigger:{name}] Triggering Task2 complete.");

        // Notify BaybayinManager (if assigned)
        if (baybayinManager != null)
        {
            try
            {
                baybayinManager.MarkTask2Completed();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[KikoTask2Trigger:{name}] Exception calling MarkTask2Completed: {ex}");
            }
        }
        else
        {
            if (debugLogs) Debug.Log("[KikoTask2Trigger] No BaybayinManager assigned to notify.");
        }

        // hide prompt
        if (itemPromptManager != null)
            itemPromptManager.HidePrompt();

        hasTriggered = true;
    }
}

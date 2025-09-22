using UnityEngine;
using UnityEngine.EventSystems;

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
        // already used and configured to only allow once -> do nothing
        if (hasTriggered && triggerOnce) return;

        if (!other.CompareTag("Player")) return;

        // Check DEM for required NPC id
        if (dem == null)
        {
            Debug.LogWarning("[KikoTask2Trigger] DialogueEventsManager.Instance is null. Cannot verify required trigger.");
            return;
        }

        bool requiredTriggered = dem.IsTriggered(requiredTriggeredNPCID);

        if (debugLogs) Debug.Log($"[KikoTask2Trigger:{name}] Player entered. RequiredTriggered={requiredTriggered}");

        if (requiredTriggered && !hasTriggered)
        {
            playerInRange = true;
            playerCollider = other;

            // show the prompt
            if (itemPromptManager != null)
                itemPromptManager.ShowPrompt(usablePrompt);
        }
        else
        {
            // no prompt shown if requirement not met (keeps UX clean)
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (!playerInRange) return;

        playerInRange = false;
        playerCollider = null;

        // hide the prompt
        if (itemPromptManager != null)
            itemPromptManager.HidePrompt();
    }

    void Update()
    {
        if (!playerInRange || hasTriggered) return;

        // only respond to E when the player is in range, E is pressed, and not clicking over UI
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            // re-check requirement in case DEM changed while in range
            if (dem == null)
            {
                Debug.LogWarning("[KikoTask2Trigger] DialogueEventsManager.Instance is null on E press.");
                return;
            }

            if (!dem.IsTriggered(requiredTriggeredNPCID))
            {
                if (debugLogs) Debug.Log($"[KikoTask2Trigger:{name}] E pressed but required '{requiredTriggeredNPCID}' not triggered.");
                return;
            }

            // Success — mark task 2 completed (BaybayinManager will handle advancing time / other consequences)
            TriggerTask2Complete();
        }
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

        if (disableAfterTrigger)
            gameObject.SetActive(false);
    }
}

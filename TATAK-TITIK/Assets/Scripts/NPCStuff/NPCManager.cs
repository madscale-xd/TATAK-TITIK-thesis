using UnityEngine;

/// <summary>
/// Per-NPC manager that centralizes common NPC operations for a single NPC instance.
/// Assign the referenced components in the inspector (or let them be null and the manager will warn).
/// </summary>
public class NPCManager : MonoBehaviour
{
    [Header("Core singletons / references")]
    [Tooltip("Optional: assign the DialogueEventsManager here. If left null, DialogueEventsManager.Instance will be used.")]
    public DialogueEventsManager dem;

    [Header("Per-NPC components (assign these on the NPC)")]
    [Tooltip("The NavMeshNPCController that moves this NPC.")]
    public NavMeshNPCController navController;

    [Tooltip("The NPCDialogueTrigger component that stores npcID and dialogue lines.")]
    public NPCDialogueTrigger dialogueTrigger;

    [Tooltip("The JournalTrigger component for this NPC (if any).")]
    public JournalTrigger journalTrigger;

    [Tooltip("The NPCTracker component that handles floating text / tracking.")]
    public NPCTracker tracker;

    private void Awake()
    {
        // fallback to the singleton if the inspector slot is empty
        if (dem == null)
            dem = DialogueEventsManager.Instance;
    }

    // -------------------------
    // Movement / destination API
    // -------------------------
    public void SetDestination(Vector3 worldPos)
    {
        if (navController == null)
        {
            Debug.LogWarning($"[NPCManager:{name}] SetDestination called but navController is null.");
            return;
        }
        navController.MoveTo(worldPos);
    }

    public void SetDestination(Transform target)
    {
        if (navController == null)
        {
            Debug.LogWarning($"[NPCManager:{name}] SetDestination(target) called but navController is null.");
            return;
        }
        if (target == null)
        {
            Debug.LogWarning($"[NPCManager:{name}] SetDestination(target) called with null target.");
            return;
        }
        navController.MoveTo(target);
    }

    public void StopMoving()
    {
        if (navController == null) return;
        navController.StopMoving();
    }

    public void ResumeMoving()
    {
        if (navController == null) return;
        navController.ResumeMoving();
    }

    // -------------------------
    // Dialogue ID / lines API
    // -------------------------
    /// <summary>
    /// Change this NPC's id. Prefers to call DEM.ChangeNPCName (using this GameObject.name) if DEM exists.
    /// Falls back to updating the NPCDialogueTrigger directly.
    /// Returns true if an id change was applied.
    /// </summary>
    public bool ChangeNPCID(string newNPCID, bool moveTriggeredState = false)
    {
        if (string.IsNullOrWhiteSpace(newNPCID))
        {
            Debug.LogWarning($"[NPCManager:{name}] ChangeNPCID called with empty newNPCID.");
            return false;
        }

        // Prefer DEM if available
        DialogueEventsManager useDem = dem ?? DialogueEventsManager.Instance;
        if (useDem != null)
        {
            bool changed = useDem.ChangeNPCName(gameObject.name, newNPCID, moveTriggeredState);
            if (changed)
            {
                Debug.Log($"[NPCManager:{name}] DEM changed id to '{newNPCID}'.");
                return true;
            }
        }

        // Fallback: change ID on component
        if (dialogueTrigger != null)
        {
            dialogueTrigger.SetNPCID(newNPCID);
            Debug.Log($"[NPCManager:{name}] Fallback: SetNPCID to '{newNPCID}' on NPCDialogueTrigger.");
            return true;
        }

        Debug.LogWarning($"[NPCManager:{name}] ChangeNPCID: Could not change ID (no DEM success, no NPCDialogueTrigger).");
        return false;
    }

    /// <summary>
    /// Update the dialogue lines on this NPC's NPCDialogueTrigger (no DEM involvement).
    /// </summary>
    public void SetDialogueLines(string[] newLines)
    {
        if (dialogueTrigger == null)
        {
            Debug.LogWarning($"[NPCManager:{name}] SetDialogueLines called but dialogueTrigger is null.");
            return;
        }
        dialogueTrigger.SetDialogueLines(newLines);
    }

    // -------------------------
    // Journal API
    // -------------------------
    public void SetJournalEntries(JournalTriggerEntry[] newEntries)
    {
        if (journalTrigger == null)
        {
            Debug.LogWarning($"[NPCManager:{name}] SetJournalEntries called but journalTrigger is null.");
            return;
        }
        journalTrigger.SetEntries(newEntries);
    }

    public void SetJournalSingleEntry(string key, string display)
    {
        if (journalTrigger == null)
        {
            Debug.LogWarning($"[NPCManager:{name}] SetJournalSingleEntry called but journalTrigger is null.");
            return;
        }
        journalTrigger.SetSingleEntry(key, display);
    }

    /// <summary>
    /// Immediately add this NPC's journal entries to the JournalManager (if assigned).
    /// </summary>
    public void AddJournalEntries()
    {
        if (journalTrigger == null)
        {
            Debug.LogWarning($"[NPCManager:{name}] AddJournalEntries called but journalTrigger is null.");
            return;
        }
        journalTrigger.AddEntryToJournal();
    }

    // -------------------------
    // Tracker API (floating text)
    // -------------------------
    public void SetTrackerText(string text, bool showImmediately = false, float duration = 0f)
    {
        if (tracker == null)
        {
            Debug.LogWarning($"[NPCManager:{name}] SetTrackerText called but tracker is null.");
            return;
        }

        // NPCTracker has SetFloatingText wrapper -> forwards to SetText
        tracker.SetFloatingText(text, showImmediately, duration);
    }

    public void ClearTrackerText()
    {
        if (tracker == null) return;
        tracker.ClearText();
    }

    // -------------------------
    // Utility / getters
    // -------------------------
    public string GetNPCID()
    {
        return dialogueTrigger != null ? dialogueTrigger.GetNPCID() : "";
    }
}

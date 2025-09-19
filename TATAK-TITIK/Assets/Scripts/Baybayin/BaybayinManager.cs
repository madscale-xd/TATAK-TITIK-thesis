using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class BaybayinManager : MonoBehaviour
{
    [Header("Assign the NPC manager for Kiko (the NPC who speaks)")]
    public NPCManager Kiko;

    [Header("Where should Kiko walk after finishing dialogue?")]
    [Tooltip("World-space Transform destination for Kiko.")]
    public Transform moveTarget;

    [Header("Optional: Only respond to a specific NPC ID when using OnDialogueFinished(string)")]
    [Tooltip("If true, OnDialogueFinished(string npcID) will only act when the id matches Kiko's dialogueTrigger id.")]
    public bool requireIdMatch = true;

    [Tooltip("Optional UnityEvent invoked after Kiko is ordered to move (useful for inspector wiring).")]
    public UnityEvent onMoved;

    // cached DialogueEventsManager reference used for subscribing/unsubscribing
    private DialogueEventsManager demRef;

    private void OnEnable()
    {
        // Prefer singleton instance; fall back to scene search if instance not yet set
        demRef = DialogueEventsManager.Instance;
        if (demRef == null)
            demRef = FindObjectOfType<DialogueEventsManager>();

        if (demRef != null)
        {
            demRef.OnTriggeredAdded += HandleTriggeredAdded;
        }
        else
        {
            Debug.LogWarning("[BaybayinManager] DialogueEventsManager not found. Will attempt to subscribe when it appears.");
            // We'll attempt to find & subscribe in Update if it was missing (optional)
            StartCoroutine(DeferredSubscribe());
        }
    }

    private IEnumerator DeferredSubscribe()
    {
        // try a few frames to find the DEM if it's created after this manager
        int tries = 0;
        while (demRef == null && tries < 30)
        {
            demRef = DialogueEventsManager.Instance ?? FindObjectOfType<DialogueEventsManager>();
            if (demRef != null) break;
            tries++;
            yield return null;
        }

        if (demRef != null)
        {
            demRef.OnTriggeredAdded += HandleTriggeredAdded;
            Debug.Log("[BaybayinManager] Subscribed to DialogueEventsManager.OnTriggeredAdded (deferred).");
        }
        else
        {
            Debug.LogWarning("[BaybayinManager] Could not find DialogueEventsManager to subscribe to OnTriggeredAdded.");
        }
    }

    private void OnDisable()
    {
        if (demRef != null)
        {
            demRef.OnTriggeredAdded -= HandleTriggeredAdded;
            demRef = null;
        }
        StopAllCoroutines(); // stop DeferredSubscribe if running
    }

    // Event handler for DialogueEventsManager.OnTriggeredAdded
    private void HandleTriggeredAdded(string npcID)
    {
        // Run the same logic as OnDialogueFinished(npcID)
        // If requireIdMatch is true, compare against Kiko's dialogueTrigger id
        if (Kiko == null)
        {
            Debug.LogWarning("[BaybayinManager] HandleTriggeredAdded called but Kiko is not assigned.");
            return;
        }

        if (requireIdMatch)
        {
            var trigger = Kiko.dialogueTrigger;
            if (trigger == null)
            {
                Debug.LogWarning("[BaybayinManager] requireIdMatch is true but Kiko.dialogueTrigger is null. Ignoring trigger.");
                return;
            }

            if (!string.Equals(trigger.GetNPCID(), npcID))
            {
                // not the NPC we're watching — ignore
                return;
            }
        }

        // If we reach here: either requireIdMatch==false (respond to any), or npcID matches Kiko
        MoveKikoToTarget();
    }

    // -----------------------
    // Public API - Call these when dialogue finishes
    // -----------------------

    /// <summary>
    /// Call this directly when Kiko's dialogue finishes.
    /// Example: have your dialogue finish callback call BaybayinManagerInstance.OnKikoDialogueFinished().
    /// </summary>
    public void OnKikoDialogueFinished()
    {
        MoveKikoToTarget();
    }

    /// <summary>
    /// Call this when any NPC's dialogue finishes and you receive the NPC id.
    /// If requireIdMatch is true, this will only move Kiko when npcID matches Kiko's dialogueTrigger id.
    /// </summary>
    /// <param name="npcID">the finished NPC's id (e.g. from DialogueEventsManager or your dialogue system)</param>
    public void OnDialogueFinished(string npcID)
    {
        if (Kiko == null)
        {
            Debug.LogWarning("[BaybayinManager] OnDialogueFinished called but Kiko is not assigned.");
            return;
        }

        if (requireIdMatch)
        {
            // try to compare with the id stored on Kiko's dialogueTrigger (if available)
            var trigger = Kiko.dialogueTrigger;
            if (trigger == null)
            {
                Debug.LogWarning("[BaybayinManager] requireIdMatch is true but Kiko.dialogueTrigger is null. Falling back to no-op.");
                return;
            }

            if (!string.Equals(trigger.GetNPCID(), npcID))
            {
                // not intended NPC — ignore
                return;
            }
        }

        MoveKikoToTarget();
    }

    // -----------------------
    // Internal
    // -----------------------
    void MoveKikoToTarget()
    {
        if (Kiko == null)
        {
            Debug.LogWarning("[BaybayinManager] MoveKikoToTarget: Kiko (NPCManager) is not assigned.");
            return;
        }

        if (moveTarget == null)
        {
            Debug.LogWarning("[BaybayinManager] MoveKikoToTarget: moveTarget is not assigned in the inspector.");
            return;
        }

        // use NPCManager API to command movement
        Kiko.SetDestination(moveTarget);

        // invoke inspector hook if desired
        onMoved?.Invoke();

        Debug.Log($"[BaybayinManager] Told '{Kiko.name}' to move to '{moveTarget.name}' ({moveTarget.position}).");
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


/// <summary>
/// Per-NPC manager that centralizes common NPC operations for a single NPC instance.
/// Assign the referenced components in the inspector (or let them be null and the manager will warn).
/// NOTE: destination APIs now use Transform only (no Vector3 overloads).
/// </summary>
public class NPCManager : MonoBehaviour
{
    private Queue<Transform> destinationQueue = new Queue<Transform>();
    private bool processingDestinations = false;
    // add near the other fields
    private Coroutine rotateCoroutine = null;
    // Rotation lock state (add near other private fields)
    private bool rotationLocked = false;
    private Transform rotationLockTarget = null;
    private bool rotationLockOnlyYAxis = true;
    private float lockRotationSpeed = 720f; // deg/sec used when smoothing toward locked rotation
    private float savedNavRotationSpeed = float.NaN;
    private bool hasSavedNavRotationSpeed = false;

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

    // enforce the locked rotation every frame while rotationLocked is true
    private void LateUpdate()
    {
        if (!rotationLocked || rotationLockTarget == null) return;

        // choose which transform to rotate (same fallback logic you used elsewhere)
        Transform modelVisual = (navController != null && navController.modelRoot != null) ? navController.modelRoot : this.transform;

        Vector3 dir = rotationLockTarget.position - modelVisual.position;
        if (rotationLockOnlyYAxis) dir.y = 0f;
        if (dir.sqrMagnitude <= 1e-6f) return;

        Quaternion desired = Quaternion.LookRotation(dir.normalized, Vector3.up);

        // smooth toward desired rotation using lockRotationSpeed; use RotateTowards for stable deg/sec
        float step = lockRotationSpeed * Time.deltaTime;
        modelVisual.rotation = Quaternion.RotateTowards(modelVisual.rotation, desired, step);
    }
    // -------------------------
    // Movement / destination API (TRANSFORM-ONLY)
    // -------------------------
    /// <summary>
    /// Immediately set the NPC to move to the given Transform target.
    /// </summary>
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

    /// <summary>
    /// Convenience: either enqueue the transform or immediately override current destination.
    /// </summary>
    public void SetDestination(Transform target, bool queueInsteadOfOverride)
    {
        if (queueInsteadOfOverride)
            EnqueueDestination(target);
        else
            SetDestination(target);
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

    private void OnEnable()
    {
        if (navController != null)
            navController.OnDestinationReached += HandleNavReached;
    }

    private void OnDisable()
    {
        if (navController != null)
            navController.OnDestinationReached -= HandleNavReached;
    }

    // Public API: enqueue a Transform instead of overriding
    public void EnqueueDestination(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning($"[NPCManager:{name}] EnqueueDestination called with null target. Ignoring.");
            return;
        }

        destinationQueue.Enqueue(target);
        if (!processingDestinations)
            StartCoroutine(ProcessDestinationQueue());
    }

    private IEnumerator ProcessDestinationQueue()
    {
        processingDestinations = true;

        while (destinationQueue.Count > 0)
        {
            Transform next = destinationQueue.Dequeue();

            if (navController == null)
            {
                Debug.LogWarning($"[NPCManager:{name}] ProcessDestinationQueue aborted: navController is null.");
                yield break;
            }

            if (next == null)
            {
                Debug.LogWarning($"[NPCManager:{name}] Skipping null Transform in destination queue.");
                yield return null;
                continue;
            }

            // send the nav controller to the next transform
            navController.MoveTo(next);

            // wait until navController raises the OnDestinationReached event
            bool reached = false;
            System.Action onReached = () => reached = true;
            navController.OnDestinationReached += onReached;

            // safety timeout to avoid forever waits
            float timeout = 30f;
            float elapsed = 0f;
            while (!reached && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            navController.OnDestinationReached -= onReached;
            yield return null;
        }

        processingDestinations = false;
    }

    public void ClearDestinations()
    {
        // e.g. clear your internal destination queue list
        destinationQueue.Clear();
        // stop agent
        var a = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (a != null) { a.ResetPath(); a.isStopped = true; }
    }

    private void HandleNavReached()
    {
        // this is called whenever the nav controller reaches a destination.
        // With the ProcessDestinationQueue coroutine waiting on a local delegate,
        // you may not need to use this method. But it can be used for side-effects.
    }

    /// Ask the scene DialogueManager to play this NPC's dialogue using this NPC's registered NPC ID.
    /// (Wrapper that uses the snapshot behavior implemented below.)
    /// </summary>
    public void PlayDialogue()
    {
        PlayDialogue(GetNPCID());
    }

    /// <summary>
    /// Ask the scene DialogueManager to play dialogue for this NPC object.
    /// This snapshots the NPCDialogueTrigger's current lines at enqueue time and enqueues them
    /// via DialogueManager.PlayDialogueFor(GameObject,string,string[],JournalTriggerEntry[]).
    /// </summary>
    public void PlayDialogue(string overrideNpcID)
    {
        var dm = FindObjectOfType<DialogueManager>();
        if (dm == null)
        {
            Debug.LogWarning($"[NPCManager:{name}] PlayDialogue called but no DialogueManager found in scene.");
            return;
        }

        string idToUse = overrideNpcID;
        if (string.IsNullOrWhiteSpace(idToUse))
            idToUse = dialogueTrigger != null ? dialogueTrigger.GetNPCID() : "";

        if (string.IsNullOrWhiteSpace(idToUse))
        {
            Debug.LogWarning($"[NPCManager:{name}] PlayDialogue: no NPC ID available to record in DialogueEventsManager.");
        }

        // Snapshot lines now so queued items preserve the content even if SetDialogueLines is called later.
        string[] linesSnapshot = null;
        if (dialogueTrigger != null)
        {
            var triggerLines = dialogueTrigger.GetDialogueLines();
            if (triggerLines != null)
                linesSnapshot = (string[])triggerLines.Clone();
        }

        // We don't automatically snapshot journal entries here because NPCManager doesn't expose a getter.
        // If you want journals enqueued with the dialogue, use the overload below that accepts explicit journal entries.
        dm.PlayDialogueFor(this.gameObject, idToUse, linesSnapshot, null);
    }

    /// <summary>
    /// Enqueue dialogue for this NPC with explicit lines and optional journal entries.
    /// Use this when you already have the exact lines and/or journal entries you want queued.
    /// </summary>
    public void PlayDialogue(string overrideNpcID, string[] explicitLines, JournalTriggerEntry[] explicitJournalEntries)
    {
        var dm = FindObjectOfType<DialogueManager>();
        if (dm == null)
        {
            Debug.LogWarning($"[NPCManager:{name}] PlayDialogue(lines+journals) called but no DialogueManager found in scene.");
            return;
        }

        string idToUse = overrideNpcID;
        if (string.IsNullOrWhiteSpace(idToUse))
            idToUse = dialogueTrigger != null ? dialogueTrigger.GetNPCID() : "";

        // Clone snapshots to prevent external mutation before queue processing
        string[] linesSnapshot = explicitLines != null ? (string[])explicitLines.Clone() : null;
        JournalTriggerEntry[] journalSnapshot = explicitJournalEntries != null ? (JournalTriggerEntry[])explicitJournalEntries.Clone() : null;

        dm.PlayDialogueFor(this.gameObject, idToUse, linesSnapshot, journalSnapshot);
    }


    /// <summary>
    /// Lock model rotation to face `target`. The lock persists until ReleaseRotationLock() is called.
    /// - snap: true -> immediately set rotation (no smoothing)
    /// - preventControllerOverride: if true, temporarily sets navController.rotationSpeed = 0 (saved/restored)
    /// - smoothSpeed: degrees/sec used for smoothing while locked (ignored if snap==true)
    /// </summary>
    public void LockRotationToTarget(Transform target, bool onlyYAxis = true, bool snap = true, bool preventControllerOverride = true, float smoothSpeed = 720f)
    {
        if (target == null) return;

        rotationLockTarget = target;
        rotationLockOnlyYAxis = onlyYAxis;
        lockRotationSpeed = Mathf.Max(0.0001f, smoothSpeed);
        rotationLocked = true;

        // optionally stop navController from rotating the model (safer than disabling the whole controller)
        if (preventControllerOverride && navController != null)
        {
            if (!hasSavedNavRotationSpeed)
            {
                savedNavRotationSpeed = navController.rotationSpeed;
                hasSavedNavRotationSpeed = true;
            }
            navController.rotationSpeed = 0f;
        }

        // if snap, immediately set the rotation once (LateUpdate will maintain afterward)
        if (snap)
        {
            Transform modelVisual = (navController != null && navController.modelRoot != null) ? navController.modelRoot : this.transform;
            Vector3 dir = target.position - modelVisual.position;
            if (onlyYAxis) dir.y = 0f;
            if (dir.sqrMagnitude > 1e-6f)
                modelVisual.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }

    /// <summary>
    /// Release any locked rotation, optionally restoring the navController.rotationSpeed.
    /// </summary>
    public void ReleaseRotationLock(bool restoreControllerRotation = true)
    {
        rotationLocked = false;
        rotationLockTarget = null;

        if (restoreControllerRotation && navController != null && hasSavedNavRotationSpeed)
        {
            navController.rotationSpeed = savedNavRotationSpeed;
            savedNavRotationSpeed = float.NaN;
            hasSavedNavRotationSpeed = false;
        }
    }


    /// <summary>
    /// Rotate a model transform once to face a target (one-time call).
    /// If modelVisual is null we try navController.modelRoot then this.transform.
    /// If instant==true the rotation snaps; otherwise it smoothly rotates at rotationSpeed (deg/sec).
    /// </summary>
    public void RotateModelToFace(Transform modelVisual, Transform target, float rotationSpeed = 360f, bool onlyYAxis = true, bool instant = true)
    {
        if (target == null) return;

        // fallback model visual
        if (modelVisual == null)
        {
            if (navController != null && navController.modelRoot != null)
                modelVisual = navController.modelRoot;
            else
                modelVisual = this.transform;
        }

        // cancel any in-progress rotation for this NPC
        if (rotateCoroutine != null)
        {
            StopCoroutine(rotateCoroutine);
            rotateCoroutine = null;
        }

        // compute direction
        Vector3 dir = target.position - modelVisual.position;
        if (onlyYAxis) dir.y = 0f;
        if (dir.sqrMagnitude <= 1e-6f) return; // nothing meaningful to look at

        Quaternion desired = Quaternion.LookRotation(dir.normalized, Vector3.up);

        if (instant || rotationSpeed <= 0f)
        {
            modelVisual.rotation = desired;
        }
        else
        {
            // start a short coroutine to rotate over time (one-time)
            rotateCoroutine = StartCoroutine(RotateOnceCoroutine(modelVisual, desired, rotationSpeed));
        }
    }

        private IEnumerator RotateOnceCoroutine(Transform modelVisual, Quaternion desired, float rotationSpeed)
        {
            if (modelVisual == null) yield break;

            float remainingAngle = Quaternion.Angle(modelVisual.rotation, desired);

            // If already close, snap immediately
            if (remainingAngle <= 0.01f)
            {
                modelVisual.rotation = desired;
                rotateCoroutine = null;
                yield break;
            }

            while (remainingAngle > 0.25f)
            {
                float maxStep = rotationSpeed * Time.deltaTime;
                modelVisual.rotation = Quaternion.RotateTowards(modelVisual.rotation, desired, maxStep);
                remainingAngle = Quaternion.Angle(modelVisual.rotation, desired);
                yield return null;
            }

            // ensure exact final rotation
            if (modelVisual != null) modelVisual.rotation = desired;
            rotateCoroutine = null;
        }
}

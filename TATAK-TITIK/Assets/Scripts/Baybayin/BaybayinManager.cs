using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System;

public class BaybayinManager : MonoBehaviour
{
    [Header("Assign the NPC manager for the NPCs")]
    public NPCManager Kiko;
    public Transform KikoTransform;
    public Transform playerTransform;
    public NPCManager Babaylan;
    public Transform BabaylanTransform;

    [Header("Optional: final move target (not required)")]
    [Tooltip("Optional world-space Transform. If assigned it will be enqueued AFTER the waypoints; otherwise only waypoints are used.")]
    public Transform moveTarget;

    [Header("Optional: Only respond to a specific NPC ID when using OnDialogueFinished(string)")]
    [Tooltip("If true, OnDialogueFinished(string npcID) will only act when the id matches Kiko's dialogueTrigger id.")]
    public bool requireIdMatch = true;

    [Tooltip("Optional UnityEvent invoked after Kiko is ordered to move (useful for inspector wiring).")]
    public UnityEvent onMoved;

    // cached DialogueEventsManager reference used for subscribing/unsubscribing
    private DialogueEventsManager demRef;
    [SerializeField] private DayNightCycle DNC;

    [Header("Task1 (explicit) settings")]
    [Tooltip("The NPC ID that triggers Task1. Example: \"Kiko1\". If empty Task1 won't auto-start from DEM.")]
    public string task1TriggerNPCID = "Kiko1";

    [Tooltip("If true, Task1 will run only once after the trigger. If false, it will run every matching trigger.")]
    public bool task1RunOnlyOnce = true;

    [Tooltip("If true Task1 is enabled; set false to disable Task1 without removing the script.")]
    public bool task1Enabled = true;
    private string pendingTask2KikoID = null;

    // internal tracking
    [HideInInspector] public string taskCompleted = "";
    private bool task1HasRun = false;

    [Header("Waypoints (used by all tasks)")]
    public Transform[] waypoints;

    [Header("New Lines")]
    //Act1
    public string[] Kiko2Lines = new string[] {
        ""
    };
    public string[] Babaylan2Lines = new string[] {
        ""
    };
    public string[] Kiko3Lines = new string[] {
        ""
    };
    public string[] Babaylan3Lines = new string[] {
        ""
    };
    public string[] Kiko4Lines = new string[] {
        ""
    };
    public string[] Babaylan4Lines = new string[] {
        ""
    };
    public string[] Kiko5Lines = new string[] {
        ""
    };
    //Act2
    public string[] Babaylan5Lines = new string[] {
        ""
    };

    [Header("New Journal Entries")]
    JournalTriggerEntry[] Babaylan2Journal = new JournalTriggerEntry[]{
        new JournalTriggerEntry { key = "Mayohan", displayWord = "ᜋᜌᜓᜑᜈ᜔"}
    };
    JournalTriggerEntry[] Kiko4Journal = new JournalTriggerEntry[]{
        new JournalTriggerEntry { key = "doon", displayWord = "ᜇᜓᜂᜈ᜔"},
        new JournalTriggerEntry { key = "puno", displayWord = "ᜉᜓᜈᜓ"}
    };
    JournalTriggerEntry[] Babaylan3Journal = new JournalTriggerEntry[]{
        new JournalTriggerEntry { key = "mainit", displayWord = "ᜋᜁᜈᜒᜆ᜔"},
        new JournalTriggerEntry { key = "bigas", displayWord = "ᜊᜒᜄᜐ᜔"}
    };
    JournalTriggerEntry[] Kiko5Journal = new JournalTriggerEntry[]{
        new JournalTriggerEntry { key = "kaibigan", displayWord = "ᜃᜁᜊᜒᜄᜈ᜔"} };
    JournalTriggerEntry[] Babaylan4Journal = new JournalTriggerEntry[]{
        new JournalTriggerEntry { key = "tulog", displayWord = "ᜆᜓᜎᜓᜄ᜔"},
        new JournalTriggerEntry { key = "bukas", displayWord = "ᜊᜓᜃᜐ᜔"}
    };

    private void OnEnable()
    {
        DNC.SetTimeOfDay(22f, 2f);
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
            StartCoroutine(DeferredSubscribe());
        }
    }

    private IEnumerator DeferredSubscribe()
    {
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
        // If requireIdMatch is true, compare against Kiko's dialogueTrigger id (existing behavior)
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

        // At this point the incoming npcID passed the Kiko check (or requireIdMatch is false).
        // Now check Task1's trigger and run Task1 only if it matches and Task1 hasn't already run (depending on settings).
        TryStartTask1ForTrigger(npcID);
        // If we are waiting for Kiko's new dialogue to be triggered, detect it here and start Task2.
        if (!string.IsNullOrWhiteSpace(pendingTask2KikoID) && string.Equals(pendingTask2KikoID, npcID))
        {
            pendingTask2KikoID = null; // consume the pending flag
            Debug.Log($"[BaybayinManager] Detected trigger for '{npcID}' (Kiko new dialogue). Starting Task2.");
            Task2();
        }
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

        TryStartTask1ForTrigger(npcID);
        // after TryStartTask1ForTrigger(npcID);
        if (!string.IsNullOrWhiteSpace(pendingTask2KikoID) && string.Equals(pendingTask2KikoID, npcID))
        {
            pendingTask2KikoID = null;
            Debug.Log($"[BaybayinManager] OnDialogueFinished detected '{npcID}' (Kiko new dialogue). Starting Task2.");
            Task2();
        }
    }

    /// <summary>
    /// Mark a named task as completed. BaybayinManager knows about "task1" in your case.
    /// This is public so external triggers (waypoints) can notify completion.
    /// </summary>
    public void MarkTaskCompleted(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName)) return;

        // only handle "task1" for now (extend with switch/case if you add more tasks)
        if (taskName == "task1")
        {
            if (taskCompleted == "task1")
            {
                Debug.Log("[BaybayinManager] MarkTaskCompleted: task1 already marked completed.");
                return;
            }

            taskCompleted = "task1";
            if (task1RunOnlyOnce) task1HasRun = true;
            Debug.Log("[BaybayinManager] MarkTaskCompleted: task1 marked completed (via external trigger).");
        }
        else
        {
            Debug.LogWarning($"[BaybayinManager] MarkTaskCompleted: unknown taskName '{taskName}'.");
        }
    }

    public void MarkTask1Completed()
    {
        MarkTaskCompleted("task1");

        // Change Kiko ID and update lines immediately
        Kiko.ChangeNPCID("Kiko2", false);
        Kiko.SetDialogueLines(Kiko2Lines);

        // Instead of starting Task2 now, wait until the player actually talks to Kiko (Kiko2).
        // Store the new npcID so we can start Task2 when DialogueEventsManager reports it triggered.
        var newId = Kiko.GetNPCID();
        if (!string.IsNullOrWhiteSpace(newId))
        {
            pendingTask2KikoID = newId;
            Debug.Log($"[BaybayinManager] Task1 completed — waiting for dialogue for '{newId}' to start Task2.");
        }
        else
        {
            // Fallback: if for some reason ID isn't available, start immediately
            Debug.LogWarning("[BaybayinManager] MarkTask1Completed: Kiko.GetNPCID() returned empty. Starting Task2 immediately as fallback.");
            Task2();
        }
    }

    /// <summary>
    /// Called when the player uses the bed (Task2 completes).
    /// Centralizes post-sleep logic: marks the task as completed and advances the day/time.
    /// </summary>
    public void MarkTask2Completed()
    {
        // prevent double-execution
        if (taskCompleted == "task2")
        {
            Debug.Log("[BaybayinManager] MarkTask2Completed: already completed, ignoring.");
            return;
        }

        // mark done
        taskCompleted = "task2";
        Debug.Log("[BaybayinManager] MarkTask2Completed: task2 marked completed.");

        // If you have a 'run once' semantic for task2 you can set a flag here similar to task1HasRun.
        // (If you add bool task2HasRun; to the class, uncomment the next line)
        // task2HasRun = true;

        // Advance to morning via DayNightCycle (safe null-check)
        if (DNC != null)
        {
            try
            {
                DNC.SetTimeOfDay(8f, 10f);
                Debug.Log("[BaybayinManager] MarkTask2Completed: advanced time to morning (8:00).");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BaybayinManager] MarkTask2Completed: exception calling DNC.SetTimeOfDay: {ex}");
            }
        }
        else
        {
            Debug.LogWarning("[BaybayinManager] MarkTask2Completed: DayNightCycle (DNC) reference is null; cannot advance time.");
        }
    }

    // -----------------------
    // Task1 control
    // -----------------------
    private void TryStartTask1ForTrigger(string incomingNpcID)
    {
        if (!task1Enabled)
            return;

        // If Task1 has a configured trigger NPC id, ensure it matches (if non-empty).
        if (!string.IsNullOrEmpty(task1TriggerNPCID) && !string.Equals(task1TriggerNPCID, incomingNpcID))
        {
            // not the task1 trigger
            return;
        }

        // Respect run-once semantics
        if (task1RunOnlyOnce && task1HasRun)
        {
            Debug.Log("[BaybayinManager] Task1 trigger received but Task1 already ran and is configured to run only once. Ignoring.");
            return;
        }

        // Start Task1
        Task1();
    }

    /// <summary>
    /// The explicit Task1 movement. Only started by TryStartTask1ForTrigger above.
    /// Enqueues waypoints 0,1,5 (skips out-of-range/null). moveTarget is optional and enqueued last if assigned.
    /// </summary>
    void Task1()
    {
        DNC.SetTimeOfDay(23f, 20f);
        if (Kiko == null)
        {
            Debug.LogWarning("[BaybayinManager] Task1: Kiko (NPCManager) is not assigned.");
            return;
        }

        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning("[BaybayinManager] Task1: waypoints array is empty. Nothing to enqueue.");
            return;
        }

        // explicit desired indices: 0,1,5
        int[] desiredIndices = new int[] { 1, 5 };

        int enqueued = 0;
        foreach (int i in desiredIndices)
        {
            if (i < 0 || i >= waypoints.Length)
            {
                Debug.LogWarning($"[BaybayinManager] Task1: waypoint index {i} is out of range (waypoints.Length={waypoints.Length}). Skipping.");
                continue;
            }

            if (waypoints[i] == null)
            {
                Debug.LogWarning($"[BaybayinManager] Task1: waypoint[{i}] is null. Skipping.");
                continue;
            }

            Kiko.EnqueueDestination(waypoints[i]);
            enqueued++;
        }

        // If the user did assign an explicit moveTarget, enqueue it after the waypoints (optional).
        if (moveTarget != null)
        {
            Kiko.EnqueueDestination(moveTarget);
            Debug.Log($"[BaybayinManager] Task1: moveTarget '{moveTarget.name}' enqueued after waypoints.");
        }
        else
        {
            Debug.Log($"[BaybayinManager] Task1: Enqueued {enqueued} waypoint(s) (indices 0,1,5 as available). No moveTarget assigned — done.");
        }

        // invoke inspector hook if desired
        onMoved?.Invoke();

        // mark as run if configured to only run once
        if (task1RunOnlyOnce)
            task1HasRun = true;
    }

    /// <summary>
    /// Public helper: reset Task1 so it can run again (useful for testing or replay).
    /// </summary>
    public void ResetTask1()
    {
        task1HasRun = false;
        if (taskCompleted == "task1") taskCompleted = "";
        Debug.Log("[BaybayinManager] Task1 has been reset and can run again.");
    }

    void Task2()
    {
        Kiko.PlayDialogue("KIKO");
        Kiko.ChangeNPCID("Kiko3", false);
        Kiko.SetDialogueLines(Kiko3Lines);

        Babaylan.AddJournalEntries();
        Babaylan.EnqueueDestination(waypoints[13]);
        Babaylan.PlayDialogue("BABAYLAN");
        Babaylan.ChangeNPCID("Babaylan2", false);
        Babaylan.SetDialogueLines(Babaylan2Lines);
        Babaylan.LockRotationToTarget(KikoTransform, onlyYAxis: true, snap: false, preventControllerOverride: true, smoothSpeed: 360f);

        Kiko.PlayDialogue("KIKO");
        Kiko.LockRotationToTarget(BabaylanTransform, onlyYAxis: true, snap: false, preventControllerOverride: true, smoothSpeed: 360f);
        Kiko.ChangeNPCID("Kiko4", false);
        Kiko.SetDialogueLines(Kiko4Lines);

        Babaylan.LockRotationToTarget(playerTransform, onlyYAxis: true, snap: false, preventControllerOverride: true, smoothSpeed: 360f);
        Babaylan.SetJournalEntries(Babaylan2Journal);
        Babaylan.PlayDialogue("BABAYLAN", Babaylan2Lines, Babaylan2Journal);
        Babaylan.ChangeNPCID("Babaylan3", false);
        Babaylan.SetDialogueLines(Babaylan3Lines);

        Kiko.SetJournalEntries(Kiko4Journal);
        Kiko.PlayDialogue("KIKO", Kiko4Lines, Kiko4Journal);
        Kiko.ChangeNPCID("Kiko5", false);
        Kiko.SetDialogueLines(Kiko5Lines);

        Babaylan.EnqueueDestination(waypoints[14]);
        Babaylan.SetJournalEntries(Babaylan3Journal);
        Babaylan.PlayDialogue("BABAYLAN", Babaylan3Lines, Babaylan3Journal);
        Babaylan.ChangeNPCID("Babaylan4", false);
        Babaylan.SetDialogueLines(Babaylan4Lines);

        Kiko.LockRotationToTarget(playerTransform, onlyYAxis: true, snap: false, preventControllerOverride: true, smoothSpeed: 360f);
        Kiko.SetJournalEntries(Kiko5Journal);
        Kiko.PlayDialogue("KIKO",Kiko5Lines, Kiko5Journal);

        Babaylan.SetJournalEntries(Babaylan4Journal);
        Babaylan.PlayDialogue("BABAYLAN", Babaylan4Lines, Babaylan4Journal);
    }
}

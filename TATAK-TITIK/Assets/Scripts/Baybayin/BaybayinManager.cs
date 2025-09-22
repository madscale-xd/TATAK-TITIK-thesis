using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

public class BaybayinManager : MonoBehaviour
{
    [Header("Assign the NPC manager for the NPCs")]
    public NPCManager Kiko;
    public Transform KikoTransform;
    public Transform playerTransform;
    public NPCManager Babaylan;
    public NPCManager Magsasaka;
    public GameObject MagsasakaObj;
    public GameObject Sombrero;
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
    // pending id for Task3: when this ID is triggered, start Task3
    private string pendingTask3MagsasakaID = null;

    // internal tracking: track multiple completed tasks (idempotent)
    private HashSet<string> completedTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool task1HasRun = false;

    // NEW: internal tracking of started (in-progress) tasks (idempotent)
    private HashSet<string> startedTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private string currentStartedTask = null;

    /// <summary>Return a copy of completed tasks for saving or inspection.</summary>
    public List<string> GetCompletedTasks()
    {
        return new List<string>(completedTasks);
    }

    /// <summary>Return a copy of started tasks for saving or inspection. NEW.</summary>
    public List<string> GetStartedTasks()
    {
        if (string.IsNullOrWhiteSpace(currentStartedTask)) return new List<string>();
        return new List<string>{ currentStartedTask }; // first/only entry = current
    }

    /// <summary>Query whether a named task is completed.</summary>
    public bool IsTaskCompleted(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName)) return false;
        return completedTasks.Contains(taskName);
    }

    /// <summary>Query whether a named task has been started (in-progress). NEW.</summary>
    public bool IsTaskStarted(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName)) return false;
        return string.Equals(currentStartedTask, taskName, StringComparison.OrdinalIgnoreCase);
    }

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
        // If we are waiting for the Magsasaka's dialogue trigger to start Task3, detect it here.
        if (!string.IsNullOrWhiteSpace(pendingTask3MagsasakaID) && string.Equals(pendingTask3MagsasakaID, npcID))
        {
            pendingTask3MagsasakaID = null; // consume it
            Debug.Log($"[BaybayinManager] Detected trigger for '{npcID}' (Magsasaka). Starting Task3.");
            Task3();
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
        // If we are waiting for the Magsasaka's finished-dialogue to start Task3, detect it here.
        if (!string.IsNullOrWhiteSpace(pendingTask3MagsasakaID) && string.Equals(pendingTask3MagsasakaID, npcID))
        {
            pendingTask3MagsasakaID = null; // consume it
            Debug.Log($"[BaybayinManager] OnDialogueFinished detected '{npcID}' (Magsasaka). Starting Task3.");
            Task3();
        }
    }

    /// <summary>
    /// Mark a named task as completed. BaybayinManager knows about "task1" in your case.
    /// This is public so external triggers (waypoints) can notify completion.
    /// </summary>
    public void MarkTaskCompleted(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName)) return;

        // already done?
        if (completedTasks.Contains(taskName))
        {
            Debug.Log($"[BaybayinManager] MarkTaskCompleted: '{taskName}' already completed.");
            return;
        }

        // record it
        completedTasks.Add(taskName);

        // some tasks may need special flags (task1 had a run-once flag earlier)
        if (taskName.Equals("task1", StringComparison.OrdinalIgnoreCase) && task1RunOnlyOnce)
            task1HasRun = true;

        Debug.Log($"[BaybayinManager] MarkTaskCompleted: '{taskName}' recorded.");

        // Apply the side effects immediately (safe idempotent implementation)
        ApplyTaskEffects(taskName);
    }

    // NEW: Mark a task as started (in-progress) without marking as complete.
    // This records the started state and applies any non-terminal start-side-effects so the NPCs remain in the expected intermediate states.
    public void MarkTaskStarted(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName)) return;
        if (string.Equals(currentStartedTask, taskName, StringComparison.OrdinalIgnoreCase)) return;

        currentStartedTask = taskName;
        Debug.Log($"MarkTaskStarted: currentStartedTask = '{taskName}'");
        ApplyStartEffects(taskName);
    }


    /// <summary>
    /// Apply side-effects associated with a completed task.
    /// This is safe to call on load to rehydrate runtime state without
    /// re-starting interactive flows that require player actions.
    /// </summary>
    public void ApplyTaskEffects(string taskName)       //PERMANENT
    {
        if (string.IsNullOrWhiteSpace(taskName)) return;

        // guard so we don't reapply expensive stuff twice
        if (!completedTasks.Contains(taskName))
            completedTasks.Add(taskName);

        switch (taskName.ToLowerInvariant())
        {
            case "task1":
                // Reapply what MarkTask1Completed did: snapshot Kiko -> Kiko2 lines/state
                if (Kiko != null)
                {
                    Kiko.ChangeNPCID("Kiko2", false);
                    Kiko.SetDialogueLines(Kiko2Lines);
                }
                // Mark that Task1 was run (preserve run-once semantics)
                if (task1RunOnlyOnce) task1HasRun = true;
                break;

            case "task2":
                // Reapply Task2 side-effects: advance time & enable Magsasaka (and other required effects)
                if (DNC != null)
                {
                    try
                    {
                        // set to morning (same as runtime)
                        DNC.SetTimeOfDay(8f, 10f);
                        Debug.Log("[BaybayinManager] ApplyTaskEffects: Set time of day for task2.");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[BaybayinManager] ApplyTaskEffects(task2): exception setting time: {ex}");
                    }
                }

                // enable Magsasaka object if assigned
                if (MagsasakaObj != null)
                {
                    MagsasakaObj.SetActive(true);
                }

                // if there's any other non-interactive side-effect from your original MarkTask2Completed,
                // apply it here (e.g., journal toggles, scene flags).
                break;

            case "task3":
                // Reapply Task3 side-effects — example: reveal Sombrero or whatever Task3 does
                if (Sombrero != null)
                {
                    Sombrero.SetActive(true);
                }

                // add any extra rehydration your Task3 would have done
                break;

            default:
                Debug.Log($"[BaybayinManager] ApplyTaskEffects: no explicit handler for '{taskName}'.");
                break;
        }
    }

    // NEW: Apply the non-terminal / "in-progress" effects for a started task.
    // These should not mark the task complete but should rehydrate NPC state that indicates the task is underway.
    // By default this mirrors some parts of ApplyTaskEffects in a non-terminal way — customize as needed.
    public void ApplyStartEffects(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName)) return;

        switch (taskName.ToLowerInvariant())
        {
            case "task1":
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
                break;

            case "task2":
                break;

            case "task3":
                // Example: reveal partial visuals for Task3 but don't mark finished
                if (Sombrero != null)
                    Sombrero.SetActive(true);
                break;

            default:
                Debug.Log($"[BaybayinManager] ApplyStartEffects: no explicit handler for '{taskName}'.");
                break;
        }
    }

    // NEW: convenience to re-apply all started-task effects (call on load if desired)
    public void RehydrateStartedTasks()
    {
        foreach (var t in startedTasks)
        {
            ApplyStartEffects(t);
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
        if (IsTaskCompleted("task2"))
        {
            Debug.Log("[BaybayinManager] MarkTask2Completed: already completed, ignoring.");
            return;
        }

        // mark done
        MarkTaskCompleted("Task2");
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
                MagsasakaObj.SetActive(true);
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
        // If we have a Magsasaka NPC, wait until the player talks to them to start Task3.
        if (Magsasaka != null)
        {
            try
            {
                var magsId = Magsasaka.GetNPCID();
                if (!string.IsNullOrWhiteSpace(magsId))
                {
                    pendingTask3MagsasakaID = magsId;
                    Debug.Log($"[BaybayinManager] Task2 completed — will start Task3 when dialogue for '{magsId}' is triggered.");
                }
                else
                {
                    Debug.LogWarning("[BaybayinManager] MarkTask2Completed: Magsasaka.GetNPCID() returned empty. Starting Task3 immediately as fallback.");
                    Task3();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BaybayinManager] MarkTask2Completed: exception getting Magsasaka id: {ex}. Starting Task3 immediately as fallback.");
                Task3();
            }
        }
        else
        {
            Debug.LogWarning("[BaybayinManager] MarkTask2Completed: Magsasaka reference is null; starting Task3 immediately as fallback.");
            Task3();
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

        // Start Task1 (we also record it as started for persistence)
        Task1();
        MarkTaskStarted("task1"); // NEW: record that task1 has started
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
        Kiko.PlayDialogue("KIKO", Kiko5Lines, Kiko5Journal);

        Babaylan.SetJournalEntries(Babaylan4Journal);
        Babaylan.PlayDialogue("BABAYLAN", Babaylan4Lines, Babaylan4Journal);
    }

    void Task3()
    {
        Debug.Log("[BaybayinManager] Task3: starting...");
        Sombrero.SetActive(true);
        Debug.Log("[BaybayinManager] Task3: finished (customize the method body with real actions).");
        MarkTaskCompleted("task3");
    }
}
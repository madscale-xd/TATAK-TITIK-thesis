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
    public MagsasakaTASK4TRigger MagTrig;
    public GameObject Sombrero;
    public GameObject SombreroCosmetic;
    public Transform BabaylanTransform;
    public GameObject[] Leaves;
    public GameObject RiceBowl;
    public GameObject PaintBowl;

    public GameObject RiceBowlKALAN;
    public GameObject PaintBowlKALAN;

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
    public string kiko7TriggerNPCID = "Kiko7";
    private bool task1HasRun = false;

    // NEW: internal tracking of started (in-progress) tasks (idempotent)
    // --- Fields (replace your started/completed fields with these) ---
    private HashSet<string> completedTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private List<string> completedTaskOrder = new List<string>(); // preserves completion order

    private HashSet<string> startedTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private List<string> startedTaskOrder = new List<string>(); // preserves started order
    private string currentStartedTask = null;

    /// <summary>Return a copy of completed tasks for saving or inspection.</summary>
    public List<string> GetCompletedTasks()
    {
        // return in saved order (oldest -> newest)
        return new List<string>(completedTaskOrder);
    }

    public List<string> GetStartedTasks()
    {
        // return the ordered started list (oldest -> newest); SaveLoadManager can use last as "current"
        return new List<string>(startedTaskOrder);
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
    public string[] Kiko2Lines = new string[] { "" };
    public string[] Babaylan2Lines = new string[] { "" };
    public string[] Kiko3Lines = new string[] { "" };
    public string[] Babaylan3Lines = new string[] { "" };
    public string[] Kiko4Lines = new string[] { "" };
    public string[] Babaylan4Lines = new string[] { "" };
    public string[] Kiko5Lines = new string[] { "" };
    //Act2
    public string[] Babaylan5Lines = new string[] { "" };
    public string[] Kiko6Lines = new string[] { "" };
    public string[] Kiko7Lines = new string[] { "" };
    public string[] Kiko8Lines = new string[] { "" };
    public string[] Kiko9Lines = new string[] { "" };
    public string[] Kiko10Lines = new string[] { "" };
    public string[] Kiko11Lines = new string[] { "" };
    public string[] Kiko12Lines = new string[] { "" };
    public string[] Kiko13Lines = new string[] { "" };

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
    JournalTriggerEntry[] Kiko6Journal = new JournalTriggerEntry[]{
        new JournalTriggerEntry { key = "mag-ani", displayWord = "ᜋᜄ᜔-ᜀᜈᜒ"},
        new JournalTriggerEntry { key = "kiping", displayWord = "ᜃᜒᜉᜒᜅ᜔"},
        new JournalTriggerEntry { key = "palamuti", displayWord = "ᜉᜎᜋᜓᜆᜒ"} };

    JournalTriggerEntry[] Kiko7Journal = new JournalTriggerEntry[]{
        new JournalTriggerEntry { key = "dahon", displayWord = "ᜇᜑᜓᜈ"}};

    JournalTriggerEntry[] Kiko8Journal = new JournalTriggerEntry[]{
        new JournalTriggerEntry { key = "dito", displayWord = "ᜇᜒᜆᜓ"}};

    JournalTriggerEntry[] Kiko9Journal = new JournalTriggerEntry[]{
        new JournalTriggerEntry { key = "mangkok", displayWord = "ᜋᜅ᜔ᜃᜓᜃ᜔"},
        new JournalTriggerEntry { key = "kalan", displayWord = "ᜃᜎᜈ᜔"}};

    JournalTriggerEntry[] Kiko10Journal = new JournalTriggerEntry[]{
        new JournalTriggerEntry { key = "kulay", displayWord = "ᜃᜓᜎᜌ᜔"},
        new JournalTriggerEntry { key = "galapong", displayWord = "ᜄᜎᜉᜓᜅ᜔"}};

    JournalTriggerEntry[] Kiko11Journal = new JournalTriggerEntry[]{
        new JournalTriggerEntry { key = "pintura", displayWord = "ᜉᜒᜈ᜔ᜆᜓᜇ"}};
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
        if (!string.IsNullOrWhiteSpace(kiko7TriggerNPCID) && string.Equals(kiko7TriggerNPCID, npcID, StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log($"[BaybayinManager] Detected trigger for '{npcID}' — calling OnKiko7Detected().");
            Task7();
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
        // inside OnDialogueFinished, after other checks:
        if (!string.IsNullOrWhiteSpace(kiko7TriggerNPCID) && string.Equals(kiko7TriggerNPCID, npcID, StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log($"[BaybayinManager] OnDialogueFinished: detected '{npcID}' — calling OnKiko7Detected().");
            Task7();
        }
    }

    /// <summary>
    /// Mark a named task as completed. BaybayinManager knows about "task1" in your case.
    /// This is public so external triggers (waypoints) can notify completion.
    /// </summary>
    public void MarkTaskCompleted(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName)) return;

        if (completedTasks.Contains(taskName))
        {
            Debug.Log($"[BaybayinManager] MarkTaskCompleted: '{taskName}' already completed.");
            return;
        }

        completedTasks.Add(taskName);
        if (!completedTaskOrder.Contains(taskName))
            completedTaskOrder.Add(taskName);

        // If this task was in the started set, remove it — it's no longer "in-progress"
        if (startedTasks.Contains(taskName))
        {
            startedTasks.Remove(taskName);
            startedTaskOrder.RemoveAll(s => string.Equals(s, taskName, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(currentStartedTask, taskName, StringComparison.OrdinalIgnoreCase))
                currentStartedTask = (startedTaskOrder.Count > 0) ? startedTaskOrder[startedTaskOrder.Count - 1] : null;
        }

        if (taskName.Equals("task1", StringComparison.OrdinalIgnoreCase) && task1RunOnlyOnce)
            task1HasRun = true;

        Debug.Log($"[BaybayinManager] MarkTaskCompleted: '{taskName}' recorded.");
        ApplyTaskEffects(taskName);
    }

    // NEW: Mark a task as started (in-progress) without marking as complete.
    // This records the started state and applies any non-terminal start-side-effects so the NPCs remain in the expected intermediate states.
    public void MarkTaskStarted(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName)) return;

        // If it's already recorded as started, just make it current
        if (startedTasks.Contains(taskName))
        {
            currentStartedTask = taskName;
            // make sure order reflects last-started
            startedTaskOrder.RemoveAll(s => string.Equals(s, taskName, StringComparison.OrdinalIgnoreCase));
            startedTaskOrder.Add(taskName);
            Debug.Log($"MarkTaskStarted (existing): currentStartedTask = '{taskName}'");
            return;
        }

        startedTasks.Add(taskName);
        startedTaskOrder.Add(taskName);
        currentStartedTask = taskName;
        Debug.Log($"MarkTaskStarted: recorded startedTasks.Add('{taskName}') and currentStartedTask = '{taskName}'");

        // Apply non-terminal start effects (idempotent)
        ApplyStartEffects(taskName);
    }


    public void ApplyTaskEffects(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName)) return;

        // ensure recorded in completed set/order
        if (!completedTasks.Contains(taskName))
        {
            completedTasks.Add(taskName);
            if (!completedTaskOrder.Contains(taskName))
                completedTaskOrder.Add(taskName);
        }

        switch (taskName.ToLowerInvariant())
        {
            case "task1":
                if (Kiko != null)
                {
                    Kiko.ChangeNPCID("Kiko2", false);
                    Kiko.SetDialogueLines(Kiko2Lines);
                }
                if (task1RunOnlyOnce) task1HasRun = true;
                break;

            case "task2":
                // Apply permanent task2 effects (clear Kiko destinations early so rehydrates don't leave old paths)
                Kiko.ClearDestinations();
                if (MagsasakaObj != null)
                {
                    MagsasakaObj.SetActive(true);
                }
                else
                {
                    Debug.Log("Mgassask");
                }
                ;
                break;

            case "task3":
                if (SombreroCosmetic != null)
                    SombreroCosmetic.SetActive(true);
                break;

            case "task4":
                if (MagTrig != null) MagTrig.KeepMagsasakaUpdated();
                if (Magsasaka != null && waypoints != null && waypoints.Length > 8)
                {
                    Magsasaka.EnqueueDestination(waypoints[6]);
                    Magsasaka.EnqueueDestination(waypoints[7]);
                    Magsasaka.EnqueueDestination(waypoints[8]);
                }
                break;

            case "task5":
                break;

            case "task6":
                break;

            case "task7":
                RiceBowl.SetActive(true);
                break;

            case "task8":
                break;
            
            case "task9":
                PaintBowl.SetActive(true);
                break;

            case "task10":
                break;
            
            case "task11":
                break;

            case "task12":
                break;

            default:
                Debug.Log($"[BaybayinManager] ApplyTaskEffects: no explicit handler for '{taskName}'.");
                break;
        }
    }


    public void ApplyStartEffects(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName)) return;

        // IMPORTANT: don't apply start-effects for a task that's already been completed.
        if (IsTaskCompleted(taskName))
        {
            Debug.Log($"[BaybayinManager] ApplyStartEffects: '{taskName}' already completed — skipping start-effects.");
            return;
        }

        switch (taskName.ToLowerInvariant())
        {
            case "task1":
                if (Kiko == null)
                {
                    Debug.LogWarning("[BaybayinManager] Task1: Kiko is not assigned.");
                    return;
                }
                if (waypoints == null || waypoints.Length == 0)
                {
                    Debug.LogWarning("[BaybayinManager] Task1: waypoints array is empty. Nothing to enqueue.");
                    return;
                }
                int[] desiredIndices = new int[] { 1, 5 };
                foreach (int i in desiredIndices)
                {
                    if (i < 0 || i >= waypoints.Length) continue;
                    if (waypoints[i] == null) continue;
                    // If your NPCManager has a HasDestination check, use it. Otherwise ensure EnqueueDestination is idempotent.
                    Kiko.EnqueueDestination(waypoints[i]);
                }
                break;

            case "task2":
                break;

            case "task3":
                if (Sombrero != null) Sombrero.SetActive(true);
                break;

            case "task4":
                if (MagTrig != null) MagTrig.KeepMagsasakaUpdated();
                if (Magsasaka != null && waypoints != null && waypoints.Length > 8)
                {
                    Magsasaka.EnqueueDestination(waypoints[6]);
                    Magsasaka.EnqueueDestination(waypoints[7]);
                    Magsasaka.EnqueueDestination(waypoints[8]);
                }
                Babaylan.EnqueueDestination(waypoints[15]);
                Kiko.EnqueueDestination(waypoints[17]);
                break;

            case "task5":
                Kiko.ChangeNPCID("Kiko6", false);
                Kiko.EnqueueDestination(waypoints[7]);
                break;

            case "task6":
                Kiko.ChangeNPCID("Kiko7", false);
                Kiko.SetDialogueLines(Kiko7Lines);
                Kiko.SetJournalEntries(Kiko7Journal);
                break;

            case "task7":
                foreach (GameObject leaf in Leaves)
                {
                    if (leaf != null)
                    {
                        leaf.SetActive(true);
                    }
                }
                break;

            case "task8":
                Kiko.EnqueueDestination(waypoints[18]);
                Kiko.ChangeNPCID("Kiko8", false);
                Kiko.SetDialogueLines(Kiko8Lines);
                Kiko.SetJournalEntries(Kiko8Journal);
                break;

            case "task9":
                Kiko.ChangeNPCID("Kiko9", false);
                Kiko.SetDialogueLines(Kiko9Lines);
                Kiko.SetJournalEntries(Kiko9Journal);
                break;
            
            case "task10":
                Kiko.ChangeNPCID("Kiko10", false);
                Kiko.SetDialogueLines(Kiko10Lines);
                Kiko.SetJournalEntries(Kiko10Journal);
                RiceBowlKALAN.SetActive(true);
                break;
            
            case "task11":
                Kiko.ChangeNPCID("Kiko11", false);
                Kiko.SetDialogueLines(Kiko11Lines);
                Kiko.SetJournalEntries(Kiko11Journal);
                PaintBowlKALAN.SetActive(true);
                break;

            case "task12":
                break;

            default:
                Debug.Log($"[BaybayinManager] ApplyStartEffects: no explicit handler for '{taskName}'.");
                break;
        }
    }

    public void RehydrateStartedTasks()
    {
        if (string.IsNullOrWhiteSpace(currentStartedTask))
        {
            Debug.Log("[BaybayinManager] RehydrateStartedTasks: no current started task to rehydrate.");
            return;
        }

        Debug.Log($"[BaybayinManager] RehydrateStartedTasks: applying start-effects for currentStartedTask='{currentStartedTask}'");
        ApplyStartEffects(currentStartedTask);
    }

    // Call this from SaveLoadManager after pendingBaybayinStartedTasks is available
    public void LoadStartedTasks(IEnumerable<string> started, bool applyNow = false)
    {
        if (started == null) return;
        startedTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        startedTaskOrder = new List<string>();

        foreach (var s in started)
        {
            if (string.IsNullOrWhiteSpace(s)) continue;
            if (!startedTasks.Contains(s))
            {
                startedTasks.Add(s);
                startedTaskOrder.Add(s);
            }
        }

        // current started = last one in list
        if (startedTaskOrder.Count > 0)
            currentStartedTask = startedTaskOrder[startedTaskOrder.Count - 1];
        else
            currentStartedTask = null;

        if (applyNow)
            RehydrateStartedTasks();
    }

    public void MarkTask1Completed()    //Kiko reaches hut
    {
        if (IsTaskCompleted("task1"))
        {
            Debug.Log("[BaybayinManager] MarkTask2Completed: already completed, ignoring.");
            return;
        }
        MarkTaskCompleted("task1");
        Kiko.ClearDestinations();
        MarkTaskStarted("task2");

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
        MarkTaskCompleted("task2");
        Debug.Log("[BaybayinManager] MarkTask2Completed: task2 marked completed.");
        Babaylan.EnqueueDestination(waypoints[16]);
        Kiko.EnqueueDestination(waypoints[6]);

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
    void Task1()    //going towards Babaylan hut
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

    void Task2()    //exchange between Kiko and Babaylan
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

    void Task3()    //Hat state
    {
        Debug.Log("[BaybayinManager] Task3: starting...");
        Sombrero.SetActive(true);
        Debug.Log("[BaybayinManager] Task3: finished (customize the method body with real actions).");
    }

    public void Task4() //Hat thanks, Morning dialogue
    {
        Debug.Log("Task 4 time");
        MarkTaskStarted("task4");
        Babaylan.ChangeNPCID("Babaylan5");
        Babaylan.EnqueueDestination(waypoints[15]);
        Babaylan.SetDialogueLines(Babaylan5Lines);
        Babaylan.LockRotationToTarget(playerTransform, onlyYAxis: true, snap: false, preventControllerOverride: true, smoothSpeed: 360f);
        Kiko.LockRotationToTarget(playerTransform, onlyYAxis: true, snap: false, preventControllerOverride: true, smoothSpeed: 360f);
        Babaylan.PlayDialogue("BABAYLAN");

        Kiko.EnqueueDestination(waypoints[17]);
        Kiko.ChangeNPCID("Kiko6", false);
        Kiko.SetJournalEntries(Kiko6Journal);
        Kiko.SetDialogueLines(Kiko6Lines);
        Kiko.PlayDialogue("KIKO", Kiko6Lines, Kiko6Journal);
    }

    public void Task5() //Moving Kiko to factory
    {
        Debug.Log("Task 5 time");
        MarkTaskCompleted("task4");
        DNC.SetTimeOfDay(10f, 5f);
        MarkTaskStarted("task5");
        Kiko.EnqueueDestination(waypoints[7]);
    }

    public void Task6() //Kiko telling u to get leaves
    {
        Debug.Log("Task 6 time");
        DNC.SetTimeOfDay(11f, 5f);
        MarkTaskCompleted("task5");
        Kiko.ChangeNPCID("Kiko7", false);
        Kiko.SetDialogueLines(Kiko7Lines);
        Kiko.SetJournalEntries(Kiko7Journal);
        MarkTaskStarted("task6");
    }

    public void Task7() //leaves collection time
    {
        Debug.Log("Task 7 time");
        DNC.SetTimeOfDay(12f, 10f);
        MarkTaskCompleted("task6");
        MarkTaskStarted("task7");
    }

    public void Task8() //KIKO go to table
    {
        Debug.Log("Task 8 time");
        Kiko.EnqueueDestination(waypoints[18]);
        Kiko.ChangeNPCID("Kiko8", false);
        Kiko.SetDialogueLines(Kiko8Lines);
        Kiko.SetJournalEntries(Kiko8Journal);
        MarkTaskCompleted("task7");
        MarkTaskStarted("task8");
    }

    public void Task9() //get RiceBowl, put on 
    {
        Debug.Log("Task 9 time");
        Kiko.ChangeNPCID("Kiko9", false);
        Kiko.SetDialogueLines(Kiko9Lines);
        Kiko.SetJournalEntries(Kiko9Journal);
        Kiko.PlayDialogue("KIKO", Kiko9Lines, Kiko9Journal);
        MarkTaskCompleted("task8");
        MarkTaskStarted("task9");
    }

    public void Task10()    //put PaintBowl on Kalan
    {
        Debug.Log("Task 10 time");
        Kiko.ChangeNPCID("Kiko10", false);
        Kiko.SetDialogueLines(Kiko10Lines);
        Kiko.SetJournalEntries(Kiko10Journal);
        Kiko.PlayDialogue("KIKO", Kiko10Lines, Kiko10Journal);
        MarkTaskCompleted("task9");
        MarkTaskStarted("task10");
    }
    public void Task11()    //hang leaves
    {
        Debug.Log("Task 11 time");
        Kiko.EnqueueDestination(waypoints[20]);
        Kiko.ChangeNPCID("Kiko10", false);
        Kiko.SetDialogueLines(Kiko11Lines);
        Kiko.SetJournalEntries(Kiko11Journal);
        Kiko.PlayDialogue("KIKO", Kiko11Lines, Kiko11Journal);
        MarkTaskCompleted("task10");
        MarkTaskStarted("task11");
    }
}
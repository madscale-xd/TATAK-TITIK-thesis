using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class SaveLoadManager : MonoBehaviour
{
    public Transform playerTransform;
    public int currentSaveSlot = 1; // Default slot to save/load (1-5)
    private const int maxSaveSlots = 5;

    private Vector3 positionToLoad;
    private bool shouldApplyPosition = false;

    public static List<JournalEntry> pendingJournalEntries;

    // Reference to your ItemDatabase (assign in inspector)
    public ItemDatabase itemDatabase;

    private bool shouldResetInventoryAfterLoad = false;

    public static SaveLoadManager Instance;
    public DialogueManager DM;
    private HashSet<string> collectedPickupIDs = new HashSet<string>();
    private HashSet<string> interactedObjectIDs = new HashSet<string>();
    private bool? pendingJournalAvailable = null;

    // NEW: pending triggered dialogue IDs to apply after scene load
    private List<string> pendingTriggeredDialogueIDs = null;
    private List<NPCIdPair> pendingNpcIdOverrides = null;
    private List<NPCDialoguePair> pendingNpcDialogueOverrides = null;
    private List<NPCDestinationPair> pendingNpcDestinations = null;

    // NEW: pending time of day to apply after scene load (hours 0..24). NaN means none.
    private float pendingTimeOfDay = float.NaN;
    // NEW: pending inventory to apply after scene load
    private List<InventoryItemData> pendingInventoryData = null;
    private string pendingEquippedItem = null;
    private List<string> pendingBaybayinCompletedTasks = null;
    private List<string> pendingBaybayinStartedTasks = null; // NEW


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep this instance alive
        }
        else if (Instance != this)
        {
            Destroy(gameObject); // Destroy duplicate
        }
    }

    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Helper: Find the player by tag
    private void AssignPlayerTransform()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogError("[SaveLoadManager] Player with tag 'Player' not found.");
        }
    }

    public void SaveGame(int slot)
    {
        if (slot < 1 || slot > maxSaveSlots)
        {
            Debug.LogError("Save slot out of range!");
            return;
        }
        currentSaveSlot = slot;
        AssignPlayerTransform();
        if (playerTransform == null)
        {
            Debug.LogError("Cannot save: playerTransform is null.");
            return;
        }

        // Collect inventory data from InventoryManager
        List<InventoryItemData> inventoryData = new List<InventoryItemData>();
        foreach (var item in InventoryManager.Instance.items)
        {
            inventoryData.Add(new InventoryItemData(item.itemName, item.quantity));
        }
        string currentScene = SceneManager.GetActiveScene().name;

        bool journalAvailable = false;
        if (JournalAvailability.Instance != null)
            journalAvailable = JournalAvailability.Instance.IsAvailable();

        // Create SaveData (constructor includes journalAvailable param)
        SaveData data = new SaveData(
            playerTransform.position,
            JournalManager.Instance.GetEntries(),
            inventoryData,
            InventoryManager.Instance.equippedItem,
            currentScene,
            journalAvailable
        );

        // Persist runtime NPC IDs (GameObject name -> current npcID)
        var allTriggers = FindObjectsOfType<NPCDialogueTrigger>();
        data.npcIdOverrides = new List<NPCIdPair>();
        foreach (var t in allTriggers)
        {
            if (t == null) continue;
            // store the GameObject name so we can find the exact instance at load time
            data.npcIdOverrides.Add(new NPCIdPair(t.gameObject.name, t.GetNPCID()));
        }

        // persist runtime NPC dialogue lines per npcID
        data.npcDialogueOverrides = new List<NPCDialoguePair>();
        foreach (var t in allTriggers)
        {
            if (t == null) continue;
            string id = t.GetNPCID();
            if (string.IsNullOrWhiteSpace(id)) continue;
            data.npcDialogueOverrides.Add(new NPCDialoguePair(id, t.GetDialogueLines()));
        }

        // persist NPC movement positions & destinations
        data.npcDestinations = new List<NPCDestinationPair>();
        var navs = FindObjectsOfType<NavMeshNPCController>();
        foreach (var n in navs)
        {
            if (n == null) continue;
            var go = n.gameObject;
            var agent = n.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Vector3 pos = go.transform.position;
            bool hasPos = true;

            bool hasDest = false;
            Vector3 dest = Vector3.zero;
            bool wasStopped = false;

            if (agent != null)
            {
                wasStopped = agent.isStopped;
                if (agent.hasPath)
                {
                    hasDest = true;
                    dest = agent.destination;
                }
            }

            data.npcDestinations.Add(new NPCDestinationPair(go.name, pos, hasPos, dest, hasDest, wasStopped));
        }

        // persist pickup/interacted sets
        data.collectedPickupIDs = new List<string>(collectedPickupIDs);
        data.interactedObjectIDs = new List<string>(interactedObjectIDs);

        // NEW: add triggered-dialogue IDs from DialogueEventsManager (if present)
        if (DialogueEventsManager.Instance != null)
        {
            var triggeredList = DialogueEventsManager.Instance.GetTriggeredListForSave();
            data.triggeredDialogueIDs = triggeredList ?? new List<string>();
        }
        else
        {
            data.triggeredDialogueIDs = new List<string>();
        }

        // NEW: capture current time-of-day from DayNightCycle (if present)
        var dnc = FindObjectOfType<DayNightCycle>();
        if (dnc != null)
        {
            data.timeOfDayHours = dnc.GetTimeOfDayHours();
            Debug.Log($"[SaveLoadManager] Captured timeOfDay = {data.timeOfDayHours}h to savefile.");
        }
        else
        {
            data.timeOfDayHours = -1f;
        }

        var bay = FindObjectOfType<BaybayinManager>();
        if (bay != null)
        {
            data.completedTasks = bay.GetCompletedTasks(); // ordered
            data.startedTasks = bay.GetStartedTasks();     // ordered (oldest..newest)
        }
        else
        {
            data.completedTasks = new List<string>();
            data.startedTasks = new List<string>();
        }

        SaveSystem.Save(data, slot);
        Debug.Log($"Game saved in slot {slot} at position {playerTransform.position}");
    }

    public void LoadGame(int slot)
    {
        if (slot < 1 || slot > maxSaveSlots)
        {
            Debug.LogError("Load slot out of range!");
            return;
        }
        currentSaveSlot = slot;
        Debug.Log($"[SaveLoadManager] Loading save slot {slot}...");

        SaveData data = SaveSystem.Load(slot);
        if (data != null)
        {
            positionToLoad = data.GetPosition();

            // Validate the saved position before applying
            if (IsValidPosition(positionToLoad))
            {
                shouldApplyPosition = true;
            }
            else
            {
                Debug.LogWarning($"[SaveLoadManager] Saved player position is invalid ({positionToLoad}). Will not apply position on load.");
                shouldApplyPosition = false;
            }

            pendingJournalEntries = data.journalEntries;

            // DO NOT apply inventory immediately here — stash it to be applied after the scene loads
            pendingInventoryData = data.inventoryItems != null ? new List<InventoryItemData>(data.inventoryItems) : new List<InventoryItemData>();
            pendingEquippedItem = data.equippedItem;

            // persist collected / interacted sets right away
            collectedPickupIDs = new HashSet<string>(data.collectedPickupIDs ?? new List<string>());
            interactedObjectIDs = new HashSet<string>(data.interactedObjectIDs ?? new List<string>());

            // NEW: restore pending journal availability
            pendingJournalAvailable = data.journalAvailable;

            // NEW: store triggered dialogue IDs to apply on next scene load
            pendingTriggeredDialogueIDs = data.triggeredDialogueIDs != null ? new List<string>(data.triggeredDialogueIDs) : new List<string>();

            // NEW: store NPC ID overrides to apply on next scene load
            pendingNpcIdOverrides = data.npcIdOverrides != null ? new List<NPCIdPair>(data.npcIdOverrides) : new List<NPCIdPair>();
            pendingNpcDialogueOverrides = data.npcDialogueOverrides != null ? new List<NPCDialoguePair>(data.npcDialogueOverrides) : new List<NPCDialoguePair>();
            // NEW: store NPC destinations to apply on next scene load
            pendingNpcDestinations = data.npcDestinations != null ? new List<NPCDestinationPair>(data.npcDestinations) : new List<NPCDestinationPair>();

            // NEW: store Baybayin task completion to apply after scene load
            pendingBaybayinCompletedTasks = data.completedTasks != null && data.completedTasks.Count > 0
                ? new List<string>(data.completedTasks)
                : null;

            // NEW: store Baybayin started (in-progress) tasks to apply after scene load
            pendingBaybayinStartedTasks = data.startedTasks != null && data.startedTasks.Count > 0
                ? new List<string>(data.startedTasks)
                : null;

            // NEW: store pending timeOfDay to apply after scene load
            if (data.timeOfDayHours >= 0f)
                pendingTimeOfDay = data.timeOfDayHours;
            else
                pendingTimeOfDay = float.NaN;

            Debug.Log($"Loading scene for save slot {slot} with saved position {positionToLoad} (valid={shouldApplyPosition})");
        }
        else
        {
            Debug.LogWarning($"No save file found in slot {slot}. Starting fresh.");
            // Clear any runtime state so it doesn't carry over between slots
            shouldApplyPosition = false;
            pendingJournalEntries = null;
            pendingJournalAvailable = false;
            pendingTriggeredDialogueIDs = new List<string>();
            pendingNpcIdOverrides = new List<NPCIdPair>();
            pendingNpcDialogueOverrides = new List<NPCDialoguePair>();
            pendingNpcDestinations = new List<NPCDestinationPair>();
            pendingTimeOfDay = float.NaN;
            pendingBaybayinCompletedTasks = null;

            // **** CRITICAL: clear collected/interacted sets so slot isolation holds ****
            collectedPickupIDs.Clear();
            interactedObjectIDs.Clear();

            // Ensure Inventory is cleared (slot isolation)
            shouldResetInventoryAfterLoad = true;

            // Clear any pending inventory we might have kept
            pendingInventoryData = null;
            pendingEquippedItem = null;
        }
        Time.timeScale = 1f;


        // safe load: only reference data if not null & savedSceneName not empty
        string defaultScene = "WizardTower";
        if (data != null && !string.IsNullOrEmpty(data.savedSceneName))
        {
            // Validate the saved scene is actually in build settings
            if (IsSceneInBuildSettings(data.savedSceneName))
            {
                SceneManager.LoadScene(data.savedSceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogWarning($"[SaveLoadManager] Saved scene '{data.savedSceneName}' is not in Build Settings. Falling back to '{defaultScene}'.");
                SceneManager.LoadScene(defaultScene, LoadSceneMode.Single);
            }
        }
        else
        {
            Debug.LogWarning($"No scene name found in save data. Defaulting to {defaultScene}.");
            SceneManager.LoadScene(defaultScene, LoadSceneMode.Single);
        }
    }


    public void ClearGame(int slot)
    {
        if (slot < 1 || slot > maxSaveSlots)
        {
            Debug.LogError("Clear slot out of range!");
            return;
        }

        // Delete persistent save file
        SaveSystem.Delete(slot);
        Debug.Log($"Save slot {slot} has been cleared (file deleted).");

        // Mark inventory to reset on next scene load and also attempt immediate reset
        shouldResetInventoryAfterLoad = true;

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ResetInventory();
            InventoryManager.Instance.inventoryUI = FindObjectOfType<InventoryUI>();
            InventoryManager.Instance.inventoryUI?.UpdateInventoryUI();
            Debug.Log("[SaveLoadManager] InventoryManager reset immediately by ClearGame.");
        }

        // Clear collected/interacted runtime sets
        collectedPickupIDs.Clear();
        interactedObjectIDs.Clear();

        // Clear pending journal entries and availability (apply immediately if possible)
        pendingJournalEntries = new List<JournalEntry>();
        pendingJournalAvailable = false;
        if (JournalAvailability.Instance != null)
        {
            JournalAvailability.Instance.DisableJournal();
        }

        if (JournalManager.Instance != null)
        {
            // Ensure JournalManager is cleared right away
            JournalManager.Instance.LoadEntries(pendingJournalEntries);
            Debug.Log("[SaveLoadManager] JournalManager cleared immediately by ClearGame.");
        }

        // Clear triggered-dialogue both in-memory and in DialogueEventsManager (if present)
        pendingTriggeredDialogueIDs = new List<string>();
        if (DialogueEventsManager.Instance != null)
        {
            DialogueEventsManager.Instance.ApplyTriggeredListFromSave(pendingTriggeredDialogueIDs);
            Debug.Log("[SaveLoadManager] DialogueEventsManager triggered list cleared immediately by ClearGame.");
        }

        // Clear all NPC runtime overrides so NPCs revert to prefab/default behavior after reload
        pendingNpcIdOverrides = new List<NPCIdPair>();
        pendingNpcDialogueOverrides = new List<NPCDialoguePair>();
        pendingNpcDestinations = new List<NPCDestinationPair>();

        // Clear pending inventory data/equipped item
        pendingInventoryData = null;
        pendingEquippedItem = null;
        pendingBaybayinCompletedTasks = new List<string>();
        pendingBaybayinStartedTasks = new List<string>(); // NEW
        // Clear pending time-of-day and any pending player position to apply
        pendingTimeOfDay = float.NaN;
        shouldApplyPosition = false;
        positionToLoad = Vector3.zero;

        // Reset current save slot state to default (optional: keep as slot)
        currentSaveSlot = 1;

        // If you store any other runtime state elsewhere, clear it here:
        // e.g. any static registries, caches etc. that should be slot-isolated.

        // Finally reload the scene (LoadGame will detect no saved file and perform a fresh load)
        LoadGame(slot);
    }

    private IEnumerator ResetInventoryDelayed()
    {
        yield return null;

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ResetInventory();

            // 🔁 Update UI if you have a reference to it
            InventoryUI ui = FindObjectOfType<InventoryUI>();
            if (ui != null)
            {
                ui.UpdateInventoryUI();
            }
        }
        else
        {
            Debug.LogWarning("InventoryManager instance not found when resetting inventory.");
        }

        shouldResetInventoryAfterLoad = false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DM.ClearDialogueQueue();
        if (shouldResetInventoryAfterLoad)
        {
            StartCoroutine(ResetInventoryDelayed());
        }

        if (shouldApplyPosition)
        {
            StartCoroutine(SetPlayerPositionNextFrame());
        }

        // First: apply pending inventory (so InventoryManager/UI are ready)
        if (pendingInventoryData != null)
        {
            StartCoroutine(ApplyPendingInventoryNextFrame());
        }

        // Ensure InventoryManager points to the InventoryUI in the just-loaded scene and refresh UI
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.inventoryUI = FindObjectOfType<InventoryUI>();
            InventoryManager.Instance.inventoryUI?.UpdateInventoryUI();
        }

        // 🧠 Restore journal entries after scene load
        if (pendingJournalEntries != null && pendingJournalEntries.Count > 0)
        {
            if (JournalManager.Instance != null)
            {
                JournalManager.Instance.LoadEntries(pendingJournalEntries);
                Debug.Log("[SaveLoadManager] Journal entries restored.");
            }
            else
            {
                Debug.LogWarning("[SaveLoadManager] JournalManager not found.");
            }

            pendingJournalEntries = null;
        }

        // Apply pending journal availability now that scene is loaded
        if (pendingJournalAvailable.HasValue)
        {
            if (JournalAvailability.Instance != null)
            {
                JournalAvailability.Instance.SetAvailable(pendingJournalAvailable.Value);
                Debug.Log($"[SaveLoadManager] Applied journal availability = {pendingJournalAvailable.Value}");
            }
            else
            {
                Debug.LogWarning("[SaveLoadManager] JournalAvailability instance not present to apply saved availability.");
            }

            pendingJournalAvailable = null;
        }

        // NEW: apply pending triggered-dialogue IDs
        if (pendingTriggeredDialogueIDs != null)
        {
            if (DialogueEventsManager.Instance != null)
            {
                DialogueEventsManager.Instance.ApplyTriggeredListFromSave(pendingTriggeredDialogueIDs);
                Debug.Log($"[SaveLoadManager] Applied {pendingTriggeredDialogueIDs.Count} triggered dialogue IDs from save.");
            }
            else
            {
                Debug.LogWarning("[SaveLoadManager] DialogueEventsManager not present to apply triggered-dialogue IDs.");
            }

            pendingTriggeredDialogueIDs = null;
        }

        if (pendingNpcIdOverrides != null) // we'll add this field similar to pendingTriggeredDialogueIDs
        {
            foreach (var pair in pendingNpcIdOverrides)
            {
                if (string.IsNullOrWhiteSpace(pair.gameObjectName) || string.IsNullOrWhiteSpace(pair.npcID))
                    continue;

                GameObject go = GameObject.Find(pair.gameObjectName);
                if (go == null) continue;

                var trigger = go.GetComponent<NPCDialogueTrigger>();
                if (trigger == null) continue;

                // Directly set the NPC ID — registry and runtime state updated.
                trigger.SetNPCID(pair.npcID);
            }

            Debug.Log($"[SaveLoadManager] Applied {pendingNpcIdOverrides.Count} NPC ID overrides from save.");
            pendingNpcIdOverrides = null;
        }

        if (pendingNpcDialogueOverrides != null && pendingNpcDialogueOverrides.Count > 0)
        {
            StartCoroutine(ApplyDialogueOverridesNextFrame());
        }

        // Apply pending NPC movement destinations saved earlier
        if (pendingNpcDestinations != null && pendingNpcDestinations.Count > 0)
        {
            int applied = 0;
            foreach (var pair in pendingNpcDestinations)
            {
                if (string.IsNullOrWhiteSpace(pair.gameObjectName)) continue;
                GameObject go = GameObject.Find(pair.gameObjectName);
                if (go == null) continue;

                var nav = go.GetComponent<NavMeshNPCController>();
                var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();

                // 1) restore transform position (use Warp for agents)
                if (pair.hasPosition && pair.position != null && pair.position.Length >= 3)
                {
                    Vector3 pos = new Vector3(pair.position[0], pair.position[1], pair.position[2]);

                    if (agent != null)
                    {
                        // Warp ensures agent internal position aligns with transform without path recalculation issues
                        agent.Warp(pos);
                    }
                    else
                    {
                        go.transform.position = pos;
                    }
                }

                // 2) restore destination (recreate movement)
                if (pair.hasDestination && pair.destination != null && pair.destination.Length >= 3)
                {
                    Vector3 dest = new Vector3(pair.destination[0], pair.destination[1], pair.destination[2]);

                    // Use your controller API to resume movement so any patrol/state logic remains correct
                    if (nav != null)
                    {
                        nav.MoveTo(dest);
                    }
                    else if (agent != null)
                    {
                        agent.SetDestination(dest);
                    }

                    // restore paused state if needed
                    if (agent != null)
                        agent.isStopped = pair.wasAgentStopped;
                }

                applied++;
            }
            Debug.Log($"[SaveLoadManager] Applied {applied} NPC positions/destinations from save.");
            pendingNpcDestinations = null;
        }

        // Apply pending time of day if present
        if (!float.IsNaN(pendingTimeOfDay))
        {
            StartCoroutine(ApplyPendingTimeOfDayNextFrame(pendingTimeOfDay));
            // pendingTimeOfDay will be cleared by the coroutine (or you can set it to NaN now)
            pendingTimeOfDay = float.NaN;
        }

        // Apply pending Baybayin completed tasks (if any)
        if (pendingBaybayinCompletedTasks != null && pendingBaybayinCompletedTasks.Count > 0)
        {
            var bay = FindObjectOfType<BaybayinManager>();
            if (bay != null)
            {
                foreach (var t in pendingBaybayinCompletedTasks)
                {
                    if (string.IsNullOrWhiteSpace(t)) continue;
                    bay.MarkTaskCompleted(t); // will record in ordered list and ApplyTaskEffects
                }
                Debug.Log($"[SaveLoadManager] Applied {pendingBaybayinCompletedTasks.Count} Baybayin completed task(s) from save.");
            }
            else
            {
                Debug.LogWarning("[SaveLoadManager] BaybayinManager not found when applying saved completed tasks.");
            }

            pendingBaybayinCompletedTasks = null;
        }

        if (pendingBaybayinStartedTasks != null && pendingBaybayinStartedTasks.Count > 0)
        {
            var bay = FindObjectOfType<BaybayinManager>();
            if (bay != null)
            {
                // Defer one frame so NPCs and waypoints have initialized
                StartCoroutine(ApplyBaybayinStartedTasksNextFrame(bay, pendingBaybayinStartedTasks));
            }
            pendingBaybayinStartedTasks = null;
        }
    }

    private IEnumerator SetPlayerPositionNextFrame()
    {
        // wait a frame so scene objects have at least started
        yield return null;

        AssignPlayerTransform();
        if (playerTransform == null)
        {
            Debug.LogWarning("[SaveLoadManager] SetPlayerPositionNextFrame: playerTransform still null.");
            yield break;
        }

        // grab common movement/physics components we want to temporarily disable
        var controller = playerTransform.GetComponent<CharacterController>();
        var movement = playerTransform.GetComponent<PlayerMovement>();
        var navAgent = playerTransform.GetComponent<UnityEngine.AI.NavMeshAgent>();
        var rb = playerTransform.GetComponent<Rigidbody>();

        // disable things that may override transform changes
        if (movement != null) movement.enabled = false;
        if (controller != null) controller.enabled = false;
        if (navAgent != null) navAgent.enabled = false;
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Try setting the position over several frames to beat out other scene initializers.
        const int maxAttempts = 10;
        int attempts = 0;
        bool positionStable = false;

        while (attempts < maxAttempts && !positionStable)
        {
            // snap to saved position
            playerTransform.position = positionToLoad;

            // one frame to let other scripts run and perhaps try to override
            yield return null;

            // If another script moved the player away, we'll catch that here and retry.
            float distance = Vector3.Distance(playerTransform.position, positionToLoad);
            if (distance <= 0.05f) // good enough threshold
            {
                positionStable = true;
                break;
            }

            attempts++;
            Debug.Log($"[SaveLoadManager] Attempt {attempts}: player position after set is {playerTransform.position} (wanted {positionToLoad}), retrying...");
        }

        // Final snap to ensure position is exact
        playerTransform.position = positionToLoad;

        // small safety yield so physics/agents settle
        yield return null;

        // restore physics/agent/movement
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
        }

        if (navAgent != null)
        {
            // If the nav-agent had an outstanding path it may immediately move again;
            // that's why we disabled it above — we re-enable after position has been set.
            navAgent.Warp(positionToLoad); // warp is safe if agent exists
            navAgent.enabled = true;
        }

        if (controller != null) controller.enabled = true;
        if (movement != null) movement.enabled = true;

        shouldApplyPosition = false;

        Debug.Log($"[SaveLoadManager] Player position applied (attempts={attempts + 1}). Final position: {playerTransform.position}");
    }

    public void MarkPickupCollected(string pickupID)
    {
        collectedPickupIDs.Add(pickupID);
    }

    public bool IsPickupCollected(string pickupID)
    {
        return collectedPickupIDs.Contains(pickupID);
    }

    public void MarkObjectInteracted(string objectID)
    {
        interactedObjectIDs.Add(objectID);
    }

    public bool IsObjectInteracted(string objectID)
    {
        return interactedObjectIDs.Contains(objectID);
    }
    private IEnumerator ApplyDialogueOverridesNextFrame()
    {
        // Wait a frame so NPCDialogueTrigger.OnEnable() has a chance to register into the static registry
        yield return null;

        int applied = 0;
        foreach (var pair in pendingNpcDialogueOverrides)
        {
            if (string.IsNullOrWhiteSpace(pair.npcID)) continue;
            var trigger = NPCDialogueTrigger.GetByID(pair.npcID);
            if (trigger != null)
            {
                trigger.SetDialogueLines(pair.dialogueLines ?? new string[0]);
                applied++;
            }
            else
            {
                Debug.LogWarning($"[SaveLoadManager] Could not find NPC with ID '{pair.npcID}' to apply saved dialogue.");
            }
        }
        Debug.Log($"[SaveLoadManager] Applied {applied} NPC dialogue overrides.");
        pendingNpcDialogueOverrides = null;
    }
    private IEnumerator ApplyPendingTimeOfDayNextFrame(float timeHours)
    {
        // Wait a frame so other components (DayNightCycle.Start etc.) run.
        yield return null;

        // Try a few more frames in case something else initializes slightly later.
        int attempts = 0;
        DayNightCycle dnc = null;
        while (attempts < 5)
        {
            dnc = FindObjectOfType<DayNightCycle>();
            if (dnc != null) break;
            attempts++;
            yield return null;
        }

        if (dnc != null)
        {
            // Snap to the saved hour. If you'd like an animated restore, replace 0f with seconds (e.g. 1f).
            dnc.SetTimeOfDay(timeHours, 0f);
            Debug.Log($"[SaveLoadManager] Applied saved timeOfDay = {timeHours}h to DayNightCycle after {attempts + 1} frame(s).");
        }
        else
        {
            Debug.LogWarning("[SaveLoadManager] DayNightCycle not found to apply saved timeOfDay (tried several frames).");
        }
    }

    private IEnumerator ApplyPendingInventoryNextFrame()
    {
        // Wait a frame so InventoryManager / InventoryUI have a chance to initialize
        yield return null;

        int attempts = 0;
        while (attempts < 10 && InventoryManager.Instance == null)
        {
            // try to find an InventoryManager in the scene (it may set Instance in Awake)
            var im = FindObjectOfType<InventoryManager>();
            attempts++;
            yield return null;
        }

        if (pendingInventoryData != null)
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.LoadInventory(pendingInventoryData, pendingEquippedItem ?? "", itemDatabase);
                // Defensive: ensure the InventoryManager has the scene's InventoryUI and force a UI update
                InventoryManager.Instance.inventoryUI = FindObjectOfType<InventoryUI>();
                InventoryManager.Instance.inventoryUI?.UpdateInventoryUI();
                Debug.Log($"[SaveLoadManager] Applied pending inventory (items={pendingInventoryData.Count}) after {attempts + 1} frame(s).");
            }
            else
            {
                Debug.LogWarning("[SaveLoadManager] InventoryManager not found to apply saved inventory.");
            }
        }

        // Clear pending storage whether applied or not
        pendingInventoryData = null;
        pendingEquippedItem = null;
    }

    // Helper: check if a scene name is present in build settings
    private bool IsSceneInBuildSettings(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName) return true;
        }
        return false;
    }

    // Helper: validate saved position before we attempt to apply it
    private bool IsValidPosition(Vector3 pos)
    {
        if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z)) return false;
        if (float.IsInfinity(pos.x) || float.IsInfinity(pos.y) || float.IsInfinity(pos.z)) return false;

        // Reject extremely large values which are usually corrupted saves
        const float maxAbs = 100000f;
        if (Mathf.Abs(pos.x) > maxAbs || Mathf.Abs(pos.y) > maxAbs || Mathf.Abs(pos.z) > maxAbs) return false;

        return true;
    }
    private IEnumerator ApplyBaybayinStartedTasksNextFrame(BaybayinManager bay, List<string> tasks)
    {
        // wait a frame so NPCs/waypoints/agents are ready
        yield return null;

        // Pass full ordered list; LoadStartedTasks will set currentStartedTask = last element
        bay.LoadStartedTasks(tasks, applyNow: true);

        Debug.Log($"[SaveLoadManager] Rehydrated Baybayin started tasks (count={tasks.Count}). Current = '{(tasks.Count>0? tasks[tasks.Count-1] : "<none>")}'");
    }
}

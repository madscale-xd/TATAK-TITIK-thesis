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
    private HashSet<string> collectedPickupIDs = new HashSet<string>();
    private HashSet<string> interactedObjectIDs = new HashSet<string>();
    private bool? pendingJournalAvailable = null;

    // NEW: pending triggered dialogue IDs to apply after scene load
    private List<string> pendingTriggeredDialogueIDs = null;
    private List<NPCIdPair> pendingNpcIdOverrides = null;
    private List<NPCDialoguePair> pendingNpcDialogueOverrides = null;

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

        SaveData data = SaveSystem.Load(slot);
        if (data != null)
        {
            positionToLoad = data.GetPosition();
            shouldApplyPosition = true;

            pendingJournalEntries = data.journalEntries;

            // Load inventory from saved data
            InventoryManager.Instance.LoadInventory(data.inventoryItems, data.equippedItem, itemDatabase);
            collectedPickupIDs = new HashSet<string>(data.collectedPickupIDs);
            interactedObjectIDs = new HashSet<string>(data.interactedObjectIDs); // ✅ Load it back

            // NEW: restore pending journal availability
            pendingJournalAvailable = data.journalAvailable;

            // NEW: store triggered dialogue IDs to apply on next scene load
            pendingTriggeredDialogueIDs = data.triggeredDialogueIDs != null ? new List<string>(data.triggeredDialogueIDs) : new List<string>();

            // NEW: store NPC ID overrides to apply on next scene load
            pendingNpcIdOverrides = data.npcIdOverrides != null ? new List<NPCIdPair>(data.npcIdOverrides) : new List<NPCIdPair>();
            pendingNpcDialogueOverrides = data.npcDialogueOverrides != null ? new List<NPCDialoguePair>(data.npcDialogueOverrides) : new List<NPCDialoguePair>();

            Debug.Log($"Loading scene for save slot {slot} with saved position {positionToLoad}");
        }
        else
        {
            Debug.LogWarning($"No save file found in slot {slot}. Starting fresh.");
            shouldApplyPosition = false; // Ensure nothing is applied

            // set defaults
            pendingJournalAvailable = false;
            pendingTriggeredDialogueIDs = new List<string>(); // empty = none triggered
            pendingNpcIdOverrides = new List<NPCIdPair>();    // empty = no overrides
        }

        Time.timeScale = 1f;
        // safe load: only reference data if not null
        if (data != null && !string.IsNullOrEmpty(data.savedSceneName))
        {
            SceneManager.LoadScene(data.savedSceneName, LoadSceneMode.Single);
        }
        else
        {
            // <-- Change this string to pick the default scene after clearing / missing save
            string defaultScene = "WizardTower";
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

        SaveSystem.Delete(slot);
        Debug.Log($"Save slot {slot} has been cleared.");

        shouldResetInventoryAfterLoad = true; // Reset inventory on scene load

        // Clear collected pickup IDs
        collectedPickupIDs.Clear();
        interactedObjectIDs.Clear(); // ✅ Clear interacted objects

        // Journal availability reset
        pendingJournalAvailable = false;
        if (JournalAvailability.Instance != null)
            JournalAvailability.Instance.DisableJournal();

        // NEW: clear triggered-dialogue list both in memory and on next scene load
        pendingTriggeredDialogueIDs = new List<string>(); // empty means none triggered
        if (DialogueEventsManager.Instance != null)
        {
            DialogueEventsManager.Instance.ApplyTriggeredListFromSave(pendingTriggeredDialogueIDs); // immediately clear
        }

        // NEW: clear NPC ID overrides for next load (so NPCs revert to prefab defaults after reload)
        pendingNpcIdOverrides = new List<NPCIdPair>();
        // We don't attempt to modify runtime NPC components here because the scene will be reloaded.
        // After reload, OnSceneLoaded will apply pendingNpcIdOverrides (which is now empty), so defaults stay.

        LoadGame(slot); // Reload scene
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
        if (shouldResetInventoryAfterLoad)
        {
            StartCoroutine(ResetInventoryDelayed());
        }

        if (shouldApplyPosition)
        {
            StartCoroutine(SetPlayerPositionNextFrame());
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
    }

    private IEnumerator SetPlayerPositionNextFrame()
    {
        yield return null;

        AssignPlayerTransform();
        if (playerTransform == null)
        {
            yield break;
        }

        CharacterController controller = playerTransform.GetComponent<CharacterController>();
        PlayerMovement movement = playerTransform.GetComponent<PlayerMovement>();

        if (movement != null)
            movement.enabled = false;

        if (controller != null)
            controller.enabled = false;

        playerTransform.position = positionToLoad;
        Debug.Log($"[SaveLoadManager] Position restored to {positionToLoad}");

        yield return null;

        if (controller != null)
            controller.enabled = true;

        if (movement != null)
            movement.enabled = true;

        shouldApplyPosition = false;
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
}

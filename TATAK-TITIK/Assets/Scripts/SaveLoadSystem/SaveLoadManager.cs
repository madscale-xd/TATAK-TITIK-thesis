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

        SaveData data = new SaveData(
            playerTransform.position,
            JournalManager.Instance.GetEntries(),
            inventoryData,
            InventoryManager.Instance.equippedItem,
            currentScene,
            journalAvailable
        );
        data.collectedPickupIDs = new List<string>(collectedPickupIDs);
        data.interactedObjectIDs = new List<string>(interactedObjectIDs); // ✅ Add this

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

            pendingJournalAvailable = data.journalAvailable;

            Debug.Log($"Loading scene for save slot {slot} with saved position {positionToLoad}");
        }
        else
        {
            Debug.LogWarning($"No save file found in slot {slot}. Starting fresh.");
            shouldApplyPosition = false; // Ensure nothing is applied
            pendingJournalAvailable = false;
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

        pendingJournalAvailable = false;
        if (JournalAvailability.Instance != null)
            JournalAvailability.Instance.DisableJournal();

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
}

using System;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class STEAMERManager : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private string[] allowedItems = new string[] { "KulayDahon" };
    [Tooltip("Name shown when player is in range")]
    [SerializeField] private string interactionPrompt = "Press E to use steamer";
    [Tooltip("If true the GameObject will be disabled after a successful interaction.")]
    public bool disableAfterTrigger = false;

    [Header("Dahon requirement")]
    [Tooltip("How many KulayDahon are required for this interaction")]
    [SerializeField] private int dahonRequired = 10;

    [Header("Task gating")]
    [Tooltip("If non-empty, interaction (and prompt) will only be enabled while BaybayinManager.IsTaskStarted(TaskStarted) is true.")]
    public string TaskStarted = "Task15"; // default gating as requested

    [Header("Save ID (unique per scene)")]
    [SerializeField] private string customInteractableID = "";
    private string interactableID;

    [Header("References & events")]
    public BaybayinManager BayMan;
    public GameObject ActivateAfter;
    public UnityEvent onSuccessfulInteraction;
    public UnityEvent onFailedInteraction;

    // runtime
    private bool playerNearby = false;
    private bool hasInteracted = false;

    void Start()
    {
        // Build persistent ID
        interactableID = string.IsNullOrEmpty(customInteractableID)
            ? gameObject.scene.name + "_" + transform.position.ToString()
            : customInteractableID;

        // Ensure collider is trigger so OnTriggerEnter/Exit fires
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;

        // Check saved state
        if (SaveLoadManager.Instance != null && SaveLoadManager.Instance.IsObjectInteracted(interactableID))
        {
            hasInteracted = true;
            if (disableAfterTrigger)
                gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (!hasInteracted && playerNearby && Input.GetKeyDown(KeyCode.E))
        {
            if (!IsTaskAllowed())
            {
                if (string.IsNullOrWhiteSpace(TaskStarted))
                    FloatingNotifier.Instance?.ShowMessage("You can't use this right now.", Color.red);
                else
                    FloatingNotifier.Instance?.ShowMessage($"You can't use this yet. Requires task '{TaskStarted}'.", Color.red);

                onFailedInteraction?.Invoke();
                return;
            }

            TryInteract();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerNearby = true;

        if (!hasInteracted && IsTaskAllowed())
        {
            if (FloatingNotifier.Instance != null)
                FloatingNotifier.Instance.ShowMessage(interactionPrompt, Color.white);
            else
                Debug.Log(interactionPrompt);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerNearby = false;

        if (FloatingNotifier.Instance != null)
            FloatingNotifier.Instance.ShowMessage("", Color.clear);
    }

    private bool IsTaskAllowed()
    {
        if (string.IsNullOrWhiteSpace(TaskStarted))
            return true;

        if (BayMan == null)
        {
            Debug.LogWarning($"[STEAMERManager] TaskStarted='{TaskStarted}' but BayMan reference is null. Interaction will remain disabled until BayMan assigned.");
            return false;
        }

        try
        {
            return BayMan.IsTaskStarted(TaskStarted);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[STEAMERManager] Exception while checking BayMan.IsTaskStarted('{TaskStarted}'): {ex}. Treating as not started.");
            return false;
        }
    }

    public void TryInteract()
    {
        if (hasInteracted) return;

        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("[STEAMERManager] InventoryManager.Instance is null.");
            FloatingNotifier.Instance?.ShowMessage("You can't use this right now.", Color.red);
            onFailedInteraction?.Invoke();
            return;
        }

        if (!IsTaskAllowed())
        {
            FloatingNotifier.Instance?.ShowMessage($"This steamer is not ready yet (task '{TaskStarted}' not started).", Color.red);
            onFailedInteraction?.Invoke();
            return;
        }

        string equipped = InventoryManager.Instance.equippedItem;
        if (string.IsNullOrEmpty(equipped))
        {
            FloatingNotifier.Instance?.ShowMessage("You must equip KulayDahon to use the steamer.", Color.red);
            onFailedInteraction?.Invoke();
            return;
        }

        // ensure the equipped item is allowed (case-insensitive)
        bool allowed = Array.Exists(allowedItems, s => string.Equals(s, equipped, StringComparison.OrdinalIgnoreCase));
        if (!allowed)
        {
            string allowedList = string.Join(" or ", allowedItems);
            FloatingNotifier.Instance?.ShowMessage($"You need {allowedList} equipped to use this.", Color.red);
            onFailedInteraction?.Invoke();
            return;
        }

        // find the inventory entry
        InventoryItem item = InventoryManager.Instance.items.Find(i =>
            string.Equals(i.itemName, equipped, StringComparison.OrdinalIgnoreCase) && i.quantity > 0);

        if (item == null)
        {
            FloatingNotifier.Instance?.ShowMessage($"You don't actually have a {equipped} in your inventory.", Color.red);
            onFailedInteraction?.Invoke();
            return;
        }

        // Require dahonRequired units
        if (item.quantity < dahonRequired)
        {
            FloatingNotifier.Instance?.ShowMessage($"You need {dahonRequired} KulayDahon to use this.", Color.red);
            onFailedInteraction?.Invoke();
            return;
        }

        // consume required amount (subtract dahonRequired)
        item.quantity -= dahonRequired;
        if (item.quantity <= 0)
            InventoryManager.Instance.items.Remove(item);

        InventoryManager.Instance.inventoryUI?.UpdateInventoryUI();

        // Success: perform steamer logic and call Task16 on BayMan
        PerformInteraction();

        // Persist & callbacks
        hasInteracted = true;
        SaveLoadManager.Instance?.MarkObjectInteracted(interactableID);
        SaveLoadManager.Instance?.MarkPickupCollected(interactableID);

        onSuccessfulInteraction?.Invoke();

        if (disableAfterTrigger)
        {
            if (ActivateAfter != null)
            {
                try { ActivateAfter.SetActive(true); }
                catch (Exception ex) { Debug.LogWarning($"[STEAMERManager] Failed to activate 'ActivateAfter' GameObject: {ex}"); }
            }

            gameObject.SetActive(false);
        }
    }

    private void PerformInteraction()
    {
        // Main success message
        FloatingNotifier.Instance?.ShowMessage($"You steamed {dahonRequired} KulayDahon!", Color.white);

        // Trigger Baybayin task 16
        try
        {
            InventoryManager.Instance.AddItem("UsokDahon", 10);
            BayMan?.Task16();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[STEAMERManager] Exception while calling BayMan.Task16(): {ex}");
        }

        // Clear prompt just in case
        FloatingNotifier.Instance?.ShowMessage("", Color.clear);
    }

    // Helper for other systems
    public bool IsUsed() => hasInteracted;
}

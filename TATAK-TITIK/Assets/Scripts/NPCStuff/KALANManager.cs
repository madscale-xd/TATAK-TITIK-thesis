using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class KALANManager : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private string[] allowedItems = new string[] { "BowlBigas", "BowlPangkulay" };
    [Tooltip("Name shown when player is in range")]
    [SerializeField] private string interactionPrompt = "Press E to use bowl";
    [Tooltip("If true, one unit of the bowl will be consumed on success")]
    [SerializeField] private bool consumeItem = true;

    [Header("Save ID (unique per scene)")]
    [SerializeField] private string customInteractableID = "";
    private string interactableID;

    [Header("Events (optional)")]
    public UnityEvent onSuccessfulInteraction;
    public UnityEvent onFailedInteraction;

    [Header("Task gating")]
    [Tooltip("If non-empty, interaction (and prompt) will only be enabled while BaybayinManager.IsTaskStarted(TaskStarted) is true.")]
    public string TaskStarted = ""; // <-- public field labeled "TaskStarted" in inspector

    [Tooltip("If true the GameObject will be disabled after a successful interaction.")]
    public bool disableAfterTrigger = false;

    // runtime
    private bool playerNearby = false;
    private bool hasInteracted = false;
    public BaybayinManager BayMan;
    public GameObject ActivateAfter;

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
        }
    }

    void Update()
    {
        // interact with E key while nearby (same UX as your Magsasaka trigger)
        if (!hasInteracted && playerNearby && Input.GetKeyDown(KeyCode.E))
        {
            // Dynamic gating: only allow interact if the required task (if any) has started
            if (!IsTaskAllowed())
            {
                // optional helpful feedback
                if (string.IsNullOrWhiteSpace(TaskStarted))
                    FloatingNotifier.Instance?.ShowMessage("You can't interact right now.", Color.red);
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

        // show prompt only if not already interacted AND the task gating (if any) allows it
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

        // clear prompt
        if (FloatingNotifier.Instance != null)
            FloatingNotifier.Instance.ShowMessage("", Color.clear);
    }

    /// <summary>
    /// Returns true when interaction is allowed by the TaskStarted gating (or when no gating is set).
    /// </summary>
    private bool IsTaskAllowed()
    {
        if (string.IsNullOrWhiteSpace(TaskStarted))
            return true; // no gating required

        if (BayMan == null)
        {
            // If BayMan missing and a task is required, treat as not allowed and warn.
            Debug.LogWarning($"[KALANManager] TaskStarted='{TaskStarted}' but BayMan reference is null. Interaction will remain disabled until BayMan assigned.");
            return false;
        }

        try
        {
            return BayMan.IsTaskStarted(TaskStarted);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[KALANManager] Exception while checking BayMan.IsTaskStarted('{TaskStarted}'): {ex}. Treating as not started.");
            return false;
        }
    }

    /// <summary>
    /// Attempt to interact with this Kalan. Meant to be called from input or other systems.
    /// </summary>
    public void TryInteract()
    {
        if (hasInteracted) return;

        // Safety checks
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("InventoryManager.Instance is null.");
            FloatingNotifier.Instance?.ShowMessage("You can't interact right now.", Color.red);
            onFailedInteraction?.Invoke();
            return;
        }

        // Double-check gating at entry to be defensive
        if (!IsTaskAllowed())
        {
            FloatingNotifier.Instance?.ShowMessage($"This Kalan is not ready yet (task '{TaskStarted}' not started).", Color.red);
            onFailedInteraction?.Invoke();
            return;
        }

        string equipped = InventoryManager.Instance.equippedItem;
        if (string.IsNullOrEmpty(equipped))
        {
            FloatingNotifier.Instance?.ShowMessage("You must equip a bowl to use the Kalan.", Color.red);
            onFailedInteraction?.Invoke();
            return;
        }

        // Check allowed items (case-insensitive)
        bool allowed = Array.Exists(allowedItems, s => string.Equals(s, equipped, StringComparison.OrdinalIgnoreCase));
        if (!allowed)
        {
            // if player has one but not equipped, give helpful message
            InventoryItem found = InventoryManager.Instance.items.Find(i => string.Equals(i.itemName, equipped, StringComparison.OrdinalIgnoreCase) && i.quantity > 0);
            string msg = $"You need a {allowedItems[0]} or {allowedItems[1]} equipped to interact with {gameObject.name}.";
            FloatingNotifier.Instance?.ShowMessage(msg, Color.red);
            onFailedInteraction?.Invoke();
            return;
        }

        // Find the inventory entry (case-insensitive match)
        InventoryItem item = InventoryManager.Instance.items.Find(i =>
            string.Equals(i.itemName, equipped, StringComparison.OrdinalIgnoreCase) && i.quantity > 0);

        if (item == null)
        {
            // unexpected: equipped string present but not in list
            FloatingNotifier.Instance?.ShowMessage($"You don't actually have a {equipped} in your inventory.", Color.red);
            onFailedInteraction?.Invoke();
            return;
        }

        // Optionally consume one unit
        if (consumeItem)
        {
            item.quantity--;
            if (item.quantity <= 0)
                InventoryManager.Instance.items.Remove(item);

            InventoryManager.Instance.inventoryUI?.UpdateInventoryUI();
        }

        // Success: perform Kalan logic
        PerformInteraction(item.itemName);

        // Persist (only once, since hasInteracted currently makes this one-shot)
        SaveLoadManager.Instance?.MarkObjectInteracted(interactableID);
        SaveLoadManager.Instance?.MarkPickupCollected(interactableID);

        onSuccessfulInteraction?.Invoke();

        // If requested, activate another GameObject BEFORE disabling this one.
        if (disableAfterTrigger)
        {
            if (ActivateAfter != null)
            {
                try
                {
                    ActivateAfter.SetActive(true);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[KALANManager] Failed to activate 'ActivateAfter' GameObject: {ex}");
                }
            }

            // Finally deactivate this object
            gameObject.SetActive(false);
        }
    }

    private void PerformInteraction(string usedItem)
    {
        hasInteracted = true;

        // Kalan-specific outcomes (customize further as needed)
        if (string.Equals(usedItem, "BowlBigas", StringComparison.OrdinalIgnoreCase))
        {
            FloatingNotifier.Instance?.ShowMessage("You put the galapong on the Kalan!", Color.white);
            BayMan?.Task10();
            // TODO: spawn cooked rice, update world state, play animation, etc.
        }
        else if (string.Equals(usedItem, "BowlPangkulay", StringComparison.OrdinalIgnoreCase))
        {
            FloatingNotifier.Instance?.ShowMessage("You mixed the dye in on the Kalan!", Color.magenta);
            BayMan?.Task11();
            // TODO: trigger dyeing flow, change clothes, etc.
        }
        else
        {
            FloatingNotifier.Instance?.ShowMessage($"You used {usedItem} on the Kalan.", Color.cyan);
        }

        // Hide prompt just in case
        FloatingNotifier.Instance?.ShowMessage("", Color.clear);
    }

    // Helper for other systems
    public bool IsUsed() => hasInteracted;
}

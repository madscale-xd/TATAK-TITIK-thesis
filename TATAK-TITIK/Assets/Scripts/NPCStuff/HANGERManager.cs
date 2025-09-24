using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class HANGERManager : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private string[] allowedItems = new string[] { "ClothWet", "ShirtWet" };
    [Tooltip("Name shown when player is in range")]
    [SerializeField] private string interactionPrompt = "Press E to hang clothes";
    [Tooltip("If true, one unit of the used item will be consumed on success. If false, you can optionally replace the item with 'resultItemName'.")]
    [SerializeField] private bool consumeItem = false;

    [Tooltip("Optional: when consumeItem is false, the matched inventory item's name will be replaced by this value (e.g. 'ClothDry'). Leave empty to keep the original name.")]
    [SerializeField] private string resultItemName = "ClothDry";

    [Header("Save ID (unique per scene)")]
    [SerializeField] private string customInteractableID = "";
    private string interactableID;

    [Header("Events (optional)")]
    public UnityEvent onSuccessfulInteraction;
    public UnityEvent onFailedInteraction;

    [Header("Task gating")]
    [Tooltip("If non-empty, interaction (and prompt) will only be enabled while BaybayinManager.IsTaskStarted(TaskStarted) is true.")]
    public string TaskStarted = "";

    [Tooltip("If true the GameObject will be disabled after a successful interaction.")]
    public bool disableAfterTrigger = false;

    [Header("References")]
    public BaybayinManager BayMan;
    public GameObject ActivateAfter;

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
                    FloatingNotifier.Instance?.ShowMessage("You can't hang this right now.", Color.red);
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
            Debug.LogWarning($"[HANGERManager] TaskStarted='{TaskStarted}' but BayMan reference is null. Interaction will remain disabled until BayMan assigned.");
            return false;
        }

        try
        {
            return BayMan.IsTaskStarted(TaskStarted);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HANGERManager] Exception while checking BayMan.IsTaskStarted('{TaskStarted}'): {ex}. Treating as not started.");
            return false;
        }
    }

    public void TryInteract()
    {
        if (hasInteracted) return;

        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("InventoryManager.Instance is null.");
            FloatingNotifier.Instance?.ShowMessage("You can't hang this right now.", Color.red);
            onFailedInteraction?.Invoke();
            return;
        }

        if (!IsTaskAllowed())
        {
            FloatingNotifier.Instance?.ShowMessage($"This hanger is not ready yet (task '{TaskStarted}' not started).", Color.red);
            onFailedInteraction?.Invoke();
            return;
        }

        string equipped = InventoryManager.Instance.equippedItem;
        if (string.IsNullOrEmpty(equipped))
        {
            FloatingNotifier.Instance?.ShowMessage("You must equip a wet cloth to hang it.", Color.red);
            onFailedInteraction?.Invoke();
            return;
        }

        bool allowed = Array.Exists(allowedItems, s => string.Equals(s, equipped, StringComparison.OrdinalIgnoreCase));
        if (!allowed)
        {
            string allowedList = string.Join(" or ", allowedItems);
            FloatingNotifier.Instance?.ShowMessage($"You need {allowedList} equipped to hang it.", Color.red);
            onFailedInteraction?.Invoke();
            return;
        }

        InventoryItem item = InventoryManager.Instance.items.Find(i =>
            string.Equals(i.itemName, equipped, StringComparison.OrdinalIgnoreCase) && i.quantity > 0);

        if (item == null)
        {
            FloatingNotifier.Instance?.ShowMessage($"You don't actually have a {equipped} in your inventory.", Color.red);
            onFailedInteraction?.Invoke();
            return;
        }

        // --- Special-case: consume 10 Dahon for this interaction ---
        if (string.Equals(item.itemName, "Dahon", StringComparison.OrdinalIgnoreCase))
        {
            const int required = 10;

            // make sure player actually has 10
            if (item.quantity < required)
            {
                FloatingNotifier.Instance?.ShowMessage($"You need {required} Dahon to do this.", Color.red);
                onFailedInteraction?.Invoke();
                return;
            }

            // consume 10
            item.quantity -= required;
            if (item.quantity <= 0)
                InventoryManager.Instance.items.Remove(item);

            InventoryManager.Instance.inventoryUI?.UpdateInventoryUI();
            // call the interaction outcome explicitly for Dahon
            PerformInteraction("Dahon");

            // persist & callbacks (same as the normal flow)
            SaveLoadManager.Instance?.MarkObjectInteracted(interactableID);
            SaveLoadManager.Instance?.MarkPickupCollected(interactableID);
            onSuccessfulInteraction?.Invoke();

            if (disableAfterTrigger)
            {
                if (ActivateAfter != null)
                {
                    try { ActivateAfter.SetActive(true); }
                    catch (Exception ex) { Debug.LogWarning($"[HANGERManager] Failed to activate 'ActivateAfter' GameObject: {ex}"); }
                }
                gameObject.SetActive(false);
            }

            // stop further processing (we already handled this interaction)
            return;
        }

        // Handle consumption / conversion
        if (consumeItem)
        {
            item.quantity--;
            if (item.quantity <= 0)
                InventoryManager.Instance.items.Remove(item);

            InventoryManager.Instance.inventoryUI?.UpdateInventoryUI();
        }
        else if (!string.IsNullOrWhiteSpace(resultItemName))
        {
            // Convert the item into its dry counterpart (in-place)
            item.itemName = resultItemName;
            InventoryManager.Instance.inventoryUI?.UpdateInventoryUI();
        }

        // Success
        PerformInteraction(item.itemName);

        SaveLoadManager.Instance?.MarkObjectInteracted(interactableID);
        SaveLoadManager.Instance?.MarkPickupCollected(interactableID);

        onSuccessfulInteraction?.Invoke();

        if (disableAfterTrigger)
        {
            if (ActivateAfter != null)
            {
                try { ActivateAfter.SetActive(true); }
                catch (Exception ex) { Debug.LogWarning($"[HANGERManager] Failed to activate 'ActivateAfter' GameObject: {ex}"); }
            }

            gameObject.SetActive(false);
        }
    }

    private void PerformInteraction(string usedItem)
    {
        hasInteracted = true;

        if (string.Equals(usedItem, "Dahon", StringComparison.OrdinalIgnoreCase))
        {
            FloatingNotifier.Instance?.ShowMessage("Hanged leaves.", Color.white);
            BayMan?.Task12();
        }
        else if (string.Equals(usedItem, "BowlGalapong", StringComparison.OrdinalIgnoreCase))
        {
            FloatingNotifier.Instance?.ShowMessage("Painted leaves.", Color.white);
            BayMan?.Task14();
        }
        else
        {
            FloatingNotifier.Instance?.ShowMessage($"You hung {usedItem}.", Color.white);
        }

        FloatingNotifier.Instance?.ShowMessage("", Color.clear);
    }

    // Helper for other systems
    public bool IsUsed() => hasInteracted;
}
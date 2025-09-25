using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class KIKOHarvest : MonoBehaviour
{
    [Header("Requirement (consumed)")]
    [Tooltip("Item required to perform this interaction (will be consumed).")]
    public string requiredItemName = "Kiping";
    [Tooltip("Amount of required item consumed to perform the harvest.")]
    public int requiredAmount = 5;

    [Header("Reward")]
    [Tooltip("Item given to the player when harvest succeeds.")]
    public string rewardItemName = "PaypayDahon";
    [Tooltip("Amount of reward item given.")]
    public int rewardAmount = 999;

    [Header("Prompt / UI")]
    [Tooltip("Prompt shown while player is in range")]
    public string interactionPrompt = "Press E to trade 5x Kiping for 999x PaypayDahon";

    [Header("Task gating (optional)")]
    [Tooltip("If non-empty, interaction will only be allowed while BaybayinManager.IsTaskStarted(TaskStarted) is true.")]
    public string TaskStarted = "";

    [Header("Baybayin Task (optional)")]
    [Tooltip("Parameterless method name on BaybayinManager to call after successful harvest (e.g. 'Task24'). Leave empty to skip calling BayMan.")]
    public string TaskMethodName = "";

    [Header("Save ID (unique per scene)")]
    [Tooltip("Unique ID used for persistence. If empty it will be auto-generated from scene+position.")]
    public string customInteractableID = "";
    private string interactableID;

    [Header("Behaviour")]
    [Tooltip("If true the GameObject will be disabled after a successful interaction.")]
    public bool disableAfterTrigger = false;
    [Tooltip("Optional GameObject to activate after a successful interaction (activated before disabling this).")]
    public GameObject ActivateAfter;

    [Header("Optional refs / events")]
    public BaybayinManager BayMan;
    public UnityEvent onSuccessfulInteraction;
    public UnityEvent onFailedInteraction;

    [Header("Debug")]
    [Tooltip("Enable detailed debug logging.")]
    public bool debugLogs = false;

    // runtime
    private bool playerNearby = false;
    private bool hasInteracted = false;

    void Start()
    {
        interactableID = string.IsNullOrEmpty(customInteractableID)
            ? gameObject.scene.name + "_" + transform.position.ToString()
            : customInteractableID;

        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;

        if (SaveLoadManager.Instance != null && SaveLoadManager.Instance.IsObjectInteracted(interactableID))
        {
            hasInteracted = true;
            if (disableAfterTrigger)
                gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (hasInteracted) return;

        if (playerNearby && Input.GetKeyDown(KeyCode.E))
        {
            if (!IsTaskAllowed())
            {
                if (string.IsNullOrWhiteSpace(TaskStarted))
                    FloatingNotifier.Instance?.ShowMessage("You can't do that right now.", Color.red);
                else
                    FloatingNotifier.Instance?.ShowMessage($"You can't do this yet. Requires task '{TaskStarted}'.", Color.red);

                onFailedInteraction?.Invoke();
                return;
            }

            // Use InventoryManager.GetEquippedItemInfo(out name, out qty) as authoritative check
            if (InventoryManager.Instance == null)
            {
                Debug.LogWarning("[KIKOHarvest] InventoryManager.Instance is null.");
                FloatingNotifier.Instance?.ShowMessage("Inventory not available.", Color.red);
                onFailedInteraction?.Invoke();
                return;
            }

            InventoryManager.Instance.GetEquippedItemInfo(out string equippedName, out int equippedQty);

            if (debugLogs) Debug.Log($"[KIKOHarvest] Equipped => name: '{equippedName}', qty: {equippedQty}");

            if (string.IsNullOrEmpty(equippedName) || !string.Equals(equippedName, requiredItemName, StringComparison.OrdinalIgnoreCase))
            {
                FloatingNotifier.Instance?.ShowMessage($"You must have {requiredItemName} equipped to do this.", Color.red);
                onFailedInteraction?.Invoke();
                return;
            }

            if (equippedQty < requiredAmount)
            {
                FloatingNotifier.Instance?.ShowMessage($"You need {requiredAmount}x {requiredItemName} equipped (you have {equippedQty}).", Color.red);
                onFailedInteraction?.Invoke();
                return;
            }

            // Consume from equipped slot (use equippedSlot if available + fall back to searching by name)
            if (!ConsumeFromEquippedSlot(requiredAmount))
            {
                FloatingNotifier.Instance?.ShowMessage("Could not consume equipped items. Contact dev.", Color.red);
                Debug.LogWarning("[KIKOHarvest] Failed to consume required items from equipped slot.");
                onFailedInteraction?.Invoke();
                return;
            }

            // Give reward
            GiveReward();

            // Invoke Baybayin task method if configured
            TryInvokeBaybayinTask();

            // Mark used & persist
            hasInteracted = true;
            SaveLoadManager.Instance?.MarkObjectInteracted(interactableID);
            SaveLoadManager.Instance?.MarkPickupCollected(interactableID);

            onSuccessfulInteraction?.Invoke();

            // Activate and/or disable
            if (ActivateAfter != null)
            {
                try { ActivateAfter.SetActive(true); }
                catch (Exception ex) { Debug.LogWarning($"[KIKOHarvest] Failed to activate 'ActivateAfter': {ex}"); }
            }

            if (disableAfterTrigger)
                gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerNearby = true;

        if (!hasInteracted && IsTaskAllowed())
        {
            if (InventoryManager.Instance == null)
            {
                FloatingNotifier.Instance?.ShowMessage(interactionPrompt, Color.white);
                return;
            }

            InventoryManager.Instance.GetEquippedItemInfo(out string equippedName, out int equippedQty);

            bool nameOk = !string.IsNullOrEmpty(equippedName) && string.Equals(equippedName, requiredItemName, StringComparison.OrdinalIgnoreCase);

            if (nameOk && equippedQty >= requiredAmount)
            {
                FloatingNotifier.Instance?.ShowMessage(interactionPrompt, Color.white);
            }
            else if (nameOk && equippedQty > 0)
            {
                FloatingNotifier.Instance?.ShowMessage($"You need {requiredAmount}x {requiredItemName} equipped (you have {equippedQty}).", Color.yellow);
            }
            else
            {
                FloatingNotifier.Instance?.ShowMessage($"Equip {requiredAmount}x {requiredItemName} to trade.", Color.yellow);
            }

            if (debugLogs) Debug.Log($"[KIKOHarvest] OnTriggerEnter equippedName='{equippedName}' equippedQty={equippedQty} equippedSlot={InventoryManager.Instance.equippedSlot}");
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
            Debug.LogWarning($"[KIKOHarvest] TaskStarted='{TaskStarted}' but BayMan reference is null.");
            return false;
        }

        try { return BayMan.IsTaskStarted(TaskStarted); }
        catch (Exception ex)
        {
            Debug.LogWarning($"[KIKOHarvest] Exception while checking BayMan.IsTaskStarted('{TaskStarted}'): {ex}");
            return false;
        }
    }

    /// <summary>
    /// Consume amount from the equipped slot. Uses InventoryManager.equippedSlot when valid.
    /// Falls back to finding the InventoryItem by name in the items list.
    /// Updates InventoryManager.equippedItem/equippedSlot and InventoryUI accordingly.
    /// </summary>
    private bool ConsumeFromEquippedSlot(int amount)
    {
        var im = InventoryManager.Instance;
        if (im == null) return false;

        // Prefer authoritative equippedSlot index
        InventoryItem item = null;
        if (im.equippedSlot >= 0 && im.equippedSlot < im.items.Count)
        {
            item = im.items[im.equippedSlot];
            if (item == null || !string.Equals(item.itemName, im.equippedItem, StringComparison.Ordinal))
            {
                // mismatch — fallback to search by equippedItem name
                item = im.items.Find(i => string.Equals(i.itemName, im.equippedItem, StringComparison.OrdinalIgnoreCase));
            }
        }
        else
        {
            // no valid slot; find by name
            item = im.items.Find(i => string.Equals(i.itemName, im.equippedItem, StringComparison.OrdinalIgnoreCase));
        }

        if (item == null)
        {
            Debug.LogWarning("[KIKOHarvest] No InventoryItem found for equipped slot/name.");
            return false;
        }

        if (item.quantity < amount)
        {
            if (debugLogs) Debug.LogWarning($"[KIKOHarvest] Not enough quantity on item '{item.itemName}' (have {item.quantity}, need {amount}).");
            return false;
        }

        // decrement
        item.quantity -= amount;
        if (debugLogs) Debug.Log($"[KIKOHarvest] Consumed {amount}x {item.itemName}. New qty: {item.quantity}");

        // If quantity dropped to 0 remove the slot and clear equip if it referenced that slot
        if (item.quantity <= 0)
        {
            // if equippedSlot references this slot, clear equipped fields
            int slotIndex = im.items.IndexOf(item);
            if (slotIndex >= 0)
            {
                im.items.RemoveAt(slotIndex);
                if (im.equippedSlot == slotIndex)
                {
                    im.equippedSlot = -1;
                    im.equippedItem = "";
                }
                else if (im.equippedSlot > slotIndex)
                {
                    // shift equippedSlot down because we removed an earlier slot
                    im.equippedSlot--;
                }
            }
            else
            {
                // item not found by identity (unlikely) — try name-based removal
                im.items.RemoveAll(i => string.Equals(i.itemName, item.itemName, StringComparison.OrdinalIgnoreCase) && i.quantity <= 0);
                if (string.Equals(im.equippedItem, item.itemName, StringComparison.OrdinalIgnoreCase))
                {
                    im.equippedItem = "";
                    im.equippedSlot = -1;
                }
            }
        }

        // update UI
        try { im.inventoryUI?.UpdateInventoryUI(); } catch { /* ignore */ }

        return true;
    }

    private void GiveReward()
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("[KIKOHarvest] InventoryManager.Instance is null (cannot give reward).");
            FloatingNotifier.Instance?.ShowMessage("Couldn't give reward — try again later.", Color.red);
            return;
        }

        try
        {
            InventoryManager.Instance.AddItem(rewardItemName, rewardAmount);
            FloatingNotifier.Instance?.ShowMessage($"Received {rewardAmount}x {rewardItemName}.", Color.white);
            if (debugLogs) Debug.Log($"[KIKOHarvest] Gave player {rewardAmount}x {rewardItemName}.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[KIKOHarvest] Exception while adding reward: {ex}");
            FloatingNotifier.Instance?.ShowMessage("Couldn't give reward — try again later.", Color.red);
        }
    }

    private void TryInvokeBaybayinTask()
    {
        if (string.IsNullOrWhiteSpace(TaskMethodName))
            return;

        if (BayMan == null)
        {
            Debug.LogWarning($"[KIKOHarvest] TaskMethodName='{TaskMethodName}' set but BayMan reference is null.");
            return;
        }

        try
        {
            MethodInfo mi = BayMan.GetType().GetMethod(TaskMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null)
            {
                Debug.LogWarning($"[KIKOHarvest] BayMan does not contain a method named '{TaskMethodName}'.");
                return;
            }

            if (mi.GetParameters().Length > 0)
            {
                Debug.LogWarning($"[KIKOHarvest] Method '{TaskMethodName}' on BayMan expects parameters. Expected a parameterless method.");
                return;
            }

            mi.Invoke(BayMan, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[KIKOHarvest] Exception while invoking '{TaskMethodName}' on BayMan: {ex}");
        }
    }

    // Helper for other systems
    public bool IsUsed() => hasInteracted;
}
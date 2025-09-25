using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class HANGERHarvest2 : MonoBehaviour
{
    [Header("Harvest Settings")]
    [Tooltip("Item given to the player when harvested")]
    public string itemName = "Kiping";
    [Tooltip("Amount of the item given")]
    public int amount = 5;
    [Tooltip("Prompt shown while player is in range")]
    public string interactionPrompt = "Press E to harvest";

    [Header("Task gating")]
    [Tooltip("If non-empty, interaction will only be allowed while BaybayinManager.IsTaskStarted(TaskStarted) is true.")]
    public string TaskStarted = "task23"; // gated by Task23 by default

    [Header("Baybayin Task (optional)")]
    [Tooltip("Parameterless method name on BaybayinManager to call after harvesting (e.g. 'Task24'). Leave empty to skip calling BayMan.")]
    public string TaskMethodName = "Task24"; // triggers Task24 by default

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

        // Check saved state (one-shot persistence)
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
                    FloatingNotifier.Instance?.ShowMessage("You can't harvest right now.", Color.red);
                else
                    FloatingNotifier.Instance?.ShowMessage($"You can't do this yet. Requires task '{TaskStarted}'.", Color.red);

                onFailedInteraction?.Invoke();
                return;
            }

            TryHarvest();
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
            Debug.LogWarning($"[HANGERHarvest2] TaskStarted='{TaskStarted}' but BayMan reference is null. Interaction will remain disabled until BayMan assigned.");
            return false;
        }

        try
        {
            return BayMan.IsTaskStarted(TaskStarted);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HANGERHarvest2] Exception while checking BayMan.IsTaskStarted('{TaskStarted}'): {ex}. Treating as not started.");
            return false;
        }
    }

    /// <summary>
    /// Perform the harvest: give items, call BayMan task (if configured), persist state, fire events and optionally disable.
    /// </summary>
    public void TryHarvest()
    {
        if (hasInteracted) return;

        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("[HANGERHarvest2] InventoryManager.Instance is null.");
            FloatingNotifier.Instance?.ShowMessage("You can't harvest right now.", Color.red);
            onFailedInteraction?.Invoke();
            return;
        }

        // Give the player the item(s)
        InventoryManager.Instance.AddItem(itemName, amount);

        // Feedback
        FloatingNotifier.Instance?.ShowMessage($"Collected {amount}x {itemName}.", Color.white);

        // Invoke BaybayinManager task method (if configured)
        TryInvokeBaybayinTask();

        // Mark used & persist
        hasInteracted = true;
        SaveLoadManager.Instance?.MarkObjectInteracted(interactableID);
        SaveLoadManager.Instance?.MarkPickupCollected(interactableID);

        onSuccessfulInteraction?.Invoke();

        // Activate another object if requested, then disable this
        if (disableAfterTrigger)
        {
            if (ActivateAfter != null)
            {
                try { ActivateAfter.SetActive(true); }
                catch (Exception ex) { Debug.LogWarning($"[HANGERHarvest2] Failed to activate 'ActivateAfter' GameObject: {ex}"); }
            }
            gameObject.SetActive(false);
        }
    }

    private void TryInvokeBaybayinTask()
    {
        if (string.IsNullOrWhiteSpace(TaskMethodName))
            return;

        if (BayMan == null)
        {
            Debug.LogWarning($"[HANGERHarvest2] TaskMethodName='{TaskMethodName}' set but BayMan reference is null.");
            return;
        }

        try
        {
            MethodInfo mi = BayMan.GetType().GetMethod(TaskMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null)
            {
                Debug.LogWarning($"[HANGERHarvest2] BayMan does not contain a method named '{TaskMethodName}'.");
                return;
            }

            if (mi.GetParameters().Length > 0)
            {
                Debug.LogWarning($"[HANGERHarvest2] Method '{TaskMethodName}' on BayMan expects parameters. Expected a parameterless method.");
                return;
            }

            mi.Invoke(BayMan, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HANGERHarvest2] Exception while invoking '{TaskMethodName}' on BayMan: {ex}");
        }
    }

    // Helper for other systems
    public bool IsUsed() => hasInteracted;
}

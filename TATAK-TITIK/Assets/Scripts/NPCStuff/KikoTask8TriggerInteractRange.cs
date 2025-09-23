using UnityEngine;

/// <summary>
/// Attach to the Kiko interact-range GameObject (must have a trigger collider).
/// When the player enters, checks BaybayinManager.IsTaskStarted("task7") first,
/// then BaybayinManager.CheckWholeInventory(itemName, requiredQty).
/// If the requirement is met, calls BaybayinManager.Task8().
/// </summary>
[RequireComponent(typeof(Collider))]
public class KikoTask8TriggerInteractRange : MonoBehaviour
{
    [Tooltip("Reference to your BaybayinManager (assign in inspector)")]
    public BaybayinManager baybayinManager;

    [Tooltip("Name of item to check (case-insensitive)")]
    public string itemName = "Dahon";

    [Tooltip("Required total quantity to start Task8")]
    public int requiredQty = 10;

    [Tooltip("Only fire once")]
    public bool triggerOnce = true;

    [Tooltip("Disable this GameObject after successful trigger")]
    public bool disableAfterTrigger = true;

    [Tooltip("Debug logs")]
    public bool debugLogs = false;

    bool hasTriggered = false;

    void Reset()
    {
        // convenience: make collider a trigger by default
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col == null)
            Debug.LogWarning($"[KikoInteractRangeTrigger:{name}] No Collider found.");
        else if (!col.isTrigger)
            Debug.LogWarning($"[KikoInteractRangeTrigger:{name}] Collider is not set to isTrigger (recommended).");
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered && triggerOnce) return;

        if (!other.CompareTag("Player"))
        {
            if (debugLogs) Debug.Log($"[KikoInteractRangeTrigger] Entered by non-player: {other.name}");
            return;
        }

        if (baybayinManager == null)
        {
            Debug.LogWarning("[KikoInteractRangeTrigger] baybayinManager not assigned.");
            return;
        }

        // NEW: require that BaybayinManager's current started task is "task7"
        if (!baybayinManager.IsTaskStarted("task7"))
        {
            if (debugLogs) Debug.Log("[KikoInteractRangeTrigger] Current started task is not 'task7' — ignoring trigger.");
            return;
        }

        if (debugLogs) Debug.Log("[KikoInteractRangeTrigger] Player entered interact range AND Task7 is active — checking inventory.");

        bool ok = InventoryManager.Instance.CheckWholeInventory(itemName, requiredQty);

        if (ok)
        {
            if (debugLogs) Debug.Log($"[KikoInteractRangeTrigger] Requirement met ({requiredQty}x {itemName}). Calling Task8().");
            try
            {
                baybayinManager.Task8();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[KikoInteractRangeTrigger] Exception calling Task8(): {ex}");
            }

            hasTriggered = true;
            if (disableAfterTrigger) gameObject.SetActive(false);
        }
        else
        {
            if (debugLogs) Debug.Log($"[KikoInteractRangeTrigger] Not enough '{itemName}' (need {requiredQty}).");
            // Optional: show message to player here (FloatingNotifier, UI prompt, etc.)
            // FloatingNotifier.Instance?.ShowMessage($"You need {requiredQty} {itemName} to proceed.", Color.white);
        }
    }
}

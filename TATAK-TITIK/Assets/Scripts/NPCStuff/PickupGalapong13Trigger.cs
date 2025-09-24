using System;
using UnityEngine;

/// <summary>
/// Listens for a pickup (by uniqueID) being marked collected in SaveLoadManager,
/// and invokes BaybayinManager.Task13() once when that happens.
/// 
/// Attach to the same GameObject as the pickup (or anywhere). If uniqueID is left empty
/// it will be auto-generated the same way ItemPickup does (sceneName + position), so
/// placing this on the same object will match automatically.
/// 
/// This component does NOT perform any pickup logic itself — it simply reacts when
/// the pickup has been collected by the existing pickup system.
/// </summary>
[DisallowMultipleComponent]
public class PickupGalapong13Trigger : MonoBehaviour
{
    [Tooltip("Unique ID of the pickup this script should watch. Leave empty to auto-generate from scene+position.")]
    public string uniqueID;

    [Tooltip("Reference to BaybayinManager to notify when pickup is collected.")]
    public BaybayinManager BayMan;

    [Tooltip("If true and the pickup is already marked collected at Start(), Task13 will be invoked immediately.")]
    public bool invokeIfAlreadyCollectedOnStart = false;

    // internal
    private bool hasBeenCollected = false;

    void Start()
    {
        if (string.IsNullOrWhiteSpace(uniqueID))
            uniqueID = gameObject.scene.name + "_" + transform.position.ToString();

        if (SaveLoadManager.Instance == null)
        {
            // Save system missing — nothing to watch yet. We'll poll in Update() until it's present.
            hasBeenCollected = false;
            return;
        }

        hasBeenCollected = SaveLoadManager.Instance.IsPickupCollected(uniqueID);

        if (hasBeenCollected && invokeIfAlreadyCollectedOnStart)
        {
            InvokeTask13Safe();
        }
    }

    void Update()
    {
        // Wait until SaveLoadManager exists
        if (SaveLoadManager.Instance == null) return;

        // If we already detected collection, nothing more to do
        if (hasBeenCollected) return;

        bool nowCollected = SaveLoadManager.Instance.IsPickupCollected(uniqueID);
        if (nowCollected)
        {
            hasBeenCollected = true;
            InvokeTask13Safe();

            // We can disable this component — its job is done
            enabled = false;
        }
    }

    private void InvokeTask13Safe()
    {
        if (BayMan == null)
        {
            Debug.LogWarning($"[PickupGalapong13Trigger] BayMan not assigned. Pickup '{uniqueID}' was collected but cannot notify BaybayinManager.");
            return;
        }

        try
        {
            // Direct call to Task13 — change this if you need a different method
            BayMan.Task13();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PickupGalapong13Trigger] Exception calling BayMan.Task13(): {ex}");
        }
    }
}

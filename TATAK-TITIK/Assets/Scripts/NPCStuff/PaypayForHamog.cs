using System;
using UnityEngine;


/// <summary>
/// Checks the player's currently equipped item (polled each frame).
/// When the equipped item name matches paypayItemName and qty>0 this shows the FloatingNotifier prompt.
/// Pressing E while that item is equipped calls the assigned HamogManager to fade the fog.
/// No trigger colliders and no reflection are used.
/// </summary>
public class PaypayForHamog : MonoBehaviour
{
    [Header("References")]
    [Tooltip("HamogManager responsible for fading the fog CanvasGroup. Assign in inspector.")]
    public HamogManager hamogManager;

    [Tooltip("BaybayinManager to call FinalTask() on after fog is cleared (optional).")]
    public BaybayinManager BayMan;

    [Header("Paypay settings")]
    [Tooltip("Name of the equipped item that can clear the fog.")]
    public string paypayItemName = "PaypayDahon";

    [Tooltip("Text shown when the player can use the equipped item.")]
    public string usablePrompt = "Press E to blow the mist away.";

    [Header("Behavior")]
    [Tooltip("If true the interaction only works once.")]
    public bool triggerOnce = true;

    [Tooltip("If true, disable this GameObject after successful use.")]
    public bool disableAfterTrigger = false;

    [Header("Debug")]
    public bool debugLogs = false;

    // runtime
    bool hasTriggered = false;

    // track equip state to avoid re-showing the same message every frame
    string lastEquippedName = null;
    int lastEquippedQty = 0;

    void Awake()
    {
        // optional helpful warning if InventoryManager isn't present at start
        if (InventoryManager.Instance == null && debugLogs)
            Debug.LogWarning("[PaypayForHamog] InventoryManager.Instance is null at Awake. This component will poll for it at runtime.");
    }

    void Update()
    {
        if (hasTriggered && triggerOnce) return;

        if (InventoryManager.Instance == null)
        {
            // keep trying each frame; avoids hard dependency on startup order
            if (debugLogs) Debug.LogWarning("[PaypayForHamog] InventoryManager.Instance is null.");
            return;
        }

        InventoryManager.Instance.GetEquippedItemInfo(out string equippedName, out int equippedQty);

        // Only react when equip state actually changed
        if (!string.Equals(equippedName, lastEquippedName, StringComparison.Ordinal) || equippedQty != lastEquippedQty)
        {
            lastEquippedName = equippedName;
            lastEquippedQty = equippedQty;

            if (string.Equals(equippedName, paypayItemName, StringComparison.OrdinalIgnoreCase) && equippedQty > 0)
            {
                FloatingNotifier.Instance?.ShowMessage(usablePrompt, Color.white);
                if (debugLogs) Debug.Log("[PaypayForHamog] Paypay equipped -> showing usable prompt.");
            }
            else
            {
                if (string.IsNullOrEmpty(equippedName))
                    FloatingNotifier.Instance?.ShowMessage($"Equip {paypayItemName} to blow the mist away.", Color.yellow);
                else
                    FloatingNotifier.Instance?.ShowMessage($"Equip {paypayItemName} to blow the mist away (you have {equippedName}).", Color.yellow);

                if (debugLogs) Debug.Log("[PaypayForHamog] Paypay not equipped -> showing hint.");
            }
        }

        // If the player currently has the paypay equipped, allow pressing E to trigger the flow
        if (string.Equals(lastEquippedName, paypayItemName, StringComparison.OrdinalIgnoreCase) && lastEquippedQty > 0)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (debugLogs) Debug.Log("[PaypayForHamog] E pressed with Paypay equipped -> executing blow flow.");
                StartBlowAwayFlow();
            }
        }
    }

    private void StartBlowAwayFlow()
    {
        // protect against missing HamogManager
        if (hamogManager == null)
        {
            hamogManager = FindObjectOfType<HamogManager>();
            if (hamogManager == null)
            {
                Debug.LogWarning("[PaypayForHamog] hamogManager not assigned and none found in scene.");
                FloatingNotifier.Instance?.ShowMessage("Can't clear mist right now.", Color.red);
                return;
            }
        }

        // call fade to alpha 0 over 5 seconds
        try
        {
            hamogManager.FadeCanvasToAlpha(0, 5); // fade to 0% alpha in 5 seconds
            FloatingNotifier.Instance?.ShowMessage("You blew the mist away...", Color.white);
            if (debugLogs) Debug.Log("[PaypayForHamog] Called hamogManager.FadeCanvasToAlpha(0,5).");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PaypayForHamog] Exception calling hamogManager.FadeCanvasToAlpha: {ex}");
            FloatingNotifier.Instance?.ShowMessage("Something went wrong clearing the mist.", Color.red);
            return;
        }

        // Call BayMan.FinalTask() directly if available (no reflection)
        if (BayMan == null)
        {
            BayMan = FindObjectOfType<BaybayinManager>();
        }

        if (BayMan != null)
        {
            try
            {
                BayMan.FinalTask();
                if (debugLogs) Debug.Log("[PaypayForHamog] Called BayMan.FinalTask().");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PaypayForHamog] Exception calling BayMan.FinalTask(): {ex}");
            }
        }
        else
        {
            if (debugLogs) Debug.LogWarning("[PaypayForHamog] BayMan not assigned and none found in scene; skipping FinalTask call.");
        }

        hasTriggered = true;

        // Clear prompt and optionally disable this GameObject
        FloatingNotifier.Instance?.ShowMessage("", Color.clear);

        if (disableAfterTrigger)
            gameObject.SetActive(false);
    }

    // Expose the ability for other scripts to reset this trigger (optional)
    public void ResetTrigger()
    {
        hasTriggered = false;
    }
}

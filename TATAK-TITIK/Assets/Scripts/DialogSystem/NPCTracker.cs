using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class NPCTracker : MonoBehaviour
{
    [Header("Tracking Settings")]
    public float rotationSpeed = 5f;

    [Header("Nav rotation tuning (Inspector)")]
    [Tooltip("Rotation speed to set on the NavMeshNPCController while a player is nearby.")]
    public float playerNearbyRotationSpeed = 180f;
    [Tooltip("Delay (seconds) after re-enabling the NavMeshNPCController before restoring the original rotationSpeed.")]
    public float restoreDelay = 0.5f;

    [Header("Floating Text Reference")]
    public TextMeshPro floatingText;       // Your 3D world-space TMP text
    public float fadeSpeed = 2f;

    [Header("Nav Controller Interaction")]
    [Tooltip("If true, the NavMeshNPCController component on the same GameObject will be disabled while the player is in range.")]
    public bool disableNavControllerWhenTracking = true;

    [Header("NavAgent Stop Options (DO NOT disable the agent component!)")]
    [Tooltip("When true, the script will pause the NavMeshAgent (agent.isStopped = true). This will NOT disable the NavMeshAgent component.")]
    public bool stopNavAgent = true;
    [Tooltip("If true, call ResetPath() when stopping (clears destination). If you want to resume exactly where it planned to go, leave false.")]
    public bool clearPathWhenStopped = false;

    [Header("Other Movement Options")]
    [Tooltip("Disable Animator.applyRootMotion while player is in range.")]
    public bool disableAnimatorRootMotion = true;
    [Tooltip("Disable Rigidbody-based movement while player is in range (zero velocities & set kinematic).")]
    public bool disableRigidbodyMovement = false;

    [Tooltip("Extra behaviours (custom movement scripts) to disable while player is in range.")]
    public Behaviour[] extraBehavioursToDisable;

    // runtime
    private Transform playerTarget = null;
    private Quaternion defaultRotation;
    private bool isTracking = false;

    private Camera mainCam;
    private Color originalColor;
    private float currentAlpha = 0f;
    private Coroutine hideCoroutine = null;

    // NavMeshNPCController reference & state
    private NavMeshNPCController navController;
    private bool navControllerWasEnabledBefore = false;

    // NavMeshAgent references & saved states
    private UnityEngine.AI.NavMeshAgent localAgent;
    private bool agentWasStoppedBeforeTrigger = false;
    private bool hadPathBeforeTrigger = false;
    private Vector3 savedDestination = Vector3.zero;

    // Animator / Rigidbody
    private Animator localAnimator;
    private bool animatorWasEnabledBefore = false;
    private bool animatorRootMotionBefore = false;

    private Rigidbody localRigidbody;
    private bool rbKinematicBefore = false;
    private Vector3 rbVelocityBefore = Vector3.zero;
    private Vector3 rbAngularBefore = Vector3.zero;
    private float suppressTrackingUntil = 0f;

    // extra behaviours saved states
    private List<bool> extraWasEnabled = new List<bool>();

    // trigger / player tracking
    private int playerInsideCount = 0;

    // rotation-speed restore (private, non-serialized so inspector won't interfere)
    private float originalNavRotationSpeed = float.NaN;
    private bool hasSavedOriginal = false;
    private Coroutine restoreRotationCoroutine = null;

    // NavMeshAgent saved settings for zeroing movement (new)
    private float savedAgentSpeed = 0f;
    private float savedAgentAcceleration = 0f;
    private bool savedAgentUpdatePosition = true;
    private bool hasSavedAgentMovementSettings = false;

    void Start()
    {
        defaultRotation = transform.rotation;
        mainCam = Camera.main;

        if (floatingText != null)
        {
            originalColor = floatingText.color;
            SetTextAlpha(0f); // Start invisible
        }

        // cache the NavMeshNPCController (may be null)
        navController = GetComponent<NavMeshNPCController>();

        // cache optional movement components
        localAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        localAnimator = GetComponentInChildren<Animator>(true);
        localRigidbody = GetComponent<Rigidbody>();

        // initialize extraWasEnabled list to match length
        if (extraBehavioursToDisable != null)
        {
            extraWasEnabled.Clear();
            for (int i = 0; i < extraBehavioursToDisable.Length; i++)
                extraWasEnabled.Add(false);
        }

        // Safety note in case someone mistakenly enabled a flag that would disable the NavMeshAgent component:
        if (localAgent == null && (stopNavAgent || clearPathWhenStopped))
        {
            Debug.LogWarning($"[NPCTracker] No NavMeshAgent found on '{name}' but stopNavAgent/clearPathWhenStopped is set.");
        }
    }

    void Update()
    {
        // Rotate NPC locally only (we'll disable NavMesh controller when tracking if requested)
        if (isTracking && playerTarget != null)
        {
            Vector3 direction = (playerTarget.position - transform.position).normalized;
            direction.y = 0f;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }
        else
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, defaultRotation, Time.deltaTime * rotationSpeed);
        }

        // Handle floating text
        if (floatingText != null && mainCam != null)
        {
            // Face the camera
            floatingText.transform.rotation = Quaternion.LookRotation(floatingText.transform.position - mainCam.transform.position);

            // Fade in/out manually by alpha (unless an immediate-show was requested and a hide coroutine is handling it)
            float targetAlpha = isTracking ? 1f : 0f;
            if (hideCoroutine == null)
                currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

            SetTextAlpha(currentAlpha);
        }
    }

    void SetTextAlpha(float alpha)
    {
        if (floatingText != null)
        {
            Color newColor = originalColor;
            newColor.a = Mathf.Clamp01(alpha);
            floatingText.color = newColor;
            currentAlpha = newColor.a;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (Time.time < suppressTrackingUntil)
        {
            // intentionally do not increment playerInsideCount or set tracking state - the NPC is ignoring the player for now
            return;
        }

        // track multiple entries safely
        playerInsideCount = Mathf.Max(0, playerInsideCount + 1);

        // first player entered -> apply "stop movement" changes
        if (playerInsideCount == 1)
        {
            // --- Rotation-speed behavior requested ---
            if (navController != null)
            {
                // cancel any pending restore
                if (restoreRotationCoroutine != null)
                {
                    StopCoroutine(restoreRotationCoroutine);
                    restoreRotationCoroutine = null;
                }

                // save original only once
                if (!hasSavedOriginal)
                {
                    originalNavRotationSpeed = navController.rotationSpeed;
                    hasSavedOriginal = true;
                }

                // set the nearby speed (inspector-configurable)
                navController.rotationSpeed = playerNearbyRotationSpeed;
            }

            // remember/disable nav controller if requested
            if (disableNavControllerWhenTracking && navController != null)
            {
                navControllerWasEnabledBefore = navController.enabled;
                if (navControllerWasEnabledBefore)
                {
                    navController.enabled = false;
                }
            }

            // STOP the NavMeshAgent movement (do NOT disable the component)
            if (stopNavAgent && localAgent != null)
            {
                agentWasStoppedBeforeTrigger = localAgent.isStopped;

                // remember whether it had a path/destination so we can restore it
                hadPathBeforeTrigger = localAgent.hasPath;
                if (hadPathBeforeTrigger)
                {
                    savedDestination = localAgent.destination;
                }

                // Save current agent movement settings so we can restore later
                if (!hasSavedAgentMovementSettings)
                {
                    savedAgentSpeed = localAgent.speed;
                    savedAgentAcceleration = localAgent.acceleration;
                    savedAgentUpdatePosition = localAgent.updatePosition;
                    hasSavedAgentMovementSettings = true;
                }

                // Prevent movement but preserve path/destination
                localAgent.isStopped = true;
                localAgent.speed = 0f;
                localAgent.acceleration = 0f;

                // Pin the agent so its internal steering won't nudge the transform:
                // - nextPosition is writeable: set it to the current transform to avoid drift
                localAgent.nextPosition = transform.position;

                // Optionally stop the agent from updating the transform while stopped.
                // This is the safest to prevent visual sliding. Restore this flag when resuming.
                localAgent.updatePosition = false;

                // Optionally clear path (only if you want the NPC to forget where it was going)
                if (clearPathWhenStopped)
                {
                    localAgent.ResetPath();
                    // if you ResetPath but still want to resume to savedDestination later you'll re-SetDestination on restore
                }
            }

            // Animator modifications
            if (localAnimator != null)
            {
                animatorWasEnabledBefore = localAnimator.enabled;
                animatorRootMotionBefore = localAnimator.applyRootMotion;

                if (disableAnimatorRootMotion)
                    localAnimator.applyRootMotion = false;
            }

            // Rigidbody modifications
            if (disableRigidbodyMovement && localRigidbody != null)
            {
                rbKinematicBefore = localRigidbody.isKinematic;
                rbVelocityBefore = localRigidbody.velocity;
                rbAngularBefore = localRigidbody.angularVelocity;

                // stop physical movement and make kinematic so physics won't move it
                localRigidbody.velocity = Vector3.zero;
                localRigidbody.angularVelocity = Vector3.zero;
                localRigidbody.isKinematic = true;
            }

            // Disable any extra behaviours and store their previous enabled state
            if (extraBehavioursToDisable != null)
            {
                extraWasEnabled.Clear();
                for (int i = 0; i < extraBehavioursToDisable.Length; i++)
                {
                    Behaviour b = extraBehavioursToDisable[i];
                    if (b == null)
                    {
                        extraWasEnabled.Add(false);
                        continue;
                    }

                    extraWasEnabled.Add(b.enabled);
                    b.enabled = false;
                }
            }
        }

        // set target & tracking (still done every enter to update playerTarget to the latest player)
        playerTarget = other.transform;
        isTracking = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        // decrement count
        playerInsideCount = Mathf.Max(0, playerInsideCount - 1);

        // only restore when the last player leaves
        if (playerInsideCount == 0)
        {
            // clear tracking state
            playerTarget = null;
            isTracking = false;

            // restore nav controller if we disabled it earlier
            if (disableNavControllerWhenTracking && navController != null && navControllerWasEnabledBefore)
            {
                navController.enabled = true;
                navControllerWasEnabledBefore = false;

                // start coroutine to restore original rotationSpeed after restoreDelay
                if (restoreRotationCoroutine != null)
                    StopCoroutine(restoreRotationCoroutine);
                restoreRotationCoroutine = StartCoroutine(RestoreNavRotationAfterDelay(restoreDelay));
            }
            else
            {
                // if we didn't disable/enable the controller (but we did set rotationSpeed), still restore after delay
                if (navController != null && hasSavedOriginal)
                {
                    if (restoreRotationCoroutine != null)
                        StopCoroutine(restoreRotationCoroutine);
                    restoreRotationCoroutine = StartCoroutine(RestoreNavRotationAfterDelay(restoreDelay));
                }
            }

            // restore NavMeshAgent state (do NOT enable/disable the component)
            if (stopNavAgent && localAgent != null)
            {
                // restore isStopped
                localAgent.isStopped = agentWasStoppedBeforeTrigger;

                // Restore movement settings if we saved them
                if (hasSavedAgentMovementSettings)
                {
                    // restore speed/accel first
                    localAgent.speed = savedAgentSpeed;
                    localAgent.acceleration = savedAgentAcceleration;

                    // Align internal agent position to transform before re-enabling updates to avoid snapping
                    localAgent.nextPosition = transform.position;

                    // restore updatePosition AFTER nextPosition is aligned
                    localAgent.updatePosition = savedAgentUpdatePosition;

                    hasSavedAgentMovementSettings = false;
                }

                // if we cleared the path earlier and we want to restore it, reassign destination
                if (clearPathWhenStopped && hadPathBeforeTrigger)
                {
                    localAgent.SetDestination(savedDestination);
                }
            }

            // restore animator
            if (localAnimator != null)
            {
                if (disableAnimatorRootMotion)
                    localAnimator.applyRootMotion = animatorRootMotionBefore;
            }

            // restore rigidbody
            if (disableRigidbodyMovement && localRigidbody != null)
            {
                localRigidbody.isKinematic = rbKinematicBefore;
                localRigidbody.velocity = rbVelocityBefore;
                localRigidbody.angularVelocity = rbAngularBefore;
            }

            // restore extra behaviours
            if (extraBehavioursToDisable != null)
            {
                for (int i = 0; i < extraBehavioursToDisable.Length; i++)
                {
                    Behaviour b = extraBehavioursToDisable[i];
                    if (b == null) continue;
                    if (i < extraWasEnabled.Count)
                        b.enabled = extraWasEnabled[i];
                }

                extraWasEnabled.Clear();
            }
        }
        else
        {
            // There are still players inside — keep the current playerTarget as-is.
        }
    }

    // restores the saved navController.rotationSpeed after delay seconds
    private IEnumerator RestoreNavRotationAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (navController != null && hasSavedOriginal)
        {
            navController.rotationSpeed = originalNavRotationSpeed;
        }

        originalNavRotationSpeed = float.NaN;
        hasSavedOriginal = false;
        restoreRotationCoroutine = null;
    }

    // -----------------------
    // Public API methods
    // -----------------------

    public void SetText(string text, bool showImmediately = false, float durationSeconds = 0f)
    {
        if (floatingText == null)
        {
            Debug.LogWarning("[NPCTracker] SetText called but floatingText is not assigned.");
            return;
        }

        floatingText.text = text ?? "";

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        if (showImmediately)
        {
            SetTextAlpha(1f);
            if (durationSeconds > 0f)
                hideCoroutine = StartCoroutine(AutoHideAfter(durationSeconds));
        }
        else
        {
            if (durationSeconds > 0f)
            {
                SetTextAlpha(1f);
                hideCoroutine = StartCoroutine(AutoHideAfter(durationSeconds));
            }
        }
    }

    public void ClearText()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
        if (floatingText != null)
        {
            floatingText.text = "";
            SetTextAlpha(0f);
        }
    }

    private IEnumerator AutoHideAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        while (currentAlpha > 0f)
        {
            currentAlpha = Mathf.MoveTowards(currentAlpha, 0f, fadeSpeed * Time.deltaTime);
            SetTextAlpha(currentAlpha);
            yield return null;
        }

        hideCoroutine = null;
    }

    public void SetFloatingText(string text, bool showImmediately = false, float durationSeconds = 0f)
    {
        SetText(text, showImmediately, durationSeconds);
    }

    public void SuppressTrackingForSeconds(float seconds)
    {
        if (seconds <= 0f) return;
        // start coroutine to handle suppression (it will restore movement immediately if needed)
        StartCoroutine(SuppressTrackingCoroutine(seconds));
    }

    private IEnumerator SuppressTrackingCoroutine(float seconds)
    {
        // If currently tracking, restore movement now so NPC can start moving.
        if (isTracking)
        {
            RestoreMovementFromTracking();   // method implemented below (extracted restore logic)
            // Reset playerInsideCount so OnTriggerExit won't try to restore again.
            playerInsideCount = 0;
            isTracking = false;
            playerTarget = null;
        }

        suppressTrackingUntil = Time.time + seconds;
        yield return new WaitForSeconds(seconds);
        suppressTrackingUntil = 0f;

        // NOTE: we deliberately do NOT force re-enter tracking here.
        // Re-entry will occur normally when the player leaves & re-enters (or you can add an overlap check here).
    }
    
    // NPCTracker.cs - add this helper (use the same restoration code you currently run when the last player leaves)
    private void RestoreMovementFromTracking()
    {
        // restore nav controller if we disabled it earlier
        if (disableNavControllerWhenTracking && navController != null && navControllerWasEnabledBefore)
        {
            navController.enabled = true;
            navControllerWasEnabledBefore = false;

            // start coroutine to restore original rotationSpeed after restoreDelay
            if (restoreRotationCoroutine != null)
                StopCoroutine(restoreRotationCoroutine);
            restoreRotationCoroutine = StartCoroutine(RestoreNavRotationAfterDelay(restoreDelay));
        }
        else
        {
            // if we didn't disable/enable the controller (but we did set rotationSpeed), still restore after delay
            if (navController != null && hasSavedOriginal)
            {
                if (restoreRotationCoroutine != null)
                    StopCoroutine(restoreRotationCoroutine);
                restoreRotationCoroutine = StartCoroutine(RestoreNavRotationAfterDelay(restoreDelay));
            }
        }

        // restore NavMeshAgent state (do NOT enable/disable the component)
        if (stopNavAgent && localAgent != null)
        {
            // restore isStopped
            localAgent.isStopped = agentWasStoppedBeforeTrigger;

            // Restore movement settings if we saved them
            if (hasSavedAgentMovementSettings)
            {
                // restore speed/accel first
                localAgent.speed = savedAgentSpeed;
                localAgent.acceleration = savedAgentAcceleration;

                // Align internal agent position to transform before re-enabling updates to avoid snapping
                localAgent.nextPosition = transform.position;

                // restore updatePosition AFTER nextPosition is aligned
                localAgent.updatePosition = savedAgentUpdatePosition;

                hasSavedAgentMovementSettings = false;
            }

            // if we cleared the path earlier and we want to restore it, reassign destination
            if (clearPathWhenStopped && hadPathBeforeTrigger)
            {
                localAgent.SetDestination(savedDestination);
            }
        }

        // restore animator
        if (localAnimator != null)
        {
            if (disableAnimatorRootMotion)
                localAnimator.applyRootMotion = animatorRootMotionBefore;
        }

        // restore rigidbody
        if (disableRigidbodyMovement && localRigidbody != null)
        {
            localRigidbody.isKinematic = rbKinematicBefore;
            localRigidbody.velocity = rbVelocityBefore;
            localRigidbody.angularVelocity = rbAngularBefore;
        }

        // restore extra behaviours
        if (extraBehavioursToDisable != null)
        {
            for (int i = 0; i < extraBehavioursToDisable.Length; i++)
            {
                Behaviour b = extraBehavioursToDisable[i];
                if (b == null) continue;
                if (i < extraWasEnabled.Count)
                    b.enabled = extraWasEnabled[i];
            }

            extraWasEnabled.Clear();
        }
    }
}

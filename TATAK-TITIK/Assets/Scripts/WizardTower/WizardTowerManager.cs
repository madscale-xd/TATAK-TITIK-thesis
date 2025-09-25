using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Robust WizardTowerManager — full script:
/// - Inspector-first wizard selection
/// - Waits for NPC to be idle before swapping NPC ID or dialogue lines (prevents mid-dialogue replacement)
/// - Polls/subscribes to DialogueEventsManager, JournalAvailability, PortalAvailability
/// - Persists portal activation via SaveLoadManager
/// - Reflection-friendly to handle different DEM signatures
/// </summary>
public class WizardTowerManager : MonoBehaviour
{
    [Header("Wizard selection (Inspector-first)")]
    [Tooltip("Prefer assigning the Wizard GameObject directly in the inspector.")]
    public GameObject wizardGameObject;

    [Tooltip("Fallback: name used to Find wizard when inspector slot is empty.")]
    public string wizardGameObjectName = "Wizard";

    [Tooltip("Fallback: tag used to Find wizard if name lookup fails.")]
    public string wizardTag = "";

    [Header("Journal")]
    [Tooltip("The (initially inactive) journal GameObject to activate when Wizard1 is triggered.")]
    public GameObject journalGameObject;

    [Header("IDs (Wizard1 -> Wizard2)")]
    public string requiredNPCID = "Wizard1"; // id DEM sends when first convo finished
    public string newNPCID = "Wizard2";      // id to change to after journal acquisition

    [Header("Wizard2 -> Wizard3 (portal)")]
    [Tooltip("DEM id to watch for to activate portal and eventually bump wizard to Wizard3")]
    public string requiredNPCID2 = "Wizard2"; // when this id is triggered, expose portal
    public string newNPCID2 = "Wizard3";      // id after portal acquisition

    [Header("New wizard dialogue lines (for testing)")]
    [TextArea(2,5)]
    public string[] newWizardDialogueLines = new string[] {
        "You have taken the journal... my new lines!",
        "More wizardy stuff..."
    };

    [Header("New wizard dialogue lines after portal (Wizard3)")]
    [TextArea(2,5)]
    public string[] newWizard3DialogueLines = new string[] {
        "You used the portal... I'm now Wizard3!",
        "New wizard3 lines..."
    };

    [Header("Portal")]
    [Tooltip("Portal object that will be activated after talking to Wizard2 (assign in inspector, initially inactive)")]
    public GameObject portalGameObject;

    [Tooltip("Optional unique ID used to persist the portal's activated state. If left empty the manager will generate one using scene + wizard name.")]
    public string portalPersistID = "";

    [Header("Optional: inspector-assignable PortalAvailability (fallbacks to PortalAvailability.Instance if null)")]
    public PortalAvailability portalAvailability;

    [Header("Polling fallback")]
    public float pollInterval = 0.25f;

    [Header("Idle-wait settings")]
    [Tooltip("How long (seconds) to wait for the NPC to finish speaking before forcing the change.")]
    public float idleWaitTimeout = 10f;

    // -----------------------
    // internal state flags
    // -----------------------
    private bool wizardTriggered = false;   // DEM reported requiredNPCID triggered
    private bool journalAcquired = false;   // JournalAvailability says available
    private bool idChanged = false;         // we've already changed the NPC id after journal
    private bool journalActivated = false;  // journal GameObject already activated

    private bool wizard2Triggered = false;  // DEM reported requiredNPCID2 triggered
    private bool portalAcquired = false;    // PortalAvailability says available (e.g. collected)
    private bool idChangedTo3 = false;      // we've already changed the NPC id to Wizard3
    private bool portalActivated = false;   // portal GameObject already activated (visible)

    // track whether player opened the Journal UI (we require this before showing portal)
    private bool journalInteracted = false;

    // subscription bookkeeping
    private bool subscribedToDEM = false;
    private bool subscribedToJournalAvailability = false;
    private bool subscribedToPortalAvailability = false;

    [Header("Optional: assign DEM directly (otherwise polls DialogueEventsManager.Instance)")]
    public DialogueEventsManager DEM;

    private void Start()
    {
        // DialogueEventsManager subscription (immediate or poll)
        if (DEM != null)
        {
            DEM.OnTriggeredAdded += HandleTriggeredAdded;
            subscribedToDEM = true;

            // handle already-triggered state
            if (DEM.IsTriggered(requiredNPCID))
                OnWizardTriggered();

            if (DEM.IsTriggered(requiredNPCID2))
                OnWizard2Triggered();
        }
        else
        {
            StartCoroutine(PollForDEMInitial());
        }

        // JournalAvailability subscription
        if (JournalAvailability.Instance != null)
        {
            JournalAvailability.Instance.OnAvailabilityChanged += HandleJournalAvailabilityChanged;
            subscribedToJournalAvailability = true;
            journalAcquired = JournalAvailability.Instance.IsAvailable();
        }
        else
        {
            StartCoroutine(PollForJournalAvailability());
        }

        // PortalAvailability: prefer inspector-assigned instance, otherwise try to find singleton
        if (portalAvailability == null)
        {
            TryPickPortalAvailabilitySingleton();
        }

        if (portalAvailability != null)
        {
            portalAvailability.OnAvailabilityChanged += HandlePortalAvailabilityChanged;
            subscribedToPortalAvailability = true;
            portalAcquired = portalAvailability.IsAvailable();
        }
        else
        {
            StartCoroutine(PollForPortalAvailability());
        }

        // Start watchers
        StartCoroutine(MonitorPortalGameObjectActive());
        StartCoroutine(MonitorJournalInteraction());

        // Restore persisted portal state (safe to wait for SaveLoadManager)
        StartCoroutine(ApplySavedPortalState());
    }

    private void OnDestroy()
    {
        if (DEM != null && subscribedToDEM)
            DEM.OnTriggeredAdded -= HandleTriggeredAdded;

        if (JournalAvailability.Instance != null && subscribedToJournalAvailability)
            JournalAvailability.Instance.OnAvailabilityChanged -= HandleJournalAvailabilityChanged;

        if (portalAvailability != null && subscribedToPortalAvailability)
            portalAvailability.OnAvailabilityChanged -= HandlePortalAvailabilityChanged;
    }

    // -----------------------------
    // Pollers for late-initialized singletons
    // -----------------------------
    private IEnumerator PollForDEMInitial()
    {
        while (DEM == null)
        {
            DEM = DialogueEventsManager.Instance;
            yield return null;
        }

        DEM.OnTriggeredAdded += HandleTriggeredAdded;
        subscribedToDEM = true;

        if (DEM.IsTriggered(requiredNPCID))
            OnWizardTriggered();

        if (DEM.IsTriggered(requiredNPCID2))
            OnWizard2Triggered();
    }

    private IEnumerator PollForJournalAvailability()
    {
        while (JournalAvailability.Instance == null)
            yield return null;

        JournalAvailability.Instance.OnAvailabilityChanged += HandleJournalAvailabilityChanged;
        subscribedToJournalAvailability = true;
        journalAcquired = JournalAvailability.Instance.IsAvailable();
    }

    private IEnumerator PollForPortalAvailability()
    {
        while (portalAvailability == null)
        {
            TryPickPortalAvailabilitySingleton();
            yield return new WaitForSeconds(pollInterval);
        }

        if (portalAvailability != null)
        {
            portalAvailability.OnAvailabilityChanged += HandlePortalAvailabilityChanged;
            subscribedToPortalAvailability = true;
            portalAcquired = portalAvailability.IsAvailable();
        }
    }

    private void TryPickPortalAvailabilitySingleton()
    {
        try
        {
            var instProp = typeof(PortalAvailability).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            if (instProp != null)
            {
                var inst = instProp.GetValue(null) as PortalAvailability;
                if (inst != null)
                    portalAvailability = inst;
            }
        }
        catch { /* ignore reflection errors */ }
    }

    // -----------------------------
    // Persistence helpers
    // -----------------------------
    private string GetPortalID()
    {
        if (!string.IsNullOrWhiteSpace(portalPersistID))
            return portalPersistID;

        string baseName = wizardGameObject != null ? wizardGameObject.name : (string.IsNullOrWhiteSpace(wizardGameObjectName) ? "Wizard" : wizardGameObjectName);
        return gameObject.scene.name + "_" + baseName + "_portal";
    }

    private void PersistPortalActivated()
    {
        string id = GetPortalID();
        if (SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.MarkObjectInteracted(id);
            SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.currentSaveSlot);
            Debug.Log($"[WizardTowerManager] Persisted portal activation with id='{id}'.");
        }
        else
        {
            Debug.LogWarning("[WizardTowerManager] PersistPortalActivated called but SaveLoadManager.Instance is null.");
        }
    }

    private IEnumerator ApplySavedPortalState()
    {
        while (SaveLoadManager.Instance == null)
            yield return null;

        string id = GetPortalID();
        if (SaveLoadManager.Instance.IsObjectInteracted(id))
        {
            if (portalGameObject != null && !portalGameObject.activeInHierarchy)
            {
                portalGameObject.SetActive(true);
                portalActivated = true;
                Debug.Log($"[WizardTowerManager] Re-applied persisted portal activation from save (id='{id}').");
            }

            if (DEM != null && DEM.IsTriggered(requiredNPCID2))
            {
                wizard2Triggered = true;
                TryChangeWizard2Id();
            }
        }
    }

    // -----------------------------
    // Watchers
    // -----------------------------
    private IEnumerator MonitorPortalGameObjectActive()
    {
        // wait until portalGameObject assigned
        while (portalGameObject == null)
            yield return null;

        bool lastActive = portalGameObject.activeInHierarchy;
        if (lastActive && !portalActivated)
        {
            portalActivated = true;
            Debug.Log("[WizardTowerManager] Portal GameObject was already active when Monitor started; marking portalActivated.");
            TryChangeWizard2Id();
        }

        while (true)
        {
            bool currentActive = portalGameObject.activeInHierarchy;
            if (currentActive && !lastActive)
            {
                portalActivated = true;
                Debug.Log("[WizardTowerManager] Detected portal GameObject activation (became active in scene).");
                PersistPortalActivated();
                TryChangeWizard2Id();
            }

            lastActive = currentActive;
            yield return null;
        }
    }

    // robust getter for SceneButtonManager.JournalPanel (field or property)
    private GameObject GetJournalPanelFromSBM(SceneButtonManager sbm)
    {
        if (sbm == null) return null;
        Type t = sbm.GetType();

        var fi = t.GetField("JournalPanel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fi != null)
        {
            try { return fi.GetValue(sbm) as GameObject; } catch { }
        }

        var pi = t.GetProperty("JournalPanel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (pi != null)
        {
            try { return pi.GetValue(sbm) as GameObject; } catch { }
        }

        return null;
    }

    private IEnumerator MonitorJournalInteraction()
    {
        SceneButtonManager sbm = null;
        while (sbm == null)
        {
            sbm = FindObjectOfType<SceneButtonManager>();
            yield return null;
        }

        var journalPanel = GetJournalPanelFromSBM(sbm);
        if (journalPanel == null)
        {
            Debug.LogWarning("[WizardTowerManager] MonitorJournalInteraction: Could not find JournalPanel on SceneButtonManager (field/property).");
            yield break;
        }

        bool lastActive = journalPanel.activeInHierarchy;
        if (lastActive && !journalInteracted)
        {
            journalInteracted = true;
            Debug.Log("[WizardTowerManager] Detected JournalPanel already active at start; marking journalInteracted.");
            if (wizard2Triggered && !portalActivated)
                ActivatePortalAndTryChange();
        }

        while (true)
        {
            bool currentActive = journalPanel.activeInHierarchy;
            if (currentActive && !lastActive)
            {
                journalInteracted = true;
                Debug.Log("[WizardTowerManager] Player opened the Journal UI — marking journalInteracted.");

                if (wizard2Triggered && !portalActivated)
                    ActivatePortalAndTryChange();
            }

            lastActive = currentActive;
            yield return null;
        }
    }

    // -----------------------------
    // DEM handler
    // -----------------------------
    private void HandleTriggeredAdded(string id)
    {
        Debug.Log($"[WizardTowerManager] HandleTriggeredAdded received id='{id}' (watching for '{requiredNPCID}' and '{requiredNPCID2}')");
        if (id == requiredNPCID)
            OnWizardTriggered();

        if (id == requiredNPCID2)
            OnWizard2Triggered();
    }

    // -----------------------------
    // Wizard1 -> Journal -> Wizard2
    // -----------------------------
    private void OnWizardTriggered()
    {
        if (wizardTriggered) return;
        wizardTriggered = true;
        Debug.Log("[WizardTowerManager] OnWizardTriggered: handling requiredNPCID.");

        if (!journalActivated && journalGameObject != null)
        {
            journalGameObject.SetActive(true);
            journalActivated = true;
            Debug.Log("[WizardTowerManager] Activated the journal GameObject because " + requiredNPCID + " was triggered.");
        }

        if (journalAcquired)
            ChangeNPCIdIfNeeded();
    }

    private void HandleJournalAvailabilityChanged(bool available)
    {
        if (!available) return;

        journalAcquired = true;
        Debug.Log("[WizardTowerManager] HandleJournalAvailabilityChanged: journal became available.");

        if (wizardTriggered)
            ChangeNPCIdIfNeeded();
    }

    // -----------------------------
    // NEW: Wait-for-idle helpers to avoid mid-dialogue swaps
    // -----------------------------
    // Best-effort detection: check common boolean indicators on NPCDialogueTrigger via reflection.
    private bool IsNPCInDialogue(NPCDialogueTrigger trigger)
    {
        if (trigger == null) return false;

        Type t = trigger.GetType();

        // Common property/field/method names that indicate "in dialogue"
        string[] idleIndicatorNames = new string[]
        {
            "IsTalking", "isTalking",
            "IsInDialogue", "isInDialogue",
            "IsDialogueActive", "isDialogueActive",
            "DialogueActive", "dialogueActive",
            "IsPlaying", "isPlaying",
            "IsActiveDialogue", "isActiveDialogue",
            "IsSpeaking", "isSpeaking"
        };

        foreach (var name in idleIndicatorNames)
        {
            // property
            var pi = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pi != null && pi.PropertyType == typeof(bool))
            {
                try { return (bool)pi.GetValue(trigger); } catch { }
            }

            // field
            var fi = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fi != null && fi.FieldType == typeof(bool))
            {
                try { return (bool)fi.GetValue(trigger); } catch { }
            }

            // method no args returning bool
            var mi = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (mi != null && mi.ReturnType == typeof(bool))
            {
                try { return (bool)mi.Invoke(trigger, null); } catch { }
            }
        }

        // If none present, return false (we can't tell) — caller will decide whether to wait or not.
        return false;
    }

    private IEnumerator WaitUntilNPCIdleAndPerformChange(NPCDialogueTrigger trigger, float timeoutSeconds, Action doChange, string debugContext)
    {
        if (trigger == null)
        {
            // nothing to wait for
            doChange?.Invoke();
            yield break;
        }

        bool canDetect = false;
        try
        {
            // call IsNPCInDialogue once to see if detection is available and whether NPC is currently speaking
            var first = IsNPCInDialogue(trigger);
            canDetect = true; // we could call the checker at least
            if (!first)
            {
                Debug.Log($"[WizardTowerManager] {debugContext}: NPC not in dialogue — performing change immediately.");
                doChange?.Invoke();
                yield break;
            }
            // if first == true, we will wait until it's false
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WizardTowerManager] {debugContext}: exception checking dialogue state: {ex}. Performing change immediately.");
            doChange?.Invoke();
            yield break;
        }

        if (!canDetect)
        {
            Debug.LogWarning($"[WizardTowerManager] {debugContext}: unable to detect dialogue state on NPCDialogueTrigger — performing change immediately to avoid blocking.");
            doChange?.Invoke();
            yield break;
        }

        float start = Time.time;
        bool becameIdle = false;
        while (Time.time - start < timeoutSeconds)
        {
            bool inDialogue = false;
            try
            {
                inDialogue = IsNPCInDialogue(trigger);
            }
            catch { inDialogue = false; }

            if (!inDialogue)
            {
                becameIdle = true;
                break;
            }

            yield return null;
        }

        if (becameIdle)
        {
            Debug.Log($"[WizardTowerManager] {debugContext}: NPC became idle — performing change now.");
            doChange?.Invoke();
        }
        else
        {
            Debug.LogWarning($"[WizardTowerManager] {debugContext}: timeout waiting for NPC to finish dialogue ({timeoutSeconds}s). Forcing change as fallback.");
            doChange?.Invoke();
        }
    }

    // NEW: prefer waiting on NPCManager.IsTalking() if available (uses the NPCManager you provided)
    private IEnumerator WaitUntilNPCManagerIdleAndPerformChange(NPCManager npcMgr, float timeoutSeconds, Action doChange, string debugContext)
    {
        if (npcMgr == null)
        {
            doChange?.Invoke();
            yield break;
        }

        bool canDetect = true;
        try
        {
            // if IsTalking() returns false, we can change immediately
            if (!npcMgr.IsTalking())
            {
                Debug.Log($"[WizardTowerManager] {debugContext}: NPCManager.IsTalking() == false — performing change immediately.");
                doChange?.Invoke();
                yield break;
            }
            // otherwise we will wait for IsTalking() to become false
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WizardTowerManager] {debugContext}: exception calling NPCManager.IsTalking(): {ex}. Falling back to trigger-reflection or performing change immediately.");
            // fall through: attempt fallback or force change
            doChange?.Invoke();
            yield break;
        }

        float start = Time.time;
        bool becameIdle = false;
        while (Time.time - start < timeoutSeconds)
        {
            bool talking = false;
            try
            {
                talking = npcMgr.IsTalking();
            }
            catch { talking = false; }

            if (!talking)
            {
                becameIdle = true;
                break;
            }

            yield return null;
        }

        if (becameIdle)
        {
            Debug.Log($"[WizardTowerManager] {debugContext}: NPCManager reported not talking — performing change now.");
            doChange?.Invoke();
        }
        else
        {
            Debug.LogWarning($"[WizardTowerManager] {debugContext}: timeout waiting for NPCManager.IsTalking() to become false ({timeoutSeconds}s). Forcing change as fallback.");
            doChange?.Invoke();
        }
    }

    // -----------------------------
    // Change NPC ID + lines (Wizard1 -> Wizard2)
    // -----------------------------
    private void ChangeNPCIdIfNeeded()
    {
        if (idChanged) return;

        string oldId = GetWizardIdForChange();
        if (string.IsNullOrWhiteSpace(oldId))
        {
            Debug.LogWarning("[WizardTowerManager] ChangeNPCIdIfNeeded: could not get NPCDialogueTrigger.GetNPCID(), falling back to GameObject name for DEM call.");
            oldId = GetWizardNameForChange();
        }

        var trigger = GetWizardNPCTrigger();
        var npcMgr = GetWizardNPCManager();

        Action performChange = () =>
        {
            Debug.Log($"[WizardTowerManager] ChangeNPCIdIfNeeded: attempting DEM change '{oldId}' -> '{newNPCID}'");
            bool changed = TryChangeNPCName(oldId, newNPCID, false);
            if (changed)
            {
                idChanged = true;
                Debug.Log($"[WizardTowerManager] DEM rename succeeded: '{oldId}' -> '{newNPCID}'.");

                // Apply new dialogue lines to the GameObject's NPC component if present
                var go = GetWizardGameObject();
                if (go != null)
                {
                    var npc = go.GetComponent<NPCDialogueTrigger>();
                    if (npc != null)
                    {
                        npc.SetDialogueLines(newWizardDialogueLines);
                        Debug.Log($"[WizardTowerManager] Applied new dialogue lines to '{go.name}'.");
                    }
                }

                if (SaveLoadManager.Instance != null)
                    SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.currentSaveSlot);
            }
            else
            {
                Debug.LogWarning("[WizardTowerManager] DEM rename failed or DEM missing — falling back to directly setting NPC component.");
                var fallbackGo = GetWizardGameObject();
                if (fallbackGo != null)
                {
                    var npc = fallbackGo.GetComponent<NPCDialogueTrigger>();
                    if (npc != null)
                    {
                        npc.SetNPCID(newNPCID);
                        npc.SetDialogueLines(newWizardDialogueLines);
                        idChanged = true;
                        Debug.Log($"[WizardTowerManager] Fallback: set NPC component id -> '{newNPCID}' and updated lines.");
                    }
                }

                if (idChanged && SaveLoadManager.Instance != null)
                    SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.currentSaveSlot);
            }
        };

        // Prefer waiting on NPCManager.IsTalking() if available
        if (npcMgr != null)
        {
            bool isTalking = false;
            try { isTalking = npcMgr.IsTalking(); } catch { isTalking = false; }

            if (isTalking)
            {
                Debug.Log("[WizardTowerManager] ChangeNPCIdIfNeeded: NPCManager reports NPC is talking — will wait until it's finished before changing ID/lines.");
                StartCoroutine(WaitUntilNPCManagerIdleAndPerformChange(npcMgr, idleWaitTimeout, performChange, "ChangeNPCIdIfNeeded"));
                return;
            }
        }

        // fallback to trigger-reflection detection
        if (trigger != null)
        {
            bool inDialogue = false;
            try { inDialogue = IsNPCInDialogue(trigger); } catch { inDialogue = false; }

            if (inDialogue)
            {
                Debug.Log("[WizardTowerManager] ChangeNPCIdIfNeeded: NPCDialogueTrigger reports NPC is in dialogue — will wait until dialogue finished before changing ID/lines.");
                StartCoroutine(WaitUntilNPCIdleAndPerformChange(trigger, idleWaitTimeout, performChange, "ChangeNPCIdIfNeeded"));
                return;
            }
        }

        // nothing to wait on: perform immediately
        performChange();
    }

    // -----------------------------
    // Wizard2 -> Portal -> Wizard3
    // -----------------------------
    private void OnWizard2Triggered()
    {
        if (wizard2Triggered) return;
        wizard2Triggered = true;
        Debug.Log("[WizardTowerManager] OnWizard2Triggered called.");

        if (!journalInteracted)
        {
            Debug.Log("[WizardTowerManager] Wizard2 triggered — waiting for player to open Journal before showing portal.");
            return;
        }

        if (portalGameObject != null && !portalActivated && !portalGameObject.activeInHierarchy)
        {
            portalGameObject.SetActive(true);
            portalActivated = true;
            Debug.Log("[WizardTowerManager] Activated the portal GameObject because Wizard2 was triggered and journal was interacted with.");
            PersistPortalActivated();
            TryChangeWizard2Id();
        }
    }

    private void HandlePortalAvailabilityChanged(bool available)
    {
        portalAcquired = available;
        Debug.Log($"[WizardTowerManager] HandlePortalAvailabilityChanged: available={available}");
        TryChangeWizard2Id();
    }

    private void TryChangeWizard2Id()
    {
        if (idChangedTo3) return;

        if (!wizard2Triggered)
        {
            Debug.Log("[WizardTowerManager] TryChangeWizard2Id: waiting for Wizard2 to be triggered.");
            return;
        }

        if (!portalActivated)
        {
            Debug.Log("[WizardTowerManager] TryChangeWizard2Id: waiting for portal to become active in the scene.");
            return;
        }

        if (!journalInteracted)
        {
            Debug.Log("[WizardTowerManager] TryChangeWizard2Id: waiting for player to interact with Journal UI.");
            return;
        }

        ChangeWizard2IdIfNeeded();
    }

    private void ChangeWizard2IdIfNeeded()
    {
        if (idChangedTo3) return;

        if (!portalActivated || !wizard2Triggered || !journalInteracted)
        {
            Debug.Log("[WizardTowerManager] ChangeWizard2IdIfNeeded called but prerequisites not met. Aborting.");
            return;
        }

        string oldId = GetWizardIdForChange();
        if (string.IsNullOrWhiteSpace(oldId))
        {
            Debug.LogWarning("[WizardTowerManager] ChangeWizard2IdIfNeeded: could not get NPCDialogueTrigger.GetNPCID(), falling back to GameObject name for DEM call.");
            oldId = GetWizardNameForChange();
        }

        var trigger = GetWizardNPCTrigger();
        var npcMgr = GetWizardNPCManager();

        Action performChange = () =>
        {
            Debug.Log($"[WizardTowerManager] ChangeWizard2IdIfNeeded: attempting DEM change '{oldId}' -> '{newNPCID2}'");
            bool changed = TryChangeNPCName(oldId, newNPCID2, false);
            if (changed)
            {
                idChangedTo3 = true;
                Debug.Log($"[WizardTowerManager] DEM rename succeeded: '{oldId}' -> '{newNPCID2}'.");

                var go = GetWizardGameObject();
                if (go != null)
                {
                    var npc = go.GetComponent<NPCDialogueTrigger>();
                    if (npc != null)
                    {
                        npc.SetDialogueLines(newWizard3DialogueLines);
                        Debug.Log($"[WizardTowerManager] Applied new Wizard3 dialogue lines to '{go.name}'.");
                    }
                }

                if (SaveLoadManager.Instance != null)
                    SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.currentSaveSlot);
            }
            else
            {
                Debug.LogWarning("[WizardTowerManager] DEM rename failed for Wizard2->Wizard3 — falling back to component change.");
                var fallbackGo = GetWizardGameObject();
                if (fallbackGo != null)
                {
                    var npc = fallbackGo.GetComponent<NPCDialogueTrigger>();
                    if (npc != null)
                    {
                        npc.SetNPCID(newNPCID2);
                        npc.SetDialogueLines(newWizard3DialogueLines);
                        idChangedTo3 = true;
                        Debug.Log("[WizardTowerManager] Fallback: set NPC component id -> '" + newNPCID2 + "' and updated lines.");
                    }
                }

                if (idChangedTo3 && SaveLoadManager.Instance != null)
                    SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.currentSaveSlot);
            }
        };

        // Prefer waiting on NPCManager.IsTalking() if available
        if (npcMgr != null)
        {
            bool isTalking = false;
            try { isTalking = npcMgr.IsTalking(); } catch { isTalking = false; }

            if (isTalking)
            {
                Debug.Log("[WizardTowerManager] ChangeWizard2IdIfNeeded: NPCManager reports NPC is talking — will wait until it's finished before changing ID/lines.");
                StartCoroutine(WaitUntilNPCManagerIdleAndPerformChange(npcMgr, idleWaitTimeout, performChange, "ChangeWizard2IdIfNeeded"));
                return;
            }
        }

        // fallback to trigger-reflection detection
        if (trigger != null)
        {
            bool inDialogue = false;
            try { inDialogue = IsNPCInDialogue(trigger); } catch { inDialogue = false; }

            if (inDialogue)
            {
                Debug.Log("[WizardTowerManager] ChangeWizard2IdIfNeeded: NPCDialogueTrigger reports NPC is in dialogue — will wait until dialogue finished before changing ID/lines.");
                StartCoroutine(WaitUntilNPCIdleAndPerformChange(trigger, idleWaitTimeout, performChange, "ChangeWizard2IdIfNeeded"));
                return;
            }
        }

        // nothing to wait on: perform immediately
        performChange();
    }

    private void ActivatePortalAndTryChange()
    {
        if (portalGameObject == null)
        {
            Debug.LogWarning("[WizardTowerManager] Cannot activate portal: portalGameObject not assigned in inspector.");
            return;
        }

        if (!portalActivated && !portalGameObject.activeInHierarchy)
        {
            portalGameObject.SetActive(true);
            portalActivated = true;
            Debug.Log("[WizardTowerManager] Activated portal GameObject due to Journal interaction.");
            PersistPortalActivated();
        }

        TryChangeWizard2Id();
    }

    // -----------------------------
    // Utilities
    // -----------------------------
    private GameObject GetWizardGameObject()
    {
        if (wizardGameObject != null) return wizardGameObject;

        if (!string.IsNullOrEmpty(wizardGameObjectName))
        {
            var go = GameObject.Find(wizardGameObjectName);
            if (go != null) return go;
        }

        if (!string.IsNullOrEmpty(wizardTag))
        {
            try
            {
                var go = GameObject.FindWithTag(wizardTag);
                if (go != null) return go;
            }
            catch { } // tag may not exist
        }

        Debug.LogWarning("[WizardTowerManager] GetWizardGameObject: could not find wizard by inspector slot, name, or tag.");
        return null;
    }

    // Prefer asking the NPCDialogueTrigger for the real NPC ID (this is the fix).
    private NPCDialogueTrigger GetWizardNPCTrigger()
    {
        var go = GetWizardGameObject();
        if (go == null) return null;
        return go.GetComponent<NPCDialogueTrigger>();
    }

    // NEW: prefer getting NPCManager so we can check IsTalking()
    private NPCManager GetWizardNPCManager()
    {
        var go = GetWizardGameObject();
        if (go == null) return null;
        return go.GetComponent<NPCManager>();
    }

    // returns the NPC ID that DEM knows about (preferred)
    private string GetWizardIdForChange()
    {
        var trigger = GetWizardNPCTrigger();
        if (trigger != null)
        {
            try
            {
                var id = trigger.GetNPCID();
                if (!string.IsNullOrWhiteSpace(id))
                    return id;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WizardTowerManager] GetWizardIdForChange: exception calling trigger.GetNPCID(): {ex}");
            }
        }

        return null;
    }

    // For DEM.ChangeNPCName reflection fallback; prefer wizardGameObject's name when no trigger exists
    private string GetWizardNameForChange()
    {
        if (wizardGameObject != null) return wizardGameObject.name;
        if (!string.IsNullOrWhiteSpace(wizardGameObjectName)) return wizardGameObjectName;
        return "Wizard";
    }

    /// <summary>
    /// Try to call DialogueEventsManager.ChangeNPCName with either (string, string, bool) or (string, string)
    /// Returns true if a call succeeded and returned true.
    /// Uses reflection to remain compatible with different DEM versions.
    /// </summary>
    private bool TryChangeNPCName(string oldName, string newName, bool moveTriggeredState = false)
    {
        var dem = DEM ?? DialogueEventsManager.Instance;
        if (dem == null)
        {
            Debug.LogWarning("[WizardTowerManager] TryChangeNPCName: DEM is null.");
            return false;
        }

        Type t = dem.GetType();

        // Try 3-arg: (string, string, bool)
        var m3 = t.GetMethod("ChangeNPCName", new Type[] { typeof(string), typeof(string), typeof(bool) });
        if (m3 != null)
        {
            try
            {
                var res = m3.Invoke(dem, new object[] { oldName, newName, moveTriggeredState });
                if (res is bool b) return b;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WizardTowerManager] TryChangeNPCName: exception invoking 3-arg ChangeNPCName: {ex}");
            }
        }

        // Try 2-arg: (string, string)
        var m2 = t.GetMethod("ChangeNPCName", new Type[] { typeof(string), typeof(string) });
        if (m2 != null)
        {
            try
            {
                var res = m2.Invoke(dem, new object[] { oldName, newName });
                if (res is bool b) return b;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WizardTowerManager] TryChangeNPCName: exception invoking 2-arg ChangeNPCName: {ex}");
            }
        }

        Debug.LogWarning("[WizardTowerManager] TryChangeNPCName: no compatible ChangeNPCName overload found on DEM or call failed.");
        return false;
    }
}
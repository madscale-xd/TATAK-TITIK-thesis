using System.Collections;
using UnityEngine;

public class WizardTowerManager : MonoBehaviour
{
    [Tooltip("Name of the Wizard GameObject in the scene (or leave empty to search by tag).")]
    public string wizardGameObjectName = "Wizard";
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

    [Header("Polling fallback")]
    public float pollInterval = 0.25f;

    // -----------------------
    // internal state flags
    // -----------------------
    // first-phase (Wizard1 -> Wizard2)
    private bool wizardTriggered = false;   // DEM reported requiredNPCID triggered
    private bool journalAcquired = false;   // JournalAvailability says available
    private bool idChanged = false;         // we've already changed the NPC id after journal
    private bool journalActivated = false;  // journal GameObject already activated

    // second-phase (Wizard2 -> Wizard3 / portal)
    private bool wizard2Triggered = false;  // DEM reported requiredNPCID2 triggered
    private bool portalAcquired = false;    // PortalAvailability says available (e.g. collected)
    private bool idChangedTo3 = false;      // we've already changed the NPC id to Wizard3
    private bool portalActivated = false;   // portal GameObject already activated (visible)
    public PortalAvailability portalAvailability;

    // NEW: track whether the player has actually opened/interacted with the Journal UI
    private bool journalInteracted = false;

    private void Start()
    {
        // Subscribe to DEM (if present) so we can react immediately when NPC dialogs are marked triggered.
        if (DialogueEventsManager.Instance != null)
        {
            DialogueEventsManager.Instance.OnTriggeredAdded += HandleTriggeredAdded;

            // If DEM already has either id triggered (e.g., loading after save), treat as triggered now
            if (DialogueEventsManager.Instance.IsTriggered(requiredNPCID))
                OnWizardTriggered();

            if (DialogueEventsManager.Instance.IsTriggered(requiredNPCID2))
                OnWizard2Triggered();
        }
        else
        {
            // fallback: poll until DEM exists, then check
            StartCoroutine(PollForDEMInitial());
        }

        // Subscribe to JournalAvailability (if present)
        if (JournalAvailability.Instance != null)
        {
            JournalAvailability.Instance.OnAvailabilityChanged += HandleJournalAvailabilityChanged;
            journalAcquired = JournalAvailability.Instance.IsAvailable();
        }
        else
        {
            StartCoroutine(PollForJournalAvailability());
        }

        // Subscribe to PortalAvailability (if present)
        if (portalAvailability != null)
        {
            portalAvailability.OnAvailabilityChanged += HandlePortalAvailabilityChanged;
            portalAcquired = portalAvailability.IsAvailable();
        }
        else
        {
            StartCoroutine(PollForPortalAvailability());
        }

        // Start watchers
        StartCoroutine(MonitorPortalGameObjectActive());
        StartCoroutine(MonitorJournalInteraction());

        // Restore persisted portal state (if any) and then finalize startup state.
        StartCoroutine(ApplySavedPortalState());
    }

    private void OnDestroy()
    {
        if (DialogueEventsManager.Instance != null)
            DialogueEventsManager.Instance.OnTriggeredAdded -= HandleTriggeredAdded;
        if (JournalAvailability.Instance != null)
            JournalAvailability.Instance.OnAvailabilityChanged -= HandleJournalAvailabilityChanged;
        if (portalAvailability != null)
            portalAvailability.OnAvailabilityChanged -= HandlePortalAvailabilityChanged;
    }

    // Fallback pollers in case singletons are created after this object
    private IEnumerator PollForDEMInitial()
    {
        while (DialogueEventsManager.Instance == null)
            yield return null;

        DialogueEventsManager.Instance.OnTriggeredAdded += HandleTriggeredAdded;

        if (DialogueEventsManager.Instance.IsTriggered(requiredNPCID))
            OnWizardTriggered();

        if (DialogueEventsManager.Instance.IsTriggered(requiredNPCID2))
            OnWizard2Triggered();
    }

    private IEnumerator PollForJournalAvailability()
    {
        while (JournalAvailability.Instance == null)
            yield return null;

        JournalAvailability.Instance.OnAvailabilityChanged += HandleJournalAvailabilityChanged;
        journalAcquired = JournalAvailability.Instance.IsAvailable();
    }

    private IEnumerator PollForPortalAvailability()
    {
        while (portalAvailability == null)
            yield return null;

        portalAvailability.OnAvailabilityChanged += HandlePortalAvailabilityChanged;
        portalAcquired = portalAvailability.IsAvailable();
    }

    // Persisted portal helper: determine ID
    private string GetPortalID()
    {
        if (!string.IsNullOrWhiteSpace(portalPersistID))
            return portalPersistID;

        return gameObject.scene.name + "_" + wizardGameObjectName + "_portal";
    }

    // When the portal is activated during gameplay, persist that fact so it will be re-applied on load.
    private void PersistPortalActivated()
    {
        string id = GetPortalID();
        if (SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.MarkObjectInteracted(id);
            // Optionally save immediately to ensure persistence; comment out if you prefer batch saves.
            SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.currentSaveSlot);
            Debug.Log($"[WizardTowerManager] Persisted portal activation with id='{id}'.");
        }
        else
        {
            Debug.LogWarning("[WizardTowerManager] PersistPortalActivated called but SaveLoadManager.Instance is null.");
        }
    }

    // Apply saved portal state on startup (safe to wait until SaveLoadManager exists)
    private IEnumerator ApplySavedPortalState()
    {
        // Wait for SaveLoadManager to initialize (if present)
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

            // If DEM already marked Wizard2 triggered, ensure we progress with the ID bump when appropriate.
            if (DialogueEventsManager.Instance != null && DialogueEventsManager.Instance.IsTriggered(requiredNPCID2))
            {
                wizard2Triggered = true;
                TryChangeWizard2Id();
            }
        }
    }

    // Watches the portalGameObject and detects when it becomes active in the scene.
    // This ensures we treat "portal activation (appearance)" separately from "portal acquired/used".
    private IEnumerator MonitorPortalGameObjectActive()
    {
        // Wait until a portal GameObject is assigned in the inspector (or becomes available)
        while (portalGameObject == null)
            yield return null;

        // Track activation changes
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
                // portal just became active
                portalActivated = true;
                Debug.Log("[WizardTowerManager] Detected portal GameObject activation (became active in scene).");

                // Persist this activation so it remains after saving/loading
                PersistPortalActivated();

                TryChangeWizard2Id();
            }

            lastActive = currentActive;
            yield return null;
        }
    }

    // NEW: Watch the SceneButtonManager -> JournalPanel to detect when the player opens the journal UI.
    // The user asked that the portal only appears after the player has actually interacted with their journal UI.
    private IEnumerator MonitorJournalInteraction()
    {
        // Wait until SceneButtonManager exists in the scene
        SceneButtonManager sbm = null;
        while (sbm == null)
        {
            sbm = FindObjectOfType<SceneButtonManager>();
            yield return null;
        }

        // If the JournalPanel reference is not assigned on the manager, we can't watch it; bail with a warning
        var journalPanelField = sbm.GetType().GetField("JournalPanel");
        if (journalPanelField == null)
        {
            Debug.LogWarning("[WizardTowerManager] MonitorJournalInteraction: SceneButtonManager doesn't expose a public JournalPanel field.");
            yield break;
        }

        GameObject journalPanel = journalPanelField.GetValue(sbm) as GameObject;
        if (journalPanel == null)
        {
            Debug.LogWarning("[WizardTowerManager] MonitorJournalInteraction: JournalPanel reference is null on SceneButtonManager.");
            yield break;
        }

        bool lastActive = journalPanel.activeInHierarchy;
        // If it is already active at start, mark interaction immediately
        if (lastActive && !journalInteracted)
        {
            journalInteracted = true;
            Debug.Log("[WizardTowerManager] Detected JournalPanel already active at start; marking journalInteracted.");
            // If Wizard2 was already triggered, activating the portal may be desired now
            if (wizard2Triggered && !portalActivated)
            {
                ActivatePortalAndTryChange();
            }
        }

        while (true)
        {
            bool currentActive = journalPanel.activeInHierarchy;
            if (currentActive && !lastActive)
            {
                // Player just opened the journal UI
                journalInteracted = true;
                Debug.Log("[WizardTowerManager] Player opened the Journal UI — marking journalInteracted.");

                // Only activate portal if the player has already triggered Wizard2 (talked to Wizard2)
                if (wizard2Triggered && !portalActivated)
                {
                    ActivatePortalAndTryChange();
                }
            }

            lastActive = currentActive;
            yield return null;
        }
    }

    // DEM event handler
    private void HandleTriggeredAdded(string id)
    {
        Debug.Log($"[WizardTowerManager] HandleTriggeredAdded received id='{id}' (watching for '{requiredNPCID}' and '{requiredNPCID2}')");
        if (id == requiredNPCID)
            OnWizardTriggered();

        if (id == requiredNPCID2)
            OnWizard2Triggered();
    }

    // --------------------
    // Wizard1 -> Journal -> Wizard2 flow
    // --------------------
    private void OnWizardTriggered()
    {
        if (wizardTriggered) return;
        wizardTriggered = true;

        if (!journalActivated && journalGameObject != null)
        {
            journalGameObject.SetActive(true);
            journalActivated = true;
            Debug.Log("[WizardTowerManager] Activated the journal GameObject because " + requiredNPCID + " was triggered.");
        }

        if (journalAcquired)
        {
            ChangeNPCIdIfNeeded();
        }
    }

    private void HandleJournalAvailabilityChanged(bool available)
    {
        if (!available) return;

        journalAcquired = true;

        if (wizardTriggered)
        {
            ChangeNPCIdIfNeeded();
        }
    }

    private void ChangeNPCIdIfNeeded()
    {
        if (idChanged) return;

        // Use DEM rename (DEM default is silent rename in your updated DEM)
        bool changed = DialogueEventsManager.Instance?.ChangeNPCName(wizardGameObjectName, newNPCID) ?? false;
        if (changed)
        {
            idChanged = true;
            Debug.Log($"[WizardTowerManager] Changed NPC '{wizardGameObjectName}' ID to '{newNPCID}' after journal acquisition.");

            var go = GameObject.Find(wizardGameObjectName);
            if (go != null)
            {
                var npc = go.GetComponent<NPCDialogueTrigger>();
                if (npc != null)
                {
                    npc.SetDialogueLines(newWizardDialogueLines);
                    Debug.Log($"[WizardTowerManager] Applied new dialogue lines to '{wizardGameObjectName}'.");
                }
            }

            if (SaveLoadManager.Instance != null)
                SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.currentSaveSlot);
        }
        else
        {
            Debug.LogWarning("[WizardTowerManager] DialogueEventsManager.ChangeNPCName returned false or DEM missing. Attempting fallback: set NPC component directly.");

            var fallbackGo = GameObject.Find(wizardGameObjectName);
            if (fallbackGo != null)
            {
                var npc = fallbackGo.GetComponent<NPCDialogueTrigger>();
                if (npc != null)
                {
                    npc.SetNPCID(newNPCID);
                    npc.SetDialogueLines(newWizardDialogueLines);
                    idChanged = true;
                }
            }

            if (idChanged && SaveLoadManager.Instance != null)
                SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.currentSaveSlot);
        }
    }

    // --------------------
    // Wizard2 -> Portal -> Wizard3 flow
    // --------------------
    private void OnWizard2Triggered()
    {
        if (wizard2Triggered) return;
        wizard2Triggered = true;

        Debug.Log("[WizardTowerManager] OnWizard2Triggered called.");

        // DON'T immediately show the portal here unless the player already interacted with the journal.
        // We'll wait for MonitorJournalInteraction to notice the player opening the Journal UI.
        if (!journalInteracted)
        {
            Debug.Log("[WizardTowerManager] Wizard2 triggered — waiting for player to open Journal before showing portal.");
            return;
        }

        // If journal has already been interacted with, we can activate the portal now (if not already activated).
        if (portalGameObject != null && !portalActivated && !portalGameObject.activeInHierarchy)
        {
            portalGameObject.SetActive(true);
            // portalActivated will be set by MonitorPortalGameObjectActive on the next frame, but set it defensively here as well
            portalActivated = true;
            Debug.Log("[WizardTowerManager] Activated the portal GameObject because Wizard2 was triggered and journal was interacted with.");

            // Persist activation
            PersistPortalActivated();

            TryChangeWizard2Id();
        }
    }

    private void HandlePortalAvailabilityChanged(bool available)
    {
        // portalAvailability likely indicates "player collected/used portal" or some availability state.
        // We record it but DO NOT use it to decide when to bump the NPC id to Wizard3 (that decision is based
        // on portal activation + wizard2 trigger + journal interaction).
        portalAcquired = available;

        Debug.Log($"[WizardTowerManager] HandlePortalAvailabilityChanged: available={available}");

        // If both conditions are already met, ensure we progress.
        TryChangeWizard2Id();
    }

    // Attempts to perform the Wizard2->Wizard3 rename only when the portal is active/visible
    // in the scene AND Wizard2 has been marked triggered by the DEM.
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

        // Additionally ensure the player has interacted with the journal before proceeding
        if (!journalInteracted)
        {
            Debug.Log("[WizardTowerManager] TryChangeWizard2Id: waiting for player to interact with Journal UI.");
            return;
        }

        // Both conditions satisfied — perform the id change.
        ChangeWizard2IdIfNeeded();
    }

    private void ChangeWizard2IdIfNeeded()
    {
        if (idChangedTo3) return;

        // Extra guard: both conditions required (defensive)
        if (!portalActivated || !wizard2Triggered || !journalInteracted)
        {
            Debug.Log("[WizardTowerManager] ChangeWizard2IdIfNeeded called but prerequisites not met. Aborting.");
            return;
        }

        bool changed = DialogueEventsManager.Instance?.ChangeNPCName(wizardGameObjectName, newNPCID2) ?? false;
        if (changed)
        {
            idChangedTo3 = true;
            Debug.Log($"[WizardTowerManager] Changed NPC '{wizardGameObjectName}' ID to '{newNPCID2}' after portal activation and Wizard2 trigger and Journal interaction.");

            var go = GameObject.Find(wizardGameObjectName);
            if (go != null)
            {
                var npc = go.GetComponent<NPCDialogueTrigger>();
                if (npc != null)
                {
                    npc.SetDialogueLines(newWizard3DialogueLines);
                    Debug.Log($"[WizardTowerManager] Applied new dialogue lines (Wizard3) to '{wizardGameObjectName}'.");
                }
            }

            if (SaveLoadManager.Instance != null)
                SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.currentSaveSlot);
        }
        else
        {
            Debug.LogWarning("[WizardTowerManager] DialogueEventsManager.ChangeNPCName returned false or DEM missing for Wizard2->Wizard3. Attempting fallback.");

            var fallbackGo = GameObject.Find(wizardGameObjectName);
            if (fallbackGo != null)
            {
                var npc = fallbackGo.GetComponent<NPCDialogueTrigger>();
                if (npc != null)
                {
                    npc.SetNPCID(newNPCID2);
                    npc.SetDialogueLines(newWizard3DialogueLines);
                    idChangedTo3 = true;
                }
            }

            if (idChangedTo3 && SaveLoadManager.Instance != null)
                SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.currentSaveSlot);
        }
    }

    // Small helper to activate portal and try the id-change (keeps duplicate logic compact)
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

            // Persist activation
            PersistPortalActivated();
        }

        TryChangeWizard2Id();
    }
}

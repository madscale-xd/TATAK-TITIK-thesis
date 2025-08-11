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

    [Header("IDs")]
    public string requiredNPCID = "Wizard1";
    public string newNPCID = "Wizard2";

    [Header("New wizard dialogue lines (for testing)")]
    [TextArea(2,5)]
    public string[] newWizardDialogueLines = new string[] {
        "You have taken the journal... my new lines!",
        "More wizardy stuff..."
    };

    [Header("Polling fallback")]
    public float pollInterval = 0.25f;

    // internal state flags
    private bool wizardTriggered = false;   // DEM reports requiredNPCID triggered
    private bool journalAcquired = false;   // JournalAvailability says available
    private bool idChanged = false;         // we've already changed the NPC id
    private bool activated = false;         // journal GameObject already activated

    private void Start()
    {
        // Subscribe to DEM (if present) so we can react immediately when the NPC triggers.
        if (DialogueEventsManager.Instance != null)
        {
            DialogueEventsManager.Instance.OnTriggeredAdded += HandleTriggeredAdded;

            // If DEM already has the id triggered, treat it as triggered now
            if (DialogueEventsManager.Instance.IsTriggered(requiredNPCID))
                OnWizardTriggered();
        }
        else
        {
            // fallback: poll until DEM exists, then check
            StartCoroutine(PollForDEMInitial());
        }

        // Subscribe to JournalAvailability changes (if present). If not present yet, we'll try lazy fallback in coroutine.
        if (JournalAvailability.Instance != null)
        {
            JournalAvailability.Instance.OnAvailabilityChanged += HandleJournalAvailabilityChanged;
            journalAcquired = JournalAvailability.Instance.IsAvailable();
        }
        else
        {
            StartCoroutine(PollForJournalAvailability());
        }
    }

    private void OnDestroy()
    {
        if (DialogueEventsManager.Instance != null)
            DialogueEventsManager.Instance.OnTriggeredAdded -= HandleTriggeredAdded;
        if (JournalAvailability.Instance != null)
            JournalAvailability.Instance.OnAvailabilityChanged -= HandleJournalAvailabilityChanged;
    }

    private IEnumerator PollForDEMInitial()
    {
        while (DialogueEventsManager.Instance == null)
            yield return null;

        DialogueEventsManager.Instance.OnTriggeredAdded += HandleTriggeredAdded;

        if (DialogueEventsManager.Instance.IsTriggered(requiredNPCID))
            OnWizardTriggered();
    }

    private IEnumerator PollForJournalAvailability()
    {
        while (JournalAvailability.Instance == null)
            yield return null;

        JournalAvailability.Instance.OnAvailabilityChanged += HandleJournalAvailabilityChanged;
        journalAcquired = JournalAvailability.Instance.IsAvailable();
    }

    private void HandleTriggeredAdded(string id)
    {
        if (id == requiredNPCID)
            OnWizardTriggered();
    }

    private void OnWizardTriggered()
    {
        if (wizardTriggered) return;
        wizardTriggered = true;

        // Activate the journal object (so player can see/pick it up)
        if (!activated && journalGameObject != null)
        {
            journalGameObject.SetActive(true);
            activated = true;
            Debug.Log("[WizardTowerManager] Activated the journal GameObject because " + requiredNPCID + " was triggered.");
        }

        // If the journal has already been acquired, immediately change the NPC ID now
        if (journalAcquired)
            ChangeNPCIdIfNeeded();  
            if (SaveLoadManager.Instance != null)
            {
                SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.currentSaveSlot);
            }
    }

    private void HandleJournalAvailabilityChanged(bool available)
    {
        if (!available) return;

        journalAcquired = true;

        // Only change the NPC id if the wizard was already triggered (i.e., we've shown the journal/pickup)
        if (wizardTriggered)
        {
            ChangeNPCIdIfNeeded();
        }
        // else: if wizardTriggered is false, when the trigger happens later OnWizardTriggered() will call ChangeNPCIdIfNeeded()
    }

    private void ChangeNPCIdIfNeeded()
    {
        if (idChanged) return;

        // Try to change the ID via the DialogueEventsManager (preferred, keeps save/trigger state transfer)
        bool changed = DialogueEventsManager.Instance?.ChangeNPCName(wizardGameObjectName, newNPCID) ?? false;
        if (changed)
        {
            idChanged = true;
            Debug.Log($"[WizardTowerManager] Changed NPC '{wizardGameObjectName}' ID to '{newNPCID}' after journal acquisition.");
        }
        else
        {
            Debug.LogWarning("[WizardTowerManager] DialogueEventsManager.ChangeNPCName returned false or DEM missing. Will still try to set dialogue lines directly.");
        }

        // Also update the NPC's dialogue lines immediately (use SetDialogueLines for testing)
        var go = GameObject.Find(wizardGameObjectName);
        if (go != null)
        {
            var npc = go.GetComponent<NPCDialogueTrigger>();
            if (npc != null)
            {
                npc.SetDialogueLines(newWizardDialogueLines);
                Debug.Log($"[WizardTowerManager] Applied new dialogue lines to '{wizardGameObjectName}'.");
            }
            else
            {
                Debug.LogWarning("[WizardTowerManager] Found the wizard GameObject but no NPCDialogueTrigger component to set lines on.");
            }
        }
        else
        {
            Debug.LogWarning("[WizardTowerManager] Could not find GameObject named: " + wizardGameObjectName);
        }

        // Optional: unsubscribe from DEM event since we're finished
        if (DialogueEventsManager.Instance != null)
            DialogueEventsManager.Instance.OnTriggeredAdded -= HandleTriggeredAdded;
    }
}

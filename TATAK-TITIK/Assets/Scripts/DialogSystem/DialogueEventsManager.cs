using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central manager for dialogue-trigger events. Maintains a set of npcIDs that have been "triggered"
/// and broadcasts OnTriggeredAdded when new IDs appear.
/// 
/// CHANGES: ChangeNPCName now takes an optional third parameter `moveTriggeredState` (default: false).
/// When false (the new default) the method will rename the NPC silently: it will NOT copy the
/// triggered state to the new ID, nor will it invoke OnTriggeredAdded. This prevents "silent"
/// activation behavior that can occur if renaming automatically marks the new ID as triggered.
/// 
/// If you want the old behavior (move triggered state and invoke listeners), call
/// ChangeNPCName(npcGameObjectName, newID, true).
/// </summary>
public class DialogueEventsManager : MonoBehaviour
{
    public static DialogueEventsManager Instance { get; private set; }
    public event Action<string> OnTriggeredAdded;

    // set of npcIDs that have been triggered
    private HashSet<string> triggeredDialogues = new HashSet<string>(StringComparer.Ordinal);

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Find GameObject by name, read its NPCDialogueTrigger.npcID and add that ID to the triggered set.
    /// Returns true if the ID was added (wasn't already present).
    /// </summary>
    public bool AddToTriggeredList(string npcGameObjectName)
    {
        if (string.IsNullOrWhiteSpace(npcGameObjectName)) return false;

        GameObject go = GameObject.Find(npcGameObjectName);
        if (go == null)
        {
            Debug.LogWarning($"DialogueEventsManager: no GameObject named '{npcGameObjectName}' found.");
            return false;
        }

        var trigger = go.GetComponent<NPCDialogueTrigger>();
        if (trigger == null)
        {
            Debug.LogWarning($"DialogueEventsManager: GameObject '{npcGameObjectName}' has no NPCDialogueTrigger.");
            return false;
        }

        string id = trigger.GetNPCID();
        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning($"DialogueEventsManager: NPC on '{npcGameObjectName}' has empty npcID.");
            return false;
        }

        bool added = triggeredDialogues.Add(id);
        if (added)
        {
            // persist if SaveLoadManager exists
            SaveLoadManager.Instance?.MarkObjectInteracted(id);
            Debug.Log($"DialogueEventsManager: Added triggered ID '{id}'.");

            OnTriggeredAdded?.Invoke(id);
        }
        return added;
    }

    /// <summary>
    /// Find GameObject by name and change its NPCDialogueTrigger.npcID to newID.
    /// By default this is a SILENT rename: it will NOT move triggered-state nor fire OnTriggeredAdded.
    /// If you want to move the triggered state to the new ID and notify listeners, pass moveTriggeredState = true.
    /// Returns true on success.
    /// </summary>
    public bool ChangeNPCName(string npcGameObjectName, string newID, bool moveTriggeredState = false)
    {
        if (string.IsNullOrWhiteSpace(npcGameObjectName) || string.IsNullOrWhiteSpace(newID)) return false;

        GameObject go = GameObject.Find(npcGameObjectName);
        if (go == null)
        {
            Debug.LogWarning($"DialogueEventsManager: no GameObject named '{npcGameObjectName}' found.");
            return false;
        }

        var trigger = go.GetComponent<NPCDialogueTrigger>();
        if (trigger == null)
        {
            Debug.LogWarning($"DialogueEventsManager: GameObject '{npcGameObjectName}' has no NPCDialogueTrigger.");
            return false;
        }

        string oldID = trigger.GetNPCID();
        trigger.SetNPCID(newID);

        // If the caller asked to move the triggered state, do so explicitly.
        if (moveTriggeredState)
        {
            if (!string.IsNullOrEmpty(oldID) && triggeredDialogues.Contains(oldID))
            {
                // Remove oldID
                triggeredDialogues.Remove(oldID);

                // Add newID and detect whether it was newly added
                bool newlyAdded = triggeredDialogues.Add(newID);

                // Persist interacted state for the newID
                SaveLoadManager.Instance?.MarkObjectInteracted(newID);

                Debug.Log($"DialogueEventsManager: Changed NPC '{npcGameObjectName}' id from '{oldID}' to '{newID}' and moved triggered state.");

                // If we just added the triggered state for newID, notify listeners immediately
                if (newlyAdded)
                {
                    Debug.Log($"DialogueEventsManager: Invoking OnTriggeredAdded for moved id '{newID}'.");
                    OnTriggeredAdded?.Invoke(newID);
                }

                return true;
            }

            // If oldID wasn't triggered (nothing to move), we simply changed the npcID on the component.
            Debug.Log($"DialogueEventsManager: Changed NPC '{npcGameObjectName}' id from '{oldID}' to '{newID}' (no triggered-state to move).");
            return true;
        }

        // SILENT rename path (default): do NOT move triggered-state, do NOT invoke any events.
        // We optionally remove the oldID from our internal set so we don't persist a stale ID that no
        // longer exists on an NPC. This keeps save data tidy. Note: if you rely on SaveLoadManager
        // having a separate record, consider implementing an "UnmarkObjectInteracted" there and call
        // it here.
        if (!string.IsNullOrEmpty(oldID) && triggeredDialogues.Contains(oldID))
        {
            triggeredDialogues.Remove(oldID);
            Debug.Log($"DialogueEventsManager: Renamed NPC '{npcGameObjectName}' id from '{oldID}' to '{newID}' silently (did NOT move triggered-state).");

            // We do NOT call SaveLoadManager.Instance?.MarkObjectInteracted(newID) because the triggered
            // state intentionally does not move. If SaveLoadManager tracks interactions elsewhere, it
            // may still contain historical records; consider exposing an Unmark method if you need to
            // remove persisted records immediately.
        }
        else
        {
            Debug.Log($"DialogueEventsManager: Renamed NPC '{npcGameObjectName}' id from '{oldID}' to '{newID}' silently (no triggered-state present).");
        }

        return true;
    }

    // Optional helper if you want to query
    public bool IsTriggered(string npcID) => !string.IsNullOrWhiteSpace(npcID) && triggeredDialogues.Contains(npcID);

    // returns a copy as a list (safe to put into SaveData)
    public List<string> GetTriggeredListForSave()
    {
        return new List<string>(triggeredDialogues);
    }

    // apply a saved list (call this on scene-load after SaveLoadManager hands it over)
    public void ApplyTriggeredListFromSave(List<string> savedIDs)
    {
        if (savedIDs == null) return;
        triggeredDialogues.Clear();
        foreach (var id in savedIDs)
            if (!string.IsNullOrWhiteSpace(id))
                triggeredDialogues.Add(id);
    }
}

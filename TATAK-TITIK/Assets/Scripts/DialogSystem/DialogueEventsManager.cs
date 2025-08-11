// DialogueEventsManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

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
    /// If the old ID existed in the triggered set, move that state to the new ID and persist.
    /// Returns true on success.
    /// </summary>
    public bool ChangeNPCName(string npcGameObjectName, string newID)
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

        if (!string.IsNullOrEmpty(oldID) && triggeredDialogues.Contains(oldID))
        {
            triggeredDialogues.Remove(oldID);
            triggeredDialogues.Add(newID);
            SaveLoadManager.Instance?.MarkObjectInteracted(newID);
        }

        Debug.Log($"DialogueEventsManager: Changed NPC '{npcGameObjectName}' id from '{oldID}' to '{newID}'.");
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

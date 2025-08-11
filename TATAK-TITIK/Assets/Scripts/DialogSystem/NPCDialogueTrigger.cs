using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NPCDialogueTrigger : MonoBehaviour
{
    [Header("Identification (must be unique per NPC)")]
    public string npcID;                 // e.g. "Wizard", "Guard_01"

    [TextArea(2, 5)]
    public string[] dialogueLines;
    private DialogueManager dialogueManager;
    private PlayerInteraction playerInteraction;

    // simple registry for quick lookups
    private static readonly Dictionary<string, NPCDialogueTrigger> registry = new Dictionary<string, NPCDialogueTrigger>();

    private void Awake()
    {
        dialogueManager = FindObjectOfType<DialogueManager>();
        playerInteraction = FindObjectOfType<PlayerInteraction>();
    }

    private void OnEnable()
    {
        if (!string.IsNullOrWhiteSpace(npcID))
        {
            // overwrite if duplicate; you may want to warn instead
            registry[npcID] = this;
        }
    }

    private void OnDisable()
    {
        if (!string.IsNullOrWhiteSpace(npcID) && registry.TryGetValue(npcID, out var existing) && existing == this)
            registry.Remove(npcID);
    }

    private void Start()
    {
        if (dialogueManager == null) Debug.LogError("DialogueManager not found in scene!");
        if (playerInteraction == null) Debug.LogError("PlayerInteraction not found in scene!");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            dialogueManager.ShowPromptFor(this);
            playerInteraction.SetCurrentNPC(this);

            // NOTE: Removed calls to DialogueEventsManager here so entering range no longer marks the dialogue as triggered.
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            dialogueManager.HidePromptFor(this);
            playerInteraction.ClearCurrentNPC(this);
        }
    }

    public string[] GetDialogueLines() => dialogueLines;

    // Public mutator method to change dialogue safely at runtime
    public void SetDialogueLines(string[] newLines)
    {
        dialogueLines = newLines;

        // If this NPC is currently showing a prompt/dialogue, hide it so UI can update cleanly
        if (dialogueManager != null)
        {
            dialogueManager.HidePromptFor(this); // HidePromptFor already checks equality internally
        }
    }

    // Static lookup helper
    public static NPCDialogueTrigger GetByID(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        registry.TryGetValue(id, out var trigger);
        return trigger;
    }

    public static IReadOnlyDictionary<string, NPCDialogueTrigger> GetAllRegistered()
    {
        return registry;
    }

    // getter for id
    public string GetNPCID() => npcID;

    // setter to change id at runtime (updates registry)
    public void SetNPCID(string newID)
    {
        // remove old
        if (!string.IsNullOrWhiteSpace(npcID) && registry.ContainsKey(npcID) && registry[npcID] == this)
        {
            registry.Remove(npcID);
        }

        npcID = newID ?? "";

        if (!string.IsNullOrWhiteSpace(npcID))
        {
            registry[npcID] = this;
        }
    }
}

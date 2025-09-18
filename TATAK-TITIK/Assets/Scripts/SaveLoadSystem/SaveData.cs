using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class NPCIdPair
{
    public string gameObjectName;
    public string npcID;

    public NPCIdPair() { } // parameterless for serializer
    public NPCIdPair(string goName, string id)
    {
        gameObjectName = goName;
        npcID = id;
    }
}

[System.Serializable]
public class NPCDialoguePair
{
    public string npcID;
    public string[] dialogueLines;

    public NPCDialoguePair() { }

    public NPCDialoguePair(string id, string[] lines)
    {
        npcID = id;
        dialogueLines = lines ?? new string[0];
    }
}

[System.Serializable]
public class NPCDestinationPair
{
    public string gameObjectName;

    // saved current world position
    public float[] position;      // length 3: x,y,z
    public bool hasPosition = false;

    // saved navigation destination (where it was headed)
    public float[] destination;   // length 3: x,y,z
    public bool hasDestination = false;

    // whether agent was stopped (optional, to restore pause state)
    public bool wasAgentStopped = false;

    public NPCDestinationPair() { }

    public NPCDestinationPair(string goName, Vector3 pos, bool hasPos, Vector3 dest, bool hasDest, bool stopped)
    {
        gameObjectName = goName;
        if (hasPos)
            position = new float[] { pos.x, pos.y, pos.z };
        hasPosition = hasPos;

        if (hasDest)
            destination = new float[] { dest.x, dest.y, dest.z };
        hasDestination = hasDest;

        wasAgentStopped = stopped;
    }
}

[System.Serializable]
public class SaveData
{
    public string savedSceneName;
    public float[] playerPosition;
    public List<JournalEntry> journalEntries;
    public List<InventoryItemData> inventoryItems;
    public string equippedItem;

    public List<string> collectedPickupIDs = new List<string>();
    public List<string> interactedObjectIDs = new List<string>();

    // NEW: persisted triggered dialogue IDs
    public List<string> triggeredDialogueIDs = new List<string>();

    public bool journalAvailable = false;
    public List<NPCIdPair> npcIdOverrides = new List<NPCIdPair>();
    public List<NPCDialoguePair> npcDialogueOverrides = new List<NPCDialoguePair>();
    public List<NPCDestinationPair> npcDestinations = new List<NPCDestinationPair>();


    // NEW: persisted time-of-day (in hours 0..24). -1 means not set / not saved.
    public float timeOfDayHours = -1f;

    public SaveData(Vector3 position,
                    List<JournalEntry> journal,
                    List<InventoryItemData> inventory,
                    string equipped,
                    string sceneName,
                    bool journalAvailableFlag,
                    List<string> triggeredIDs = null)
    {
        playerPosition = new float[] { position.x, position.y, position.z };
        journalEntries = journal ?? new List<JournalEntry>();
        inventoryItems = inventory ?? new List<InventoryItemData>();
        equippedItem = equipped;
        savedSceneName = sceneName;
        journalAvailable = journalAvailableFlag;
        triggeredDialogueIDs = triggeredIDs ?? new List<string>();
        npcIdOverrides = new List<NPCIdPair>();
        // timeOfDayHours remains default (-1) unless caller populates it.
    }

    public Vector3 GetPosition() => new Vector3(playerPosition[0], playerPosition[1], playerPosition[2]);
}

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
    }

    public Vector3 GetPosition() => new Vector3(playerPosition[0], playerPosition[1], playerPosition[2]);
}

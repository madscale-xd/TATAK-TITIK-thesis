using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public float[] playerPosition;
    public List<JournalEntry> journalEntries;
    public List<InventoryItemData> inventoryItems;
    public string equippedItem;

    public List<string> collectedPickupIDs = new List<string>();
    public List<string> interactedObjectIDs = new List<string>(); // ✅ Moved here

    public SaveData(Vector3 position, List<JournalEntry> journal, List<InventoryItemData> inventory, string equipped)
    {
        playerPosition = new float[] { position.x, position.y, position.z };
        journalEntries = journal;
        inventoryItems = inventory;
        equippedItem = equipped;
    }

    public Vector3 GetPosition() => new Vector3(playerPosition[0], playerPosition[1], playerPosition[2]);
}
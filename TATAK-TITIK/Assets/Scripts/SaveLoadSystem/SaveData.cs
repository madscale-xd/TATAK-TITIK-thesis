using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public float[] playerPosition; // x, y, z
    public List<JournalEntry> journalEntries;

    public SaveData(Vector3 position, List<JournalEntry> entries)
    {
        playerPosition = new float[] { position.x, position.y, position.z };
        journalEntries = entries;
    }

    public Vector3 GetPosition() => new Vector3(playerPosition[0], playerPosition[1], playerPosition[2]);
}

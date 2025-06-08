using UnityEngine;

[System.Serializable]
public class SaveData
{
    public float[] playerPosition; // x, y, z

    public SaveData(Vector3 position)
    {
        playerPosition = new float[] { position.x, position.y, position.z };
    }

    public Vector3 GetPosition() => new Vector3(playerPosition[0], playerPosition[1], playerPosition[2]);
}

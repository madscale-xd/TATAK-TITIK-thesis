using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private static string GetPath(int slot) => Application.persistentDataPath + $"/saveSlot{slot}.json";

    public static void Save(SaveData data, int slot)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetPath(slot), json);
    }

    public static SaveData Load(int slot)
    {
        string path = GetPath(slot);
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<SaveData>(json);
        }
        return null;
    }
    public static void Delete(int slot)
    {
        string path = GetPath(slot);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"[SaveSystem] Deleted save file at slot {slot}");
        }
        else
        {
            Debug.LogWarning($"[SaveSystem] No save file to delete at slot {slot}");
        }
    }

    public static bool SaveExists(int slot) => File.Exists(GetPath(slot));
}

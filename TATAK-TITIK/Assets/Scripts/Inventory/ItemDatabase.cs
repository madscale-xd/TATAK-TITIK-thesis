using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewItemDatabase", menuName = "Inventory/ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemDefinition> items;

    [System.Serializable]
    public class ItemDefinition
    {
        public string itemName;
        public Sprite icon; // can be null if unused
        public string description; // optional
        // You can add more metadata here too
    }

    public ItemDefinition GetItemByName(string name)
    {
        return items.Find(i => i.itemName == name);
    }

    public Sprite GetIcon(string itemName)
    {
        var item = items.Find(i => i.itemName == itemName);
        if (item != null)
            return item.icon;
        else
            return null;
    }
}
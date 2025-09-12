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
        public Sprite equippedIcon;  // new: sprite to show when this item is equipped
        public string description; // optional
                                   // You can add more metadata here too

    }

    public ItemDefinition GetItemByName(string name)
    {
        return items.Find(i => i.itemName == name);
    }

    // In ItemDatabase.cs
    public Sprite GetIcon(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return null;
        string key = itemName.Trim().ToLowerInvariant();
        var item = items.Find(i => i.itemName != null && i.itemName.Trim().ToLowerInvariant() == key);
        return item != null ? item.icon : null;
    }
    public Sprite GetEquippedIcon(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return null;
        string key = itemName.Trim().ToLowerInvariant();
        var item = items.Find(i => i.itemName != null && i.itemName.Trim().ToLowerInvariant() == key);
        return item != null ? item.equippedIcon : null;
    }
    }
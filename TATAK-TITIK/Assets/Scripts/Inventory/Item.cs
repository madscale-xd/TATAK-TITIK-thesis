// InventoryItem.cs
using UnityEngine;

[System.Serializable]
public class InventoryItem
{
    public string itemName;
    public Sprite icon;         // unequipped inventory sprite
    public Sprite equippedIcon; // optional sprite when equipped
    public int quantity;

    public InventoryItem(string name, int qty = 1, Sprite iconSprite = null, Sprite equipped = null)
    {
        itemName = name;
        quantity = qty;
        icon = iconSprite;
        equippedIcon = equipped;
    }
}

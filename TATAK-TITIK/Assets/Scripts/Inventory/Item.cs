using UnityEngine;

[System.Serializable]
public class InventoryItem
{
    public string itemName;
    public Sprite icon; // optional if you want visuals
    public int quantity;

    public InventoryItem(string name, int qty = 1)
    {
        itemName = name;
        quantity = qty;
    }
}

[System.Serializable]
public class InventoryItemData
{
    public string itemName;
    public int quantity;

    public InventoryItemData(string name, int qty)
    {
        itemName = name;
        quantity = qty;
    }
}

using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance; // Singleton so you can easily call from anywhere
    public InventoryUI inventoryUI; // assign in inspector

    public List<InventoryItem> items = new List<InventoryItem>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            InventoryManager.Instance.PrintInventory();
        }
    }

    public void AddItem(string itemName, int amount = 1)
    {
        InventoryItem item = items.Find(i => i.itemName == itemName);

        if (item != null)
        {
            item.quantity += amount;
        }
        else
        {
            items.Add(new InventoryItem(itemName, amount));
        }

        Debug.Log($"Picked up {amount}x {itemName}");
        
        if (inventoryUI != null)
            inventoryUI.UpdateInventoryUI();
    }

    public void PrintInventory()
    {
        Debug.Log("Inventory Contents:");
        foreach (var item in items)
        {
            Debug.Log($"{item.itemName} x{item.quantity}");
        }
    }
}

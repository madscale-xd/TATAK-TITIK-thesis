using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance; // Singleton so you can easily call from anywhere
    public InventoryUI inventoryUI; // assign in inspector

    public List<InventoryItem> items = new List<InventoryItem>();

    public string equippedItem = "";

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
            PrintInventory();
        }

        // Equip item from 1–9 (slot 0–8)
        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                EquipItemBySlot(i);
            }
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
            if (items.Count >= 9)
            {
                Debug.LogWarning("Inventory is full (max 9 slots)!");
                return;
            }

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

    public void EquipItemBySlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < items.Count)
        {
            InventoryItem item = items[slotIndex];
            if (item.quantity > 0)
            {
                equippedItem = item.itemName;
                Debug.Log($"Equipped: {equippedItem} (from slot {slotIndex + 1})");
                FloatingNotifier.Instance.ShowMessage($"You equipped {equippedItem}(from slot {slotIndex + 1})", Color.white);
            }
            else
            {
                Debug.Log($"Slot {slotIndex + 1} is empty.");
            }
        }
        else
        {
            Debug.Log($"No item in slot {slotIndex + 1}.");
        }
    }
}

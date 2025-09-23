using System.Collections.Generic;
using UnityEngine;
using System;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance; // Singleton so you can easily call from anywhere
    public InventoryUI inventoryUI; // assign in inspector

    public List<InventoryItem> items = new List<InventoryItem>();

    public string equippedItem = "";
    public int equippedSlot = -1;

    public Sprite blankIcon; // assign a blank black sprite in inspector

    // NEW: assign your ItemDatabase in the inspector (optional; LoadInventory can also provide it)
    public ItemDatabase itemDatabase;

    void Start()
    {
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Prefer FindObjectOfType to be robust across different scene setups
            inventoryUI = FindObjectOfType<InventoryUI>();
            if (inventoryUI == null)
                Debug.LogWarning("InventoryUI component not found in scene on Awake.");
        }
        else
        {
            Destroy(gameObject);
        }
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


    // Modified AddItem: when creating a new slot, fetch icon from itemDatabase (if assigned).
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

            // Lookup both sprites centrally
            Sprite icon = null;
            Sprite equippedIcon = null;
            if (itemDatabase != null)
            {
                icon = itemDatabase.GetIcon(itemName);
                equippedIcon = itemDatabase.GetEquippedIcon(itemName);
            }

            if (icon == null && blankIcon != null) icon = blankIcon;
            if (equippedIcon == null) equippedIcon = null; // OK to be null

            items.Add(new InventoryItem(itemName, amount, icon, equippedIcon));
        }

        Debug.Log($"Picked up {amount}x {itemName}");
        if (inventoryUI == null) inventoryUI = FindObjectOfType<InventoryUI>();
        inventoryUI?.UpdateInventoryUI();
    }

    public void ResetInventory()
    {
        inventoryUI = FindObjectOfType<InventoryUI>();
        items.Clear();
        equippedItem = "";

        // Ensure inventoryUI reference is current
        if (inventoryUI == null) inventoryUI = FindObjectOfType<InventoryUI>();

        items.Clear();
        equippedItem = "";

        if (inventoryUI != null)
        {
            inventoryUI.UpdateInventoryUI();
        }
        else
        {
            Debug.LogWarning("Inventory UI reference is null in ResetInventory!");
        }
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
                equippedSlot = slotIndex;                    // authoritative
                equippedItem = item.itemName;               // compatibility
                Debug.Log($"Equipped: {equippedItem} (from slot {slotIndex + 1})");

                // update UI so equipped sprite shows
                FindObjectOfType<InventoryUI>()?.UpdateInventoryUI();

                FloatingNotifier.Instance.ShowMessage($"You equipped {equippedItem} (from slot {slotIndex + 1})", Color.white);
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

    // LoadInventory still accepts a database param; if provided, we set the manager's itemDatabase too.
    public void LoadInventory(List<InventoryItemData> dataList, string equipped, ItemDatabase database)
    {
        items.Clear();

        foreach (var data in dataList)
        {
            Sprite icon = null;
            Sprite equippedIcon = null;
            if (database != null)
            {
                icon = database.GetIcon(data.itemName);
                equippedIcon = database.GetEquippedIcon(data.itemName);
            }

            if (icon == null && blankIcon != null) icon = blankIcon;

            InventoryItem item = new InventoryItem(data.itemName, data.quantity, icon, equippedIcon);
            items.Add(item);
        }

        if (database != null) itemDatabase = database;

        equippedItem = equipped;

        // Try to find the slot index that matches equipped string (backwards compat)
        equippedSlot = -1;
        if (!string.IsNullOrEmpty(equippedItem))
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].itemName == equippedItem)
                {
                    equippedSlot = i;
                    break;
                }
        }

        if (inventoryUI == null) inventoryUI = FindObjectOfType<InventoryUI>();
        inventoryUI?.UpdateInventoryUI();
    }

    /// <summary>
    /// Returns the currently equipped item's name and quantity via out parameters.
    /// itemName will be empty and quantity 0 if nothing is equipped or item not found.
    /// Usage:
    ///    string name; int qty;
    ///    InventoryManager.Instance.GetEquippedItemInfo(out name, out qty);
    /// </summary>
    public void GetEquippedItemInfo(out string itemName, out int quantity)
    {
        // default values
        itemName = string.Empty;
        quantity = 0;

        if (string.IsNullOrEmpty(equippedItem))
            return;

        itemName = equippedItem;

        // find matching InventoryItem in the list (if present)
        var item = items.Find(i => string.Equals(i.itemName, equippedItem, System.StringComparison.Ordinal));
        if (item != null)
            quantity = item.quantity;
    }

    /// <summary>
    /// Check whether the inventory contains at least `requiredQty` of `checkName` (case-insensitive).
    /// Returns true when requiredQty <= 0 or total found >= requiredQty.
    /// </summary>
    public bool CheckWholeInventory(string checkName, int requiredQty)
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("[BaybayinManager] CheckWholeInventory: InventoryManager.Instance is null.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(checkName))
        {
            Debug.LogWarning("[BaybayinManager] CheckWholeInventory: checkName is null/empty.");
            return false;
        }

        if (requiredQty <= 0) return true; // trivially satisfied

        int total = 0;
        foreach (var it in InventoryManager.Instance.items)
        {
            if (it == null || string.IsNullOrWhiteSpace(it.itemName)) continue;
            if (string.Equals(it.itemName, checkName, StringComparison.OrdinalIgnoreCase))
                total += it.quantity;
            if (total >= requiredQty) return true; // early out
        }

        return total >= requiredQty;
    }
}

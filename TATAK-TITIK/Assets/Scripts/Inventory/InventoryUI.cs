using UnityEngine;
using UnityEngine.UI;
using TMPro; // if using TextMeshPro
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    public GameObject slotPrefab; // assign InventorySlot prefab
    public Transform slotParent;  // assign InventoryPanel
    private List<GameObject> spawnedSlots = new List<GameObject>();

    void Start()
    {
        UpdateInventoryUI(); // Call this on start or whenever items are added
    }

    public void UpdateInventoryUI()
    {
        // Clear existing slots
        foreach (var slot in spawnedSlots)
            Destroy(slot);
        spawnedSlots.Clear();

        // Repopulate with current items
        foreach (InventoryItem item in InventoryManager.Instance.items)
        {
            GameObject newSlot = Instantiate(slotPrefab, slotParent);
            InventorySlotUI slotUI = newSlot.GetComponent<InventorySlotUI>();
            slotUI.SetSlot(item.icon, item.quantity);
            spawnedSlots.Add(newSlot);
        }
    }
}

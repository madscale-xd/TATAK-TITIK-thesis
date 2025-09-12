using UnityEngine;
using UnityEngine.UI;
using TMPro; // if using TextMeshPro
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    public GameObject slotPrefab;
    public Transform slotParent;
    private List<GameObject> spawnedSlots = new List<GameObject>();

    public void UpdateInventoryUI()
    {
        foreach (var slot in spawnedSlots) Destroy(slot);
        spawnedSlots.Clear();

        if (InventoryManager.Instance == null) return;

        for (int i = 0; i < InventoryManager.Instance.items.Count; i++)
        {
            InventoryItem item = InventoryManager.Instance.items[i];
            GameObject newSlot = Instantiate(slotPrefab, slotParent);
            InventorySlotUI slotUI = newSlot.GetComponent<InventorySlotUI>();
            if (slotUI == null) continue;

            bool isEquipped = (InventoryManager.Instance.equippedSlot == i);
            // pass both sprites; slotUI will pick the right one
            slotUI.SetSlot(item.icon, item.equippedIcon, isEquipped, item.quantity);
            spawnedSlots.Add(newSlot);
        }
    }
}

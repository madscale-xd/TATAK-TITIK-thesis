using UnityEngine;
using System.Collections.Generic;

public class ItemPickup : MonoBehaviour
{
    public string itemName = "Mushroom";
    public int amount = 1;
    public string uniqueID; // Unique identifier for this pickup

    private bool playerInRange = false;
    private ItemPromptManager promptManager;
    private PickupMessageUI pickupMessage;

    private void Start()
    {
        promptManager = FindObjectOfType<ItemPromptManager>();
        pickupMessage = FindObjectOfType<PickupMessageUI>();

        if (promptManager == null)
            Debug.LogError("ItemPromptManager not found in scene!");
        if (pickupMessage == null)
            Debug.LogError("PickupMessageUI not found in scene!");

        // Disable pickup if already collected
        if (SaveLoadManager.Instance != null &&
            SaveLoadManager.Instance.IsPickupCollected(uniqueID))
        {
            gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            InventoryManager.Instance.AddItem(itemName, amount);
            promptManager.HidePrompt();
            pickupMessage.ShowMessage($"Picked up {itemName}");

            // Register this pickup as collected
            SaveLoadManager.Instance.MarkPickupCollected(uniqueID);

            gameObject.SetActive(false);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            promptManager.ShowPrompt("Press E to pick up " + itemName);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            promptManager.HidePrompt();
        }
    }
}

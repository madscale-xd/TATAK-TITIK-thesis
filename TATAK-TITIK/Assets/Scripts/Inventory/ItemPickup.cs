using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public string itemName = "Mushroom";
    public int amount = 1;

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
    }

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            InventoryManager.Instance.AddItem(itemName, amount);
            promptManager.HidePrompt();

            // Show fading pickup message
            pickupMessage.ShowMessage($"Picked up {itemName}");

            Destroy(gameObject);
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

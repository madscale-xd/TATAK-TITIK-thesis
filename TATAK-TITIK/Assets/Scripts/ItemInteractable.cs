using UnityEngine;

public class ItemInteractable : MonoBehaviour
{
    [Header("Interaction Settings")]
    public string requiredItem; // Name of the item required to interact
    public string interactionPrompt = "Press E to interact"; // UI hint when nearby

    [Header("Visual Feedback (Optional)")]
    public Color targetColor = Color.red;
    public float colorChangeSpeed = 2f;

    private bool hasInteracted = false;
    private Renderer[] renderers;
    private Color originalColor;
    private float transitionProgress = 0f;
    [Header("Save ID (unique per scene)")]
    [SerializeField] private string customInteractableID = "";
    private string interactableID;

    void Start()
    {
        // Generate a unique ID for saving interaction state
        interactableID = gameObject.scene.name + "_" + transform.position.ToString();
        

        // Check if already interacted with
        if (SaveLoadManager.Instance.IsObjectInteracted(interactableID))
        {
            hasInteracted = true;

            // Instantly change visuals or remove object if it was previously interacted
            if (gameObject.name.Contains("Bush")) Destroy(gameObject); // <- Burned bush never reappears
            // Add similar checks for other object types if needed
        }

        renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            originalColor = renderers[0].material.color;
        }

        if (hasInteracted)
        {
            transitionProgress = 1f; // Instantly apply final color if already interacted
            foreach (Renderer r in renderers)
            {
                if (r != null)
                {
                    r.material.color = Color.Lerp(originalColor, targetColor, 1f);
                }
            }
        }
    }

    public void TryInteract()
    {
        if (hasInteracted) return;

        // Check if player has and equipped the correct item BEFORE marking anything as interacted.
        if (InventoryManager.Instance != null && InventoryManager.Instance.equippedItem == requiredItem)
        {
            InventoryItem equipped = InventoryManager.Instance.items.Find(i => i.itemName == requiredItem && i.quantity > 0);
            if (equipped != null)
            {
                // consume one
                equipped.quantity--;
                if (equipped.quantity <= 0)
                    InventoryManager.Instance.items.Remove(equipped);

                InventoryManager.Instance.inventoryUI?.UpdateInventoryUI();

                // perform the interaction
                PerformInteraction();

                // ✅ Mark object as interacted (persist this only on success)
                SaveLoadManager.Instance.MarkObjectInteracted(interactableID);

                // persist pickup if you also treat it as a pickup
                SaveLoadManager.Instance.MarkPickupCollected(interactableID);

                return;
            }
        }

        // If we get here, player doesn't have the required item
        FloatingNotifier.Instance.ShowMessage($"You need a {requiredItem} to interact with {gameObject.name}.", Color.red);
    }

    void PerformInteraction()
    {
        hasInteracted = true;

        // Customize interaction logic based on name + item
        if (gameObject.name.Contains("Bush") && requiredItem == "Torch")
        {
            FloatingNotifier.Instance.ShowMessage("You burned the bush!", Color.white);
        }
        else if (gameObject.name.Contains("Door") && requiredItem == "Key")
        {
            FloatingNotifier.Instance.ShowMessage("You unlocked the door!", Color.yellow);
            // Example: gameObject.SetActive(false); to open the door
        }
        else if (gameObject.name.Contains("Rock") && requiredItem == "Pickaxe")
        {
            FloatingNotifier.Instance.ShowMessage("You mined the rock!", Color.gray);
            // Optional: spawn loot, disable rock, etc.
        }
        else
        {
            FloatingNotifier.Instance.ShowMessage($"You used {requiredItem} on {gameObject.name}.", Color.cyan);
        }

        // Start visual feedback (like color change)
        transitionProgress = 0f;
    }

    void Update()
    {
        if (hasInteracted && renderers != null)
        {
            transitionProgress += Time.deltaTime * colorChangeSpeed;
            foreach (Renderer r in renderers)
            {
                if (r != null)
                {
                    Color lerpedColor = Color.Lerp(originalColor, targetColor, Mathf.Clamp01(transitionProgress));
                    r.material.color = lerpedColor;
                }
            }
        }
    }
}

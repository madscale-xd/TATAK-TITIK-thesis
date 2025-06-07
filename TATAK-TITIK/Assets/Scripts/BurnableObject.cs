using UnityEngine;

public class BurnableObject : MonoBehaviour
{
    public string requiredItem = "Torch"; // Name of required item
    public Color burnedColor = Color.red;
    public float colorChangeSpeed = 2f;

    private bool isBurning = false;
    private Renderer objectRenderer;
    private Color originalColor;
    private float burnProgress = 2f;

    void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
            originalColor = objectRenderer.material.color;
    }

    public void TryBurn()
    {
        if (isBurning) return;

        // Check if the equipped item is the required item
        if (InventoryManager.Instance.equippedItem == requiredItem)
        {
            // Consume one instance of the equipped item
            InventoryItem equipped = InventoryManager.Instance.items.Find(i => i.itemName == requiredItem && i.quantity > 0);

            if (equipped != null)
            {
                equipped.quantity--;
                if (equipped.quantity <= 0)
                    InventoryManager.Instance.items.Remove(equipped);

                InventoryManager.Instance.inventoryUI?.UpdateInventoryUI();

                // Start burning
                isBurning = true;
                Debug.Log($"{gameObject.name} is burning!");
            }
        }
        else
        {
            Debug.Log($"You need to equip a {requiredItem} to burn this.");
        }
    }

    void Update()
    {
        if (isBurning && objectRenderer != null)
        {
            burnProgress += Time.deltaTime * colorChangeSpeed;
            objectRenderer.material.color = Color.Lerp(originalColor, burnedColor, burnProgress);
        }
    }
}

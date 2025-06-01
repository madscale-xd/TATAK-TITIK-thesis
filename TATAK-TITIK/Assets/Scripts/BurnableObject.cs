using UnityEngine;

public class BurnableObject : MonoBehaviour
{
    public string requiredItem = "Torch"; // Name of required item
    public Color burnedColor = Color.red;
    public float colorChangeSpeed = 2f;

    private bool isBurning = false;
    private Renderer objectRenderer;
    private Color originalColor;
    private float burnProgress = 0f;

    void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
            originalColor = objectRenderer.material.color;
    }

    public void TryBurn()
    {
        if (isBurning) return;

        // Check if player has the Torch
        InventoryItem torch = InventoryManager.Instance.items.Find(i => i.itemName == requiredItem && i.quantity > 0);

        if (torch != null)
        {
            // Consume one Torch
            torch.quantity--;
            if (torch.quantity <= 0)
                InventoryManager.Instance.items.Remove(torch);

            InventoryManager.Instance.inventoryUI?.UpdateInventoryUI();

            // Start burning
            isBurning = true;
            Debug.Log($"{gameObject.name} is burning!");
        }
        else
        {
            Debug.Log("You need a Torch to burn this.");
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

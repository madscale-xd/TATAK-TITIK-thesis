using UnityEngine;

public class BurnableObject : MonoBehaviour
{
    public string requiredItem = "Torch"; // Name of required item
    public Color burnedColor = Color.red;
    public float colorChangeSpeed = 2f;

    private bool isBurning = false;
    private Renderer[] renderers;
    private Color originalColor;
    private float burnProgress = 0f;

    void Start()
    {
        // Get all renderers in this object and its children
        renderers = GetComponentsInChildren<Renderer>();

        if (renderers.Length > 0)
        {
            // Assume they all start with the same color (use first as base)
            originalColor = renderers[0].material.color;
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} has no Renderer components to burn.");
        }
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
                FloatingNotifier.Instance.ShowMessage($"{gameObject.name} is burning!", Color.white);
            }
        }
        else
        {
            Debug.Log($"You need to equip a {requiredItem} to burn this.");
            FloatingNotifier.Instance.ShowMessage($"You need to equip a {requiredItem} to burn this.", Color.red);
        }
    }

    void Update()
    {
        if (isBurning && renderers != null)
        {
            burnProgress += Time.deltaTime * colorChangeSpeed;
            foreach (Renderer r in renderers)
            {
                if (r != null)
                {
                    Color lerpedColor = Color.Lerp(originalColor, burnedColor, Mathf.Clamp01(burnProgress));
                    r.material.color = lerpedColor;
                }
            }
        }
    }
}

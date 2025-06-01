using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlotUI : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI quantityText;

    public void SetSlot(Sprite icon, int quantity)
    {
        iconImage.sprite = icon;
        quantityText.text = quantity.ToString();
    }
}

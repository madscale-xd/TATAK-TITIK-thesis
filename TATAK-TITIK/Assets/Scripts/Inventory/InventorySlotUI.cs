using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlotUI : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI quantityText;
    public GameObject equippedOutline; // optional: turn on a highlight when equipped

    // Now takes unequippedSprite, equippedSprite, and a bool
    public void SetSlot(Sprite unequippedSprite, Sprite equippedSprite, bool isEquipped, int quantity)
    {
        Sprite toShow = isEquipped ? (equippedSprite != null ? equippedSprite : unequippedSprite) : unequippedSprite;

        if (iconImage != null)
        {
            iconImage.sprite = toShow;
            iconImage.enabled = (toShow != null);
        }

        if (quantityText != null) quantityText.text = quantity.ToString();

        if (equippedOutline != null) equippedOutline.SetActive(isEquipped);
    }
}

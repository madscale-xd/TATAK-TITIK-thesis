using UnityEngine;

public class ItemInteractor : MonoBehaviour
{
    private ItemInteractable currentTarget = null;

    void Update()
    {
        if (currentTarget != null && Input.GetKeyDown(KeyCode.E))
        {
            SceneButtonManager sbm = FindObjectOfType<SceneButtonManager>();
            if (sbm != null && sbm.IsEKeyEnabled())
            {
                currentTarget.TryInteract();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        ItemInteractable interactable = other.GetComponentInParent<ItemInteractable>();
        if (interactable != null)
        {
            currentTarget = interactable;
            FloatingNotifier.Instance.ShowMessage(interactable.interactionPrompt, Color.white);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<ItemInteractable>() == currentTarget)
        {
            currentTarget = null;
        }
    }
}

using UnityEngine;

public class BurnInteractor : MonoBehaviour
{
    private BurnableObject currentTarget = null;

    void Update()
    {
        if (currentTarget != null && Input.GetKeyDown(KeyCode.E))
        {
            currentTarget.TryBurn();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        BurnableObject burnable = other.GetComponentInParent<BurnableObject>();
        if (burnable != null)
        {
            currentTarget = burnable;
            FloatingNotifier.Instance.ShowMessage("Press E to burn the object.", Color.red);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<BurnableObject>() == currentTarget)
        {
            currentTarget = null;
        }
    }
}

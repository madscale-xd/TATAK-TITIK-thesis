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
        BurnableObject burnable = other.GetComponent<BurnableObject>();
        if (burnable != null)
        {
            currentTarget = burnable;
            Debug.Log("Press E to burn the object.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<BurnableObject>() == currentTarget)
        {
            currentTarget = null;
        }
    }
}

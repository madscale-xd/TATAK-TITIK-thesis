using UnityEngine;

public class NPCTracker : MonoBehaviour
{
    [Header("Tracking Settings")]
    public float rotationSpeed = 5f;        // Speed at which the NPC turns toward the player

    private Transform playerTarget = null;  // Reference to the player if detected
    private Quaternion defaultRotation;     // Rotation before tracking
    private bool isTracking = false;

    void Start()
    {
        defaultRotation = transform.rotation;
    }

    void Update()
    {
        if (isTracking && playerTarget != null)
        {
            Vector3 direction = (playerTarget.position - transform.position).normalized;
            direction.y = 0f; // Ignore vertical tilting

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }
        else
        {
            // Smoothly return to original orientation
            transform.rotation = Quaternion.Slerp(transform.rotation, defaultRotation, Time.deltaTime * rotationSpeed);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerTarget = other.transform;
            isTracking = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && other.transform == playerTarget)
        {
            playerTarget = null;
            isTracking = false;
        }
    }
}

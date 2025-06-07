using UnityEngine;
using TMPro;

public class NPCTracker : MonoBehaviour
{
    [Header("Tracking Settings")]
    public float rotationSpeed = 5f;

    [Header("Floating Text Reference")]
    public TextMeshPro floatingText;       // Your 3D world-space TMP text
    public float fadeSpeed = 2f;

    private Transform playerTarget = null;
    private Quaternion defaultRotation;
    private bool isTracking = false;

    private Camera mainCam;
    private Color originalColor;
    private float currentAlpha = 0f;

    void Start()
    {
        defaultRotation = transform.rotation;
        mainCam = Camera.main;

        if (floatingText != null)
        {
            originalColor = floatingText.color;
            SetTextAlpha(0f); // Start invisible
        }
    }

    void Update()
    {
        // Rotate NPC
        if (isTracking && playerTarget != null)
        {
            Vector3 direction = (playerTarget.position - transform.position).normalized;
            direction.y = 0f;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }
        else
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, defaultRotation, Time.deltaTime * rotationSpeed);
        }

        // Handle floating text
        if (floatingText != null)
        {
            // Face the camera
            floatingText.transform.rotation = Quaternion.LookRotation(floatingText.transform.position - mainCam.transform.position);

            // Fade in/out manually by alpha
            float targetAlpha = isTracking ? 1f : 0f;
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
            SetTextAlpha(currentAlpha);
        }
    }

    void SetTextAlpha(float alpha)
    {
        if (floatingText != null)
        {
            Color newColor = originalColor;
            newColor.a = alpha;
            floatingText.color = newColor;
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

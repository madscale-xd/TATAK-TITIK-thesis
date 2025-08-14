using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float smoothTime = 0.1f;
    public float gravity = -9.81f;
    public float bounceAmplitude = 0.1f;
    public float bounceFrequency = 5f;

    [SerializeField] private Transform playerVisual; // Drag your model here

    private CharacterController controller;
    private Vector3 velocity = Vector3.zero;
    private Vector3 currentVelocity = Vector3.zero;
    private float verticalVelocity = 0f;
    private bool isGrounded;

    private Quaternion inputRotation = Quaternion.Euler(0, 45, 0);
    private float bounceTimer = 0f;
    private float originalY;

    // NEW: cached reference to DialogueManager to detect when dialogue panel is open
    private DialogueManager dialogueManager;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (playerVisual != null)
            originalY = playerVisual.localPosition.y;

        // Cache DialogueManager (optional inspector assignment isn't used here)
        dialogueManager = FindObjectOfType<DialogueManager>();

        Debug.Log("PlayerMovement START at " + transform.position);
    }

    void Update()
    {
        // If DialogueManager exists and the dialogue panel is visible, block horizontal input.
        bool dialogueBlockingMovement = false;
        if (dialogueManager != null && dialogueManager.dialoguePanelGroup != null)
        {
            // dialoguePanelGroup.alpha > 0 means the dialogue UI is shown (including during fade).
            dialogueBlockingMovement = dialogueManager.dialoguePanelGroup.alpha > 0f;
        }

        // Ground check
        isGrounded = controller.isGrounded;

        if (isGrounded && verticalVelocity < 0)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        Vector3 horizontalVelocity;

        if (!dialogueBlockingMovement)
        {
            // Get input
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;
            Vector3 rotatedDirection = inputRotation * inputDirection;

            if (rotatedDirection.magnitude >= 0.1f)
            {
                horizontalVelocity = Vector3.SmoothDamp(velocity, rotatedDirection * moveSpeed, ref currentVelocity, smoothTime);

                // Rotation
                Vector3 lookDirection = new Vector3(rotatedDirection.x, 0f, rotatedDirection.z);
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection) * Quaternion.Euler(0, -90f, 0);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);

                // Bounce while walking
                bounceTimer += Time.deltaTime * bounceFrequency;
                if (playerVisual != null)
                {
                    float bounceOffset = Mathf.Sin(bounceTimer * Mathf.PI * 2f) * bounceAmplitude;
                    Vector3 visualPos = playerVisual.localPosition;
                    visualPos.y = originalY + bounceOffset;
                    playerVisual.localPosition = visualPos;
                }
            }
            else
            {
                horizontalVelocity = Vector3.SmoothDamp(velocity, Vector3.zero, ref currentVelocity, smoothTime);
                bounceTimer = 0f;

                if (playerVisual != null)
                {
                    Vector3 visualPos = playerVisual.localPosition;
                    visualPos.y = originalY;
                    playerVisual.localPosition = visualPos;
                }
            }
        }
        else
        {
            // Dialogue open: prevent horizontal movement and reset walk visuals
            horizontalVelocity = Vector3.SmoothDamp(velocity, Vector3.zero, ref currentVelocity, smoothTime);
            bounceTimer = 0f;
            if (playerVisual != null)
            {
                Vector3 visualPos = playerVisual.localPosition;
                visualPos.y = originalY;
                playerVisual.localPosition = visualPos;
            }
        }

        // Apply move (keep gravity)
        velocity = new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.z);
        controller.Move(velocity * Time.deltaTime);
    }
}

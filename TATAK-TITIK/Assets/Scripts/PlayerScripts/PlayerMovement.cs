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

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (playerVisual != null)
            originalY = playerVisual.localPosition.y;
    }

    void Update()
    {
        // Ground check
        isGrounded = controller.isGrounded;

        if (isGrounded && verticalVelocity < 0)
            verticalVelocity = -2f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        // Get input
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;
        Vector3 rotatedDirection = inputRotation * inputDirection;

        Vector3 horizontalVelocity;

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

        // Apply move
        velocity = new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.z);
        controller.Move(velocity * Time.deltaTime);
    }
}

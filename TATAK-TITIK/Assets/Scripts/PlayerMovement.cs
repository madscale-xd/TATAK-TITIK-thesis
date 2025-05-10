using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float smoothTime = 0.1f;
    public float gravity = -9.81f;
    public float groundCheckDistance = 0.1f;
    public LayerMask groundMask;

    private CharacterController controller;
    private Vector3 velocity = Vector3.zero;
    private Vector3 currentVelocity = Vector3.zero;
    private float verticalVelocity = 0f;
    private bool isGrounded;

    // 45-degree rotation (Y axis)
    private Quaternion inputRotation = Quaternion.Euler(0, 45, 0);

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Ground check
        isGrounded = controller.isGrounded;

        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f; // small downward force to keep contact
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;
        Vector3 rotatedDirection = inputRotation * inputDirection;

        Vector3 horizontalVelocity;

        if (rotatedDirection.magnitude >= 0.1f)
        {
            horizontalVelocity = Vector3.SmoothDamp(velocity, rotatedDirection * moveSpeed, ref currentVelocity, smoothTime);
        }
        else
        {
            horizontalVelocity = Vector3.SmoothDamp(velocity, Vector3.zero, ref currentVelocity, smoothTime);
        }

        velocity = new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.z);

        controller.Move(velocity * Time.deltaTime);
    }
}
using UnityEngine;

public class IsometricCameraFollow : MonoBehaviour
{
    public Transform target; // The GameObject to follow
    public Vector3 offset = new Vector3(-6.5f, 10, -6.5f); // Adjust for isometric angle
    public float smoothSpeed = 5f;

    [Range(0f, 90f)]
    public float pitchAngle = 50f;     // X rotation (looking downward)
    [Range(0f, 360f)]
    public float yawAngle = 45f;       // Y rotation (rotates around the character)

    private void Start()
    {
        SetCameraRotation();
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        }
    }

    private void OnValidate()
    {
        SetCameraRotation();
    }

    private void SetCameraRotation()
    {
        transform.rotation = Quaternion.Euler(pitchAngle, yawAngle, 0f);
    }
}

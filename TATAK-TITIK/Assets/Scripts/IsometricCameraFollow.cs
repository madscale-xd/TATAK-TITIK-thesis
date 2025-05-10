using UnityEngine;

public class IsometricCameraFollow : MonoBehaviour
{
    public Transform target; // The GameObject to follow
    public Vector3 offset = new Vector3(0, 10, -10); // Adjust for isometric angle
    public float smoothSpeed = 5f;

    private void Start()
    {
        // Set a classic isometric rotation
        transform.rotation = Quaternion.Euler(30f, 45f, 0f);
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        }
    }
}
    
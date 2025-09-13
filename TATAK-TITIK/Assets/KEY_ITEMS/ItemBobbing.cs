using UnityEngine;

/// <summary>
/// Minimal movement component: spins on local X/Y/Z and bobs vertically.
/// Attach to any object you want animated. To stop it, disable this component:
///   GetComponent<PickupMotion>().enabled = false;
/// </summary>
public class PickupMotion : MonoBehaviour
{
    [Header("Spin (degrees/sec)")]
    public float spinSpeedX = 120f;
    public float spinSpeedY = 90f;
    public float spinSpeedZ = 0f; // NEW: spin around local Z

    [Header("Bob (meters & cycles/sec)")]
    public float bobAmplitude = 0.25f;
    public float bobFrequency = 0.8f;

    // randomize phase so multiples don't sync perfectly
    float bobPhase;
    Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        // Spin locally (now includes Z)
        transform.Rotate(
            spinSpeedX * Time.deltaTime,
            spinSpeedY * Time.deltaTime,
            spinSpeedZ * Time.deltaTime,
            Space.Self
        );

        // Bob (sin wave). bobFrequency is cycles per second.
        float y = startPosition.y + Mathf.Sin((Time.time + bobPhase) * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
        Vector3 p = transform.position;
        p.y = y;
        transform.position = p;
    }
}

using UnityEngine;

[DisallowMultipleComponent]
public class OscillatingRotator : MonoBehaviour
{
    [Header("Axis")]
    [Tooltip("Enable rotation on each axis.")]
    public bool rotateX = true;
    public bool rotateY = false;
    public bool rotateZ = false;

    [Header("Amplitude (degrees)")]
    [Tooltip("Maximum swing in degrees for each axis.")]
    public float amplitudeX = 30f;
    public float amplitudeY = 30f;
    public float amplitudeZ = 30f;

    [Header("Timing")]
    [Tooltip("Speed multiplier of the oscillation.")]
    public float speed = 1f;
    [Tooltip("Start the oscillation at a random phase so multiple objects won't be in sync.")]
    public bool randomStartPhase = false;

    [Header("Apply To")]
    [Tooltip("If true, applies rotation relative to the object's local rotation; otherwise uses world rotation.")]
    public bool useLocalRotation = true;

    // internal state
    Quaternion initialRotation;
    float phase = 0f;

    void Awake()
    {
        // capture the baseline rotation depending on choice
        initialRotation = useLocalRotation ? transform.localRotation : transform.rotation;
        if (randomStartPhase)
            phase = Random.Range(0f, Mathf.PI * 2f);
    }

    void OnValidate()
    {
        // keep sensible, non-negative values in the inspector
        amplitudeX = Mathf.Max(0f, amplitudeX);
        amplitudeY = Mathf.Max(0f, amplitudeY);
        amplitudeZ = Mathf.Max(0f, amplitudeZ);
        speed = Mathf.Max(0f, speed);
    }

    void Update()
    {
        // advance the oscillator
        phase += Time.deltaTime * speed;
        float s = Mathf.Sin(phase); // goes -1 -> +1 -> -1, exactly what you asked for

        float x = rotateX ? amplitudeX * s : 0f;
        float y = rotateY ? amplitudeY * s : 0f;
        float z = rotateZ ? amplitudeZ * s : 0f;

        Quaternion offset = Quaternion.Euler(x, y, z);

        if (useLocalRotation)
            transform.localRotation = initialRotation * offset;
        else
            transform.rotation = initialRotation * offset;
    }

    [ContextMenu("Capture Current Rotation as Initial")]
    void CaptureInitialRotation()
    {
        initialRotation = useLocalRotation ? transform.localRotation : transform.rotation;
    }
}

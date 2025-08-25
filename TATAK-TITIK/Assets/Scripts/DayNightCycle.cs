using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Day/night cycle manager.
/// - Call SetTimeOfDay(hourOrDegrees) to snap the cycle.
/// - Call SetTimeOfDay(hour, minute, duration) to animate the transition over `duration` seconds.
/// - Subscribable UnityEvent<float> OnTimeSet fires after the value has been applied (argument = hour as float 0-24).
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    [Header("Light References")]
    public Light sunLight;
    public Light moonLight;

    [Header("Cycle Settings")]
    public float rotationSpeed = 1f; // degrees per second when autoRotate is enabled
    public bool autoRotate = true;   // set false if you want manual control

    [Header("Light Intensities")]
    public float maxSunIntensity = 1.5f;
    public float maxMoonIntensity = 2f;

    [Header("Ambient & Fog Colors")]
    public Color dayAmbient = new Color(0.85f, 0.85f, 0.9f);
    public Color nightAmbient = new Color(0.15f, 0.25f, 0.45f);
    public Color dayFog = new Color(0.85f, 0.9f, 1f);
    public Color nightFog = new Color(0.15f, 0.25f, 0.45f);

    [Header("Skybox Transition")]
    public Material daySkybox;
    public Material nightSkybox;
    private Material blendedSkybox;

    [Header("Sunlight Color Over Time")]
    public Gradient sunColorGradient;

    [Header("Events")]
    public UnityEvent<float> OnTimeSet; // float = time in hours (0-24)

    // internal
    private Quaternion sunInitialRotation;
    private Quaternion moonInitialRotation;
    private float timeOfDayHours = 12f; // 0-24
    private Coroutine timeLerpCoroutine;

    private void Start()
    {
        if (sunLight != null) sunInitialRotation = sunLight.transform.rotation;
        if (moonLight != null) moonInitialRotation = moonLight.transform.rotation;

        if (daySkybox != null && nightSkybox != null)
        {
            blendedSkybox = new Material(daySkybox.shader);
            RenderSettings.skybox = blendedSkybox;
        }

        // Set default gradient if not assigned
        if (sunColorGradient == null || sunColorGradient.colorKeys.Length == 0)
            InitializeDefaultSunGradient();

        // Initialize timeOfDayHours based on current sun rotation relative to baseline if possible
        if (sunLight != null)
        {
            float relX = Mathf.DeltaAngle(sunInitialRotation.eulerAngles.x, sunLight.transform.eulerAngles.x);
            if (relX < 0) relX += 360f;
            timeOfDayHours = (relX / 360f) * 24f;
        }

        // Apply visuals immediately for the starting rotations
        ApplyLightingImmediate();
    }

    private void Update()
    {
        if (autoRotate && timeLerpCoroutine == null)
        {
            RotateLights(Time.deltaTime);
            ApplyLightingImmediate();
        }
    }

    // -- Auto-rotation (keeps timeOfDayHours in sync)
    private void RotateLights(float deltaTime)
    {
        if (sunLight == null || moonLight == null) return;

        float rotation = rotationSpeed * deltaTime; // degrees to rotate this frame
        sunLight.transform.Rotate(Vector3.right, rotation, Space.Self);
        moonLight.transform.Rotate(Vector3.right, rotation, Space.Self);

        // advance internal hour clock
        timeOfDayHours += (rotation / 360f) * 24f;
        timeOfDayHours = Mathf.Repeat(timeOfDayHours, 24f);
    }

    // -- Set time of day manually via single public methods
    /// <summary>
    /// Sets the day/night pivot. If value <= 24 it is treated as hour (0-24). If value > 24 it is treated as degrees (0-360).
    /// This overload snaps instantly (no animation).
    /// </summary>
    public void SetTimeOfDay(float value)
    {
        SetTimeOfDay(value, 0f);
    }

    /// <summary>
    /// Sets the day/night pivot. If value <= 24 it is treated as hour (0-24). If value > 24 it is treated as degrees (0-360).
    /// If duration &gt; 0, smoothly animates the transition over that many seconds.
    /// </summary>
    public void SetTimeOfDay(float value, float duration)
    {
        if (sunLight == null || moonLight == null) return;

        float angleDeg;
        if (value <= 24f)
        {
            float hour = Mathf.Clamp(value, 0f, 24f);
            angleDeg = (hour / 24f) * 360f;
            timeOfDayHours = hour;
        }
        else
        {
            angleDeg = Mathf.Repeat(value, 360f);
            timeOfDayHours = (angleDeg / 360f) * 24f;
        }

        // Stop any running animation
        if (timeLerpCoroutine != null)
        {
            StopCoroutine(timeLerpCoroutine);
            timeLerpCoroutine = null;
        }

        if (duration <= 0f)
        {
            // Snap
            sunLight.transform.rotation = sunInitialRotation * Quaternion.Euler(angleDeg, 0f, 0f);
            moonLight.transform.rotation = moonInitialRotation * Quaternion.Euler(angleDeg + 180f, 0f, 0f);

            ApplyLightingImmediate();

            FireOnTimeSet();
        }
        else
        {
            timeLerpCoroutine = StartCoroutine(AnimateTimeChangeCoroutine(angleDeg, duration));
        }
    }

    /// <summary>
    /// Overload that accepts HH:MM and snaps (no animation).
    /// </summary>
    public void SetTimeOfDay(int hour, int minute)
    {
        SetTimeOfDay(hour, minute, 0f);
    }

    /// <summary>
    /// Overload that accepts HH:MM and optionally animates over duration seconds.
    /// </summary>
    public void SetTimeOfDay(int hour, int minute, float duration)
    {
        hour = Mathf.Clamp(hour, 0, 23);
        minute = Mathf.Clamp(minute, 0, 59);
        float fractionalHour = hour + (minute / 60f);
        SetTimeOfDay(fractionalHour, duration);
    }

    private IEnumerator AnimateTimeChangeCoroutine(float targetAngleDeg, float duration)
    {
        // Determine start angle in 0..360 relative to initial rotation
        float startAngle = GetCurrentSunAngleDeg();
        float endAngle = targetAngleDeg;

        // Compute shortest delta (signed)
        float delta = Mathf.DeltaAngle(startAngle, endAngle); // -180..180

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            // smoothstep easing for a pleasant transition
            float eased = t * t * (3f - 2f * t);

            float currentAngle = startAngle + delta * eased;

            sunLight.transform.rotation = sunInitialRotation * Quaternion.Euler(currentAngle, 0f, 0f);
            moonLight.transform.rotation = moonInitialRotation * Quaternion.Euler(currentAngle + 180f, 0f, 0f);

            // Update internal hour to match currentAngle
            float normalized = Mathf.Repeat(currentAngle, 360f);
            timeOfDayHours = (normalized / 360f) * 24f;

            // Update visuals each frame
            ApplyLightingImmediate();

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure final snap to target
        sunLight.transform.rotation = sunInitialRotation * Quaternion.Euler(endAngle, 0f, 0f);
        moonLight.transform.rotation = moonInitialRotation * Quaternion.Euler(endAngle + 180f, 0f, 0f);
        timeOfDayHours = (Mathf.Repeat(endAngle, 360f) / 360f) * 24f;
        ApplyLightingImmediate();

        timeLerpCoroutine = null;

        FireOnTimeSet();
    }

    // helper to compute current sun angle (0-360) relative to sunInitialRotation X axis
    private float GetCurrentSunAngleDeg()
    {
        if (sunLight == null) return 0f;
        float rel = Mathf.DeltaAngle(sunInitialRotation.eulerAngles.x, sunLight.transform.eulerAngles.x);
        if (rel < 0f) rel += 360f;
        return rel;
    }

    private void ApplyLightingImmediate()
    {
        if (sunLight == null || moonLight == null) return;

        float sunDot = Mathf.Clamp01(Vector3.Dot(sunLight.transform.forward, Vector3.down));
        float moonDot = Mathf.Clamp01(Vector3.Dot(moonLight.transform.forward, Vector3.down));

        sunLight.color = sunColorGradient.Evaluate(sunDot);
        sunLight.intensity = sunDot * maxSunIntensity;

        moonLight.color = new Color(0.5f, 0.6f, 1.2f) * moonDot;
        moonLight.intensity = moonDot * maxMoonIntensity;

        UpdateEnvironment(sunDot);
    }

    private void UpdateEnvironment(float sunDot)
    {
        float t = Mathf.Clamp01(sunDot);

        RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, t);
        RenderSettings.fogColor = Color.Lerp(nightFog, dayFog, t);

        if (blendedSkybox != null && daySkybox != null && nightSkybox != null)
            blendedSkybox.Lerp(nightSkybox, daySkybox, t);
    }

    private void FireOnTimeSet()
    {
        if (OnTimeSet != null)
        {
            try
            {
                OnTimeSet.Invoke(timeOfDayHours);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DayNightCycle] Exception invoking OnTimeSet: {ex}");
            }
        }
    }

    /// <summary>
    /// Public getter so external systems (SaveLoadManager, debug UI, etc.) can read the current time in hours (0..24).
    /// </summary>
    public float GetTimeOfDayHours() => timeOfDayHours;

    private void InitializeDefaultSunGradient()
    {
        sunColorGradient = new Gradient();

        sunColorGradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.05f, 0.1f, 0.2f), 0f),   // Midnight blue
                new GradientColorKey(new Color(1f, 0.5f, 0.2f), 0.25f),   // Sunrise orange
                new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0.5f),   // Day yellow
                new GradientColorKey(new Color(1f, 0.5f, 0.2f), 0.75f),   // Sunset orange
                new GradientColorKey(new Color(0.05f, 0.1f, 0.2f), 1f)    // Night blue again
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.25f),
                new GradientAlphaKey(1f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            }
        );
    }
}

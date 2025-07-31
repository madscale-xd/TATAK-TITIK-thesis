using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    [Header("Light References")]
    public Light sunLight;
    public Light moonLight;

    [Header("Cycle Settings")]
    public float rotationSpeed = 1f;

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

    private void Start()
    {
        if (daySkybox != null && nightSkybox != null)
        {
            blendedSkybox = new Material(daySkybox.shader);
            RenderSettings.skybox = blendedSkybox;
        }

        // Set default gradient if not assigned
        if (sunColorGradient == null || sunColorGradient.colorKeys.Length == 0)
            InitializeDefaultSunGradient();
    }

    private void Update()
    {
        RotateLights();

        float sunDot = Mathf.Clamp01(Vector3.Dot(sunLight.transform.forward, Vector3.down));
        float moonDot = Mathf.Clamp01(Vector3.Dot(moonLight.transform.forward, Vector3.down));

        sunLight.color = sunColorGradient.Evaluate(sunDot);
        sunLight.intensity = sunDot * maxSunIntensity;

        moonLight.color = new Color(0.5f, 0.6f, 1.2f) * moonDot;
        moonLight.intensity = moonDot * maxMoonIntensity;

        UpdateEnvironment(sunDot);
    }

    private void RotateLights()
    {
        float rotation = rotationSpeed * Time.deltaTime;
        sunLight.transform.Rotate(Vector3.right, rotation);
        moonLight.transform.Rotate(Vector3.right, rotation);
    }

    private void UpdateEnvironment(float sunDot)
    {
        float t = Mathf.Clamp01(sunDot);

        RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, t);
        RenderSettings.fogColor = Color.Lerp(nightFog, dayFog, t);

        if (blendedSkybox != null && daySkybox != null && nightSkybox != null)
            blendedSkybox.Lerp(nightSkybox, daySkybox, t);
    }

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

using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshPro))]
public class FloatingMessage : MonoBehaviour
{
    private TextMeshPro floatingText;   // Automatically assigned
    public float floatSpeed = 0.5f;
    public float duration = 2f;
    public float fadeDuration = 1f;

    private Camera mainCam;
    private float timer = 0f;

    private float fadeInDuration = 0.25f;
    private Color originalColor;

    void Awake()
    {
        floatingText = GetComponent<TextMeshPro>();
        if (floatingText == null)
        {
            Debug.LogError("FloatingMessage: TextMeshPro component missing!");
            enabled = false;
            return;
        }
    }

    void Start()
    {
        mainCam = Camera.main;
        originalColor = floatingText.color;
        SetAlpha(0f); // Start invisible
    }

    void Update()
    {
        // Face the camera
        floatingText.transform.rotation = Quaternion.LookRotation(floatingText.transform.position - mainCam.transform.position);

        // Float upward
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        timer += Time.deltaTime;

        if (timer < fadeInDuration)
        {
            float alpha = Mathf.Lerp(0f, originalColor.a, timer / fadeInDuration);
            SetAlpha(alpha);
        }
        else if (timer < duration)
        {
            SetAlpha(originalColor.a);
        }
        else if (timer < duration + fadeDuration)
        {
            float alpha = Mathf.Lerp(originalColor.a, 0f, (timer - duration) / fadeDuration);
            SetAlpha(alpha);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetText(string message, Color color)
    {
        floatingText.text = message;
        floatingText.color = color;
        originalColor = color;
    }

    private void SetAlpha(float alpha)
    {
        Color c = floatingText.color;
        c.a = alpha;
        floatingText.color = c;
    }
}

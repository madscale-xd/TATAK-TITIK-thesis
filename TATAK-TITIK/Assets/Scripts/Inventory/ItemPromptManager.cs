using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class ItemPromptManager : MonoBehaviour
{
    [Header("UI References")]
    public CanvasGroup promptGroup;
    public TMP_Text promptText;

    [Header("Settings")]
    public float fadeDuration = 0.2f;

    private Coroutine fadeCoroutine;

    void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Immediately hide the prompt when a new scene is loaded
        HidePrompt();
    }
    
    void Start()
    {
        SetCanvasGroup(promptGroup, 0, false);
    }

    public void ShowPrompt(string message)
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        promptText.text = message;
        fadeCoroutine = StartCoroutine(FadeCanvasGroup(promptGroup, 1, true));
    }

    public void HidePrompt()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeCanvasGroup(promptGroup, 0, false));
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float targetAlpha, bool interactable)
    {
        if (group == null)
            yield break;

        if (targetAlpha > 0f)
            group.gameObject.SetActive(true);

        float startAlpha = group.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            group.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        group.alpha = targetAlpha;
        group.interactable = interactable;
        group.blocksRaycasts = interactable;

        if (!interactable)
            group.gameObject.SetActive(false);
    }

    private void SetCanvasGroup(CanvasGroup group, float alpha, bool interactable)
    {
        if (group != null)
        {
            group.alpha = alpha;
            group.interactable = interactable;
            group.blocksRaycasts = interactable;
            group.gameObject.SetActive(alpha > 0f);
        }
    }
}

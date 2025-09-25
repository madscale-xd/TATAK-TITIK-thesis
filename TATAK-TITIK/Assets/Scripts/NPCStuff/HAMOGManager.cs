using System.Collections;
using UnityEngine;

public class HamogManager : MonoBehaviour
{
    [Tooltip("Assign the CanvasGroup you want this manager to control.")]
    public CanvasGroup targetCanvasGroup;

    [Tooltip("Enable debug logging for fade operations.")]
    public bool debugLogs = false;

    // If true, will toggle interactability and raycast blocking depending on alpha (alpha > 0.01 -> interactable).
    [Tooltip("Automatically set interactable/blocksRaycasts when alpha > 0.01.")]
    public bool autoToggleInteractable = true;

    Coroutine runningFade = null;

    /// <summary>
    /// Fade the assigned CanvasGroup to a target alpha.
    /// alphaPercent: 0..100 (percentage). durationSeconds: seconds (int).
    /// </summary>
    public void FadeCanvasToAlpha(int alphaPercent, int durationSeconds)
    {
        if (targetCanvasGroup == null)
        {
            Debug.LogWarning("[HamogManager] targetCanvasGroup is not assigned.");
            return;
        }

        float targetAlpha = Mathf.Clamp01(alphaPercent / 100f);
        float duration = Mathf.Max(0f, (float)durationSeconds);

        if (debugLogs) Debug.Log($"[HamogManager] Fade requested -> targetAlpha={targetAlpha:F2}, duration={duration:F2}s");

        // Cancel any existing fade
        if (runningFade != null)
        {
            StopCoroutine(runningFade);
            runningFade = null;
        }

        if (duration <= 0f)
        {
            // instant
            targetCanvasGroup.alpha = targetAlpha;
            ApplyInteractableIfNeeded(targetAlpha);
            if (debugLogs) Debug.Log("[HamogManager] Applied alpha instantly.");
        }
        else
        {
            runningFade = StartCoroutine(FadeRoutine(targetAlpha, duration));
        }
    }

    IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        float startAlpha = targetCanvasGroup.alpha;
        float elapsed = 0f;

        // if start==target we can early out
        if (Mathf.Approximately(startAlpha, targetAlpha))
        {
            ApplyInteractableIfNeeded(targetAlpha);
            runningFade = null;
            yield break;
        }

        // Step this over time (linear)
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            targetCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        // ensure exact final value
        targetCanvasGroup.alpha = targetAlpha;
        ApplyInteractableIfNeeded(targetAlpha);

        if (debugLogs) Debug.Log("[HamogManager] Fade completed.");
        runningFade = null;
    }

    void ApplyInteractableIfNeeded(float alpha)
    {
        if (!autoToggleInteractable || targetCanvasGroup == null) return;

        bool visible = alpha > 0.01f;
        targetCanvasGroup.interactable = visible;
        targetCanvasGroup.blocksRaycasts = visible;
    }

    /// <summary>
    /// Convenience overload if you want to pass floats instead (0..1 alpha, and float seconds).
    /// </summary>
    public void FadeCanvasToAlphaFloat(float alpha01, float durationSeconds)
    {
        if (targetCanvasGroup == null)
        {
            Debug.LogWarning("[HamogManager] targetCanvasGroup is not assigned.");
            return;
        }

        int percent = Mathf.RoundToInt(Mathf.Clamp01(alpha01) * 100f);
        FadeCanvasToAlpha(percent, Mathf.Max(0, Mathf.RoundToInt(durationSeconds)));
    }

    /// <summary>
    /// Instantly set alpha (bypass coroutine).
    /// </summary>
    public void SetAlphaInstant(int alphaPercent)
    {
        if (targetCanvasGroup == null)
        {
            Debug.LogWarning("[HamogManager] targetCanvasGroup is not assigned.");
            return;
        }

        float targetAlpha = Mathf.Clamp01(alphaPercent / 100f);
        if (runningFade != null)
        {
            StopCoroutine(runningFade);
            runningFade = null;
        }

        targetCanvasGroup.alpha = targetAlpha;
        ApplyInteractableIfNeeded(targetAlpha);
    }
}

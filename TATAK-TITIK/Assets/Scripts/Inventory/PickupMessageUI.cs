using TMPro;
using UnityEngine;
using System.Collections;

public class PickupMessageUI : MonoBehaviour
{
    public TextMeshProUGUI messageText;
    private CanvasGroup canvasGroup;

    public float displayTime = 2f;
    public float fadeDuration = 1f;

    private void Awake()
    {
        // Automatically grab CanvasGroup from the same object as the message text
        canvasGroup = messageText.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            Debug.LogError("No CanvasGroup found on messageText!");
    }

    public void ShowMessage(string msg)
    {
        Debug.Log($"Showing pickup message: {msg}");
        StopAllCoroutines();

        messageText.text = msg;
        canvasGroup.alpha = 1f;
        StartCoroutine(FadeOut());
    }

    IEnumerator FadeOut()
    {
        yield return new WaitForSeconds(displayTime);

        float timer = 0f;
        float startAlpha = canvasGroup.alpha;

        while (timer < fadeDuration)
        {
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, timer / fadeDuration);
            timer += Time.deltaTime;
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }
}


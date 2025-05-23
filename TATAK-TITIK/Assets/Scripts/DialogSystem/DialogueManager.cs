using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    [Header("References")]
    public CanvasGroup pressEPromptGroup;
    public CanvasGroup dialoguePanelGroup;
    public TMP_Text dialogueText;

    [Header("Settings")]
    public float fadeDuration = 0.2f;
    public float typeDelay = 0.03f;

    private Coroutine typewriterCoroutine;
    private bool isFading = false;
    private bool dialogueVisible = false;

    private string currentDialogue = "";
    private NPCDialogueTrigger currentNPC = null;

    void Start()
    {
        SetCanvasGroup(pressEPromptGroup, 0, false);
        SetCanvasGroup(dialoguePanelGroup, 0, false);
    }

    void Update()
    {
        if (currentNPC == null)
        {
            if (dialogueVisible || pressEPromptGroup.alpha > 0)
                StartCoroutine(CloseDialogueAndPrompt());
            return;
        }

        if (Input.GetKeyDown(KeyCode.E) && !isFading)
        {
            if (!dialogueVisible)
            {
                StartDialogue(currentNPC.dialogue);
            }
            else
            {
                if (typewriterCoroutine != null)
                {
                    // Skip typewriter effect - show full text immediately
                    StopCoroutine(typewriterCoroutine);
                    typewriterCoroutine = null;
                    dialogueText.text = currentDialogue;
                }
                else
                {
                    // Close dialogue panel and show prompt again
                    CloseDialogue();
                }
            }
        }

        if (dialogueVisible && Input.GetMouseButtonDown(0))
        {
            CloseDialogue();
        }
    }

    public void ShowPromptFor(NPCDialogueTrigger npc)
    {
        currentNPC = npc;
        ResetDialogueState();  // clear old stuff on new NPC enter
        StartCoroutine(FadeCanvasGroup(pressEPromptGroup, 1, true));
    }

    public void HidePromptFor(NPCDialogueTrigger npc)
    {
        if (currentNPC == npc)
        {
            currentNPC = null;
            StartCoroutine(ClosePrompt());
            ResetDialogueState();
        }
    }

    private IEnumerator ClosePrompt()
    {
        yield return FadeCanvasGroup(pressEPromptGroup, 0, false);
    }

    private void ResetDialogueState()
    {
        dialogueVisible = false;
        isFading = false;

        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        dialogueText.text = "";
        StopAllCoroutines();

        SetCanvasGroup(dialoguePanelGroup, 0, false);
    }

    private void StartDialogue(string dialogue)
    {
        if (isFading) return;

        currentDialogue = dialogue;
        dialogueVisible = true;
        StartCoroutine(TransitionToDialogue());
    }

    private void CloseDialogue()
    {
        if (isFading) return;

        dialogueVisible = false;
        StartCoroutine(CloseDialoguePanel());

        // Fade prompt back in if still near NPC
        if (currentNPC != null)
        {
            StartCoroutine(FadeCanvasGroup(pressEPromptGroup, 1, true));
        }
    }

    private IEnumerator CloseDialoguePanel()
    {
        isFading = true;

        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        dialogueText.text = "";

        yield return FadeCanvasGroup(dialoguePanelGroup, 0, false);

        isFading = false;
    }

    private IEnumerator TransitionToDialogue()
    {
        isFading = true;

        // Fade out press E prompt
        yield return FadeCanvasGroup(pressEPromptGroup, 0, false);
        yield return new WaitForSeconds(0.05f);

        // Fade in dialogue panel
        yield return FadeCanvasGroup(dialoguePanelGroup, 1, true);

        // Start typewriter effect
        dialogueText.text = "";
        typewriterCoroutine = StartCoroutine(TypeText(currentDialogue));

        isFading = false;
    }

    private IEnumerator CloseDialogueAndPrompt()
    {
        isFading = true;

        // Stop typewriter if running
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        // Clear dialogue text
        dialogueText.text = "";

        // Fade out both dialogue panel and prompt
        yield return FadeCanvasGroup(dialoguePanelGroup, 0, false);
        yield return FadeCanvasGroup(pressEPromptGroup, 0, false);

        isFading = false;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float targetAlpha, bool enableAtEnd)
    {
        if (group == null) yield break;

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
        group.interactable = enableAtEnd;
        group.blocksRaycasts = enableAtEnd;

        if (!enableAtEnd)
            group.gameObject.SetActive(false);
    }

    private IEnumerator TypeText(string fullText)
    {
        dialogueText.text = "";

        foreach (char c in fullText)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(typeDelay);
        }

        typewriterCoroutine = null;
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

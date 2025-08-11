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

    [Header("Event Managers (optional)")]
    [Tooltip("Assign the DialogueEventsManager in the scene here. If left empty, the script will use DialogueEventsManager.Instance if available.")]
    public DialogueEventsManager dialogueEventsManager;

    private Coroutine typewriterCoroutine;
    private bool isFading = false;
    private bool dialogueVisible = false;

    private string currentDialogue = "";
    private NPCDialogueTrigger currentNPC = null;

    private string[] currentDialogueLines;
    private int currentLineIndex = 0;
    private SceneButtonManager SBM;

    void Start()
    {
        SetCanvasGroup(pressEPromptGroup, 0, false);
        SetCanvasGroup(dialoguePanelGroup, 0, false);

        // fallback to singleton if inspector reference wasn't set
        if (dialogueEventsManager == null && DialogueEventsManager.Instance != null)
            dialogueEventsManager = DialogueEventsManager.Instance;
    }

    void Update()
    {
        SceneButtonManager sbm = FindObjectOfType<SceneButtonManager>();
        if (currentNPC == null)
        {
            if (dialogueVisible || (pressEPromptGroup != null && pressEPromptGroup.alpha > 0))
                StartCoroutine(CloseDialogueAndPrompt());
            return;
        }

        if (Input.GetKeyDown(KeyCode.E) && !isFading && sbm.IsEKeyEnabled())
        {
            if (!dialogueVisible)
            {
                StartDialogue(currentNPC.GetDialogueLines());
            }
            else
            {
                if (typewriterCoroutine != null)
                {
                    StopCoroutine(typewriterCoroutine);
                    typewriterCoroutine = null;
                    dialogueText.text = currentDialogue;
                }
                else
                {
                    currentLineIndex++;
                    if (currentLineIndex < currentDialogueLines.Length)
                    {
                        currentDialogue = currentDialogueLines[currentLineIndex];
                        typewriterCoroutine = StartCoroutine(TypeText(currentDialogue));
                    }
                    else
                    {
                        CloseDialogue();
                    }
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

    private void StartDialogue(string[] dialogueLines)
    {
        if (isFading || dialogueLines == null || dialogueLines.Length == 0) return;

        currentDialogueLines = dialogueLines;
        currentLineIndex = 0;
        dialogueVisible = true;
        currentDialogue = currentDialogueLines[currentLineIndex];

        // Mark in DialogueEventsManager that this NPC/gameobject was triggered (player explicitly began the dialogue).
        if (currentNPC != null)
        {
            dialogueEventsManager?.AddToTriggeredList(currentNPC.gameObject.name);
        }

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

        // Dialogue panel has closed — consider this the "finished" moment and also mark DEM (safe duplicate)
        if (currentNPC != null)
        {
            dialogueEventsManager?.AddToTriggeredList(currentNPC.gameObject.name);
        }

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

    public void ForceHidePrompt()
    {
        StopAllCoroutines(); // stop fading if in progress
        SetCanvasGroup(pressEPromptGroup, 0f, false);
    }

    public bool HasCurrentNPC()
    {
        return currentNPC != null;
    }

    public bool IsPromptVisible()
    {
        return pressEPromptGroup != null && pressEPromptGroup.alpha > 0f;
    }

    public void RefreshPromptIfNeeded()
    {
        if (currentNPC != null && !IsPromptVisible())
        {
            StartCoroutine(FadeCanvasGroup(pressEPromptGroup, 1, true));
        }
    }
}

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

    // Coroutine handles so we only stop what we started
    private Coroutine typewriterCoroutine;
    private Coroutine backgroundFadeCoroutine;
    private Coroutine panelFadeCoroutine;
    private Coroutine promptFadeCoroutine;
    private bool isFading = false;
    private bool dialogueVisible = false;

    private string currentDialogue = "";
    private NPCDialogueTrigger currentNPC = null;

    private string[] currentDialogueLines;
    private int currentLineIndex = 0;
    private SceneButtonManager SBM;
    
    [Header("Background (optional)")]
    [Tooltip("CanvasGroup used as a dim background behind the dialogue panel. Leave null to disable.")]
    public CanvasGroup dialogueBackgroundGroup;

    void Start()
    {
        SetCanvasGroup(pressEPromptGroup, 0, false);
        SetCanvasGroup(dialoguePanelGroup, 0, false);
        SetCanvasGroup(dialogueBackgroundGroup, 0f, false);

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
        if (pressEPromptGroup != null)
            promptFadeCoroutine = StartCoroutine(FadeCanvasGroup(pressEPromptGroup, 1, true));
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

        // Stop only the specific fade coroutines we might have started
        if (backgroundFadeCoroutine != null) { StopCoroutine(backgroundFadeCoroutine); backgroundFadeCoroutine = null; }
        if (panelFadeCoroutine != null) { StopCoroutine(panelFadeCoroutine); panelFadeCoroutine = null; }
        if (promptFadeCoroutine != null) { StopCoroutine(promptFadeCoroutine); promptFadeCoroutine = null; }

        dialogueText.text = "";

        // Do NOT call StopAllCoroutines() here — it cancels fades in progress elsewhere.

        SetCanvasGroup(dialoguePanelGroup, 0, false);
        SetCanvasGroup(dialogueBackgroundGroup, 0f, false);
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

        // stop panel fade if still running
        if (panelFadeCoroutine != null) { StopCoroutine(panelFadeCoroutine); panelFadeCoroutine = null; }
        if (dialoguePanelGroup != null)
            panelFadeCoroutine = StartCoroutine(FadeCanvasGroup(dialoguePanelGroup, 0, false));
        yield return panelFadeCoroutine;

        // Fade out the dim background as the dialogue fully closes
        if (backgroundFadeCoroutine != null) { StopCoroutine(backgroundFadeCoroutine); backgroundFadeCoroutine = null; }
        if (dialogueBackgroundGroup != null)
            backgroundFadeCoroutine = StartCoroutine(FadeCanvasGroup(dialogueBackgroundGroup, 0f, false));
        yield return backgroundFadeCoroutine;

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
        if (promptFadeCoroutine != null) { StopCoroutine(promptFadeCoroutine); promptFadeCoroutine = null; }
        if (pressEPromptGroup != null)
            promptFadeCoroutine = StartCoroutine(FadeCanvasGroup(pressEPromptGroup, 0, false));
        yield return promptFadeCoroutine;

        yield return new WaitForSeconds(0.05f);

        // Start background fade but KEEP the GameObject active at the end
        if (backgroundFadeCoroutine != null) { StopCoroutine(backgroundFadeCoroutine); backgroundFadeCoroutine = null; }
        if (dialogueBackgroundGroup != null)
            backgroundFadeCoroutine = StartCoroutine(FadeCanvasGroup(dialogueBackgroundGroup, 1f, true));

        // Ensure the background does NOT block input even though it stays active
        if (dialogueBackgroundGroup != null)
        {
            dialogueBackgroundGroup.interactable = false;
            dialogueBackgroundGroup.blocksRaycasts = false;
        }

        // Fade in dialogue panel and wait so the panel becomes interactable only after fade finishes
        if (panelFadeCoroutine != null) { StopCoroutine(panelFadeCoroutine); panelFadeCoroutine = null; }
        if (dialoguePanelGroup != null)
            panelFadeCoroutine = StartCoroutine(FadeCanvasGroup(dialoguePanelGroup, 1, true));
        yield return panelFadeCoroutine;

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

        // Fade out both dialogue panel and background and prompt — stop any running fades first
        if (panelFadeCoroutine != null) { StopCoroutine(panelFadeCoroutine); panelFadeCoroutine = null; }
        if (dialoguePanelGroup != null)
            panelFadeCoroutine = StartCoroutine(FadeCanvasGroup(dialoguePanelGroup, 0, false));
        yield return panelFadeCoroutine;

        if (backgroundFadeCoroutine != null) { StopCoroutine(backgroundFadeCoroutine); backgroundFadeCoroutine = null; }
        if (dialogueBackgroundGroup != null)
            backgroundFadeCoroutine = StartCoroutine(FadeCanvasGroup(dialogueBackgroundGroup, 0f, false));
        yield return backgroundFadeCoroutine;

        if (promptFadeCoroutine != null) { StopCoroutine(promptFadeCoroutine); promptFadeCoroutine = null; }
        if (pressEPromptGroup != null)
            promptFadeCoroutine = StartCoroutine(FadeCanvasGroup(pressEPromptGroup, 0, false));
        yield return promptFadeCoroutine;

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
        // stop only our tracked coroutines
        if (backgroundFadeCoroutine != null) { StopCoroutine(backgroundFadeCoroutine); backgroundFadeCoroutine = null; }
        if (panelFadeCoroutine != null) { StopCoroutine(panelFadeCoroutine); panelFadeCoroutine = null; }
        if (promptFadeCoroutine != null) { StopCoroutine(promptFadeCoroutine); promptFadeCoroutine = null; }
        if (typewriterCoroutine != null) { StopCoroutine(typewriterCoroutine); typewriterCoroutine = null; }

        SetCanvasGroup(pressEPromptGroup, 0f, false);
        SetCanvasGroup(dialogueBackgroundGroup, 0f, false);
        SetCanvasGroup(dialoguePanelGroup, 0f, false);
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

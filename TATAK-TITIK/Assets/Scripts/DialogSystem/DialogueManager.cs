using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DialogueManager : MonoBehaviour
{
    [Header("References")]
    public CanvasGroup pressEPromptGroup;
    public CanvasGroup dialoguePanelGroup;
    // track last E-key enabled state so we can react when it returns
    private bool lastEKeyEnabled = true;

    // small guard so we don't start multiple prompt fade coroutines
    private bool promptFadeInPending = false;

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

        // Keep a safe default if sbm is not present
        bool eKeyEnabled = sbm != null ? sbm.IsEKeyEnabled() : true;

        // If currentNPC is null, ensure prompt & dialogue are closed, then bail out
        if (currentNPC == null)
        {
            if (dialogueVisible || (pressEPromptGroup != null && pressEPromptGroup.alpha > 0))
                StartCoroutine(CloseDialogueAndPrompt());

            // update lastEKeyEnabled for next frame
            lastEKeyEnabled = eKeyEnabled;
            return;
        }

        // If we have a current NPC and the E-key gating just returned to enabled, refresh the prompt
        // Also refresh if prompt isn't visible and dialogue isn't open
        if (!dialogueVisible && currentNPC != null)
        {
            bool promptVisible = (pressEPromptGroup != null && pressEPromptGroup.gameObject.activeInHierarchy && pressEPromptGroup.alpha > 0f);
            if (eKeyEnabled && !promptVisible && !promptFadeInPending)
            {
                // Start fade-in
                promptFadeInPending = true;
                StartCoroutine(ShowPromptDeferred());
            }
        }
        if (Input.GetKeyDown(KeyCode.E) && !isFading && eKeyEnabled)
        {
            if (!dialogueVisible)
                StartDialogue(currentNPC.GetDialogueLines());
            else
                AdvanceOrFinishDialogue();
        }

        // Mouse click: call the same helper; but ignore clicks over UI
        if (dialogueVisible && Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                AdvanceOrFinishDialogue();
        }

        // store for next frame
        lastEKeyEnabled = eKeyEnabled;
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

    // Replace existing FadeCanvasGroup with this unscaled-time version
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
            elapsed += Time.unscaledDeltaTime; // <-- use unscaled time so fades work when timescale==0
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
        if (dialogueText == null)
            yield break;

        // Ensure rich text is enabled
        dialogueText.richText = true;

        // Assign full text (including <color> / <b> tags) upfront so TMP can parse tags
        dialogueText.text = fullText;

        // Force TMP to update its internal textInfo so character count is correct (tags are ignored)
        dialogueText.ForceMeshUpdate();

        int totalVisible = dialogueText.textInfo.characterCount; // visible characters (tags excluded)
        dialogueText.maxVisibleCharacters = 0; // hide all visible chars to start

        int visible = 0;
        // Reveal one visible character at a time
        while (visible <= totalVisible)
        {
            dialogueText.maxVisibleCharacters = visible;
            visible++;
            yield return new WaitForSeconds(typeDelay);
        }

        // Ensure everything is visible at the end and clear our handle
        dialogueText.maxVisibleCharacters = totalVisible;
        typewriterCoroutine = null;
    }

    private void SetCanvasGroup(CanvasGroup group, float alpha, bool interactable)
    {
        if (group == null) return;
        group.alpha = alpha;
        group.interactable = interactable;
        group.blocksRaycasts = interactable;
        // Do not toggle active state to avoid losing prompt GameObject entirely
        // group.gameObject.SetActive(alpha > 0f);
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

        // Cancel any pending prompt fade-in request
        promptFadeInPending = false;
    }

    public bool HasCurrentNPC()
    {
        return currentNPC != null;
    }

    public bool IsPromptVisible()
    {
        return pressEPromptGroup != null && pressEPromptGroup.alpha > 0f;
    }

    // Add this public getter (near HasCurrentNPC / IsPromptVisible)
    public bool IsDialogueVisible()
    {
        return dialogueVisible;
    }
    public void RefreshPromptIfNeeded()
    {
        // If a dialogue is currently visible, don't try to refresh the prompt.
        if (dialogueVisible) return;

        if (currentNPC != null && !IsPromptVisible())
        {
            StartCoroutine(FadeCanvasGroup(pressEPromptGroup, 1, true));
        }
    }
    /// <summary>
    /// Fade the press-E prompt in but wait one frame so other UI closures can finish.
    /// Ensures we don't race with other UI state changes (journal/menus).
    /// </summary>
    private IEnumerator ShowPromptDeferred()
    {
        // small delay to allow whatever UI just closed to finish disabling
        yield return null;
        promptFadeInPending = false;

        // If a dialogue is active, never show the prompt.
        if (dialogueVisible) yield break;

        if (currentNPC == null) yield break; // no longer in range
        if (pressEPromptGroup == null) yield break;

        // If there's already a fade coroutine running for the prompt, don't start another
        promptFadeCoroutine = StartCoroutine(FadeCanvasGroup(pressEPromptGroup, 1, true));
    }

    private void AdvanceOrFinishDialogue()
    {
        if (!dialogueVisible) return;

        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;

            dialogueText.text = currentDialogue;
            dialogueText.ForceMeshUpdate();
            dialogueText.maxVisibleCharacters = dialogueText.textInfo.characterCount;
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

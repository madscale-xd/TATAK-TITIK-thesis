using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
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

    // --- replace existing PendingDialogue struct with this ---
    private struct PendingDialogue
    {
        public GameObject npcObject;
        public string npcID;
        // snapshot of lines to play later (may be null -> fall back to trigger.GetDialogueLines())
        public string[] dialogueLines;
        // snapshot of journal entries to add when this dialogue runs (may be null)
        public JournalTriggerEntry[] journalEntries;

        public PendingDialogue(GameObject obj, string id, string[] lines, JournalTriggerEntry[] journals)
        {
            npcObject = obj;
            npcID = id;
            dialogueLines = lines;
            journalEntries = journals;
        }
    }


    // queue
    private Queue<PendingDialogue> dialogueQueue = new Queue<PendingDialogue>();

    // processing flag (similar to your nav queue pattern)
    private bool processingDialogueQueue = false;

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

        // Decide whether to keep the background visible based ONLY on actual queued items.
        // processingDialogueQueue can be true while the queue is empty (because the processor coroutine is active),
        // so relying on it causes the background to remain visible incorrectly.
        bool keepBackground = (dialogueQueue.Count > 0);

        if (dialogueBackgroundGroup != null)
        {
            // Stop any running background fade first
            if (backgroundFadeCoroutine != null) { StopCoroutine(backgroundFadeCoroutine); backgroundFadeCoroutine = null; }

            if (keepBackground)
            {
                // Keep background visible without re-fading (avoids flicker)
                SetCanvasGroup(dialogueBackgroundGroup, 1f, false);
            }
            else
            {
                backgroundFadeCoroutine = StartCoroutine(FadeCanvasGroup(dialogueBackgroundGroup, 0f, false));
                yield return backgroundFadeCoroutine;
                backgroundFadeCoroutine = null;
            }
        }

        // Consider this the "finished" moment and mark DEM (single call)
        if (currentNPC != null)
        {
            dialogueEventsManager?.AddToTriggeredList(currentNPC.gameObject.name);

            var finishedNpcMgr = currentNPC.gameObject.GetComponent<NPCManager>();
            if (finishedNpcMgr != null)
                finishedNpcMgr.NotifyDialogueFinished();
        }

        isFading = false;

        // try to play any queued dialogue — start processor if not already running
        if (!processingDialogueQueue)
            StartCoroutine(ProcessDialogueQueue());
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

        // Make sure dialogue text is empty BEFORE the panel is re-activated/faded in to avoid showing the previous dialog.
        if (dialogueText != null)
        {
            dialogueText.text = "";
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();
        }

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
        // make sure currentDialogue is already set by the caller
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

    /// <summary>
    /// Enqueue dialogue for the given NPC GameObject with explicit lines and explicit journal entries.
    /// Pass null for lines to use the NPCTrigger's current lines; pass null for journalEntries to skip journals.
    /// </summary>
    public void PlayDialogueFor(GameObject npcObject, string npcID, string[] explicitLines, JournalTriggerEntry[] explicitJournalEntries)
    {
        if (npcObject == null)
        {
            Debug.LogWarning("[DialogueManager] PlayDialogueFor (lines+journals) called with null npcObject.");
            return;
        }

        // clone to avoid external mutation
        string[] linesSnapshot = explicitLines != null ? (string[])explicitLines.Clone() : null;
        JournalTriggerEntry[] journalSnapshot = null;
        if (explicitJournalEntries != null)
            journalSnapshot = (JournalTriggerEntry[])explicitJournalEntries.Clone();

        dialogueQueue.Enqueue(new PendingDialogue(npcObject, npcID, linesSnapshot, journalSnapshot));
        Debug.Log($"[DialogueManager] PlayDialogueFor(lines+journals): enqueued '{npcObject.name}' id='{npcID}'. QueueCount={dialogueQueue.Count}");

        if (!processingDialogueQueue)
            StartCoroutine(ProcessDialogueQueue());
    }



    // Starts dialogue using a PendingDialogue (uses snapshot if provided)
    private void StartForcedDialogue(PendingDialogue pd)
    {
        if (pd.npcObject == null)
        {
            Debug.LogWarning("[DialogueManager] StartForcedDialogue: pd.npcObject is null.");
            return;
        }

        // Try to find the trigger
        var trigger = pd.npcObject.GetComponent<NPCDialogueTrigger>();
        if (trigger == null)
        {
            Debug.LogWarning($"[DialogueManager] StartForcedDialogue: no NPCDialogueTrigger found on '{pd.npcObject.name}'.");
            return;
        }

        // set the currentNPC so rest of system behaves normally
        currentNPC = trigger;

        // Use snapshot if present, otherwise read from the trigger
        currentDialogueLines = pd.dialogueLines ?? trigger.GetDialogueLines();
        if (currentDialogueLines == null || currentDialogueLines.Length == 0)
        {
            Debug.LogWarning($"[DialogueManager] StartForcedDialogue: NPC '{pd.npcObject.name}' has no dialogue lines.");
            currentNPC = null;
            return;
        }

        currentLineIndex = 0;
        currentDialogue = currentDialogueLines[currentLineIndex];
        dialogueVisible = true;

        // Notify NPC (if it has an NPCManager)
        var npcMgr = pd.npcObject.GetComponent<NPCManager>();
        if (npcMgr != null)
        {
            npcMgr.NotifyDialogueStarted();
        }

        // Optionally mark the DialogueEventsManager with the provided npcID (so it counts as triggered)
        if (!string.IsNullOrEmpty(pd.npcID))
        {
            dialogueEventsManager?.AddToTriggeredList(pd.npcID);
        }

       // If there's a JournalTrigger component on the NPC (or child), set snapshot first then add entries
        var journal = pd.npcObject.GetComponent<JournalTrigger>()
                    ?? pd.npcObject.GetComponentInChildren<JournalTrigger>(true);
        if (journal != null)
        {
            // If this queued item carried journal entries, apply them to the JournalTrigger before adding to the journal.
            if (pd.journalEntries != null)
            {
                try
                {
                    journal.SetEntries(pd.journalEntries); // NPCManager used this earlier so JournalTrigger should expose it
                    Debug.Log($"[DialogueManager] StartForcedDialogue: Applied {pd.journalEntries.Length} journal entries to '{pd.npcObject.name}'.");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[DialogueManager] StartForcedDialogue: failed to SetEntries on '{pd.npcObject.name}': {ex}");
                }
            }

            // finally add to the player's journal (original behavior)
            journal.AddEntryToJournal();
            Debug.Log($"[DialogueManager] StartForcedDialogue: Added journal entries from '{pd.npcObject.name}'.");
        }
        // Ensure the UI text is cleared immediately so the panel won't show previous text while fading in.
        if (dialogueText != null)
        {
            dialogueText.text = "";
            dialogueText.maxVisibleCharacters = 0;
            // Force TMP update to ensure internal counts are correct
            dialogueText.ForceMeshUpdate();
        }

        // Kick off UI transition
        StartCoroutine(TransitionToDialogue());
    }

   private IEnumerator ProcessDialogueQueue()
    {
        processingDialogueQueue = true;

        while (dialogueQueue.Count > 0)
        {
            var pd = dialogueQueue.Dequeue();
            Debug.Log($"[DialogueManager] ProcessDialogueQueue: next '{pd.npcObject?.name}' id='{pd.npcID}'. Remaining={dialogueQueue.Count}");

            // WAIT here until any currently-displayed dialogue + fades finish.
            // This prevents the queued dialogue from replacing an in-progress dialogue.
            yield return new WaitUntil(() => !dialogueVisible && !isFading);

            // Start the dialogue for this queued item (uses snapshot set on pd)
            StartForcedDialogue(pd);

            // Wait until that dialogue fully finishes and the UI is settled
            yield return new WaitUntil(() => !dialogueVisible && !isFading);

            // small one-frame delay to let UI state settle before starting the next one
            yield return null;
        }

        processingDialogueQueue = false;
        Debug.Log("[DialogueManager] ProcessDialogueQueue: queue empty, processor stopped.");
    }
}
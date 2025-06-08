using UnityEngine;

public class NPCDialogueTrigger : MonoBehaviour
{
    [TextArea(2, 5)]
    public string[] dialogueLines;
    private DialogueManager dialogueManager;

    private PlayerInteraction playerInteraction;

    private void Start()
    {
        dialogueManager = FindObjectOfType<DialogueManager>();
        playerInteraction = FindObjectOfType<PlayerInteraction>();

        if (dialogueManager == null)
            Debug.LogError("DialogueManager not found in scene!");
        if (playerInteraction == null)
            Debug.LogError("PlayerInteraction not found in scene!");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            dialogueManager.ShowPromptFor(this);
            playerInteraction.SetCurrentNPC(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            dialogueManager.HidePromptFor(this);
            playerInteraction.ClearCurrentNPC();
        }
    }

    public string[] GetDialogueLines()
    {
        return dialogueLines;
    }
}

using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    private NPCDialogueTrigger currentNPC;
    private SceneButtonManager sbm;

    private void Awake()
    {
        sbm = FindObjectOfType<SceneButtonManager>();
        if (sbm == null)
            Debug.LogWarning("[PlayerInteraction] SceneButtonManager not found in Awake.");
    }

    void Update()
    {
        // Cache sbm lookup fallback (just in case)
        if (sbm == null) sbm = FindObjectOfType<SceneButtonManager>();
        if (sbm == null) return;

        if (Input.GetKeyDown(KeyCode.E) && currentNPC != null && sbm.IsEKeyEnabled())
        {
            // Use TryGetComponent to avoid GetComponent allocations & to be robust
            if (currentNPC.TryGetComponent<JournalTrigger>(out var jt))
            {
                jt.AddEntryToJournal();
            }

            // NOTE: DialogueManager still controls starting the dialogue UI in your project,
            // so we leave that responsibility to DialogueManager (which listens for E too).
        }
    }

    public void SetCurrentNPC(NPCDialogueTrigger npc)
    {
        currentNPC = npc;
    }

    public void ClearCurrentNPC(NPCDialogueTrigger npc)
    {
        if (currentNPC == npc)
            currentNPC = null;
    }
}

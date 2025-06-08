using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    private NPCDialogueTrigger currentNPC;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E) && currentNPC != null)
        {
            // Call JournalTrigger on the NPC if it has one
            JournalTrigger jt = currentNPC.GetComponent<JournalTrigger>();
            if (jt != null)
            {
                jt.AddEntryToJournal();
            }

            // Also trigger dialogue or other interaction here if needed
        }
    }

    // Called by NPCDialogueTrigger when player enters trigger
    public void SetCurrentNPC(NPCDialogueTrigger npc)
    {
        currentNPC = npc;
    }

    // Called by NPCDialogueTrigger when player leaves trigger
    public void ClearCurrentNPC()
    {
        currentNPC = null;
    }
}

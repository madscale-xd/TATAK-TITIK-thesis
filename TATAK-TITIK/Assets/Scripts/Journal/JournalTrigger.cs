using UnityEngine;

public class JournalTrigger : MonoBehaviour
{
    public string keyword;         // The key identifier (like "ancientSword")
    public string displayWord;     // The actual word shown in journal (like "Ancient Sword")

    private bool hasTriggered = false;

    public void AddEntryToJournal()
    {
        if (hasTriggered) return;

        JournalManager.Instance.AddEntry(keyword, displayWord);
        hasTriggered = true;
    }
}


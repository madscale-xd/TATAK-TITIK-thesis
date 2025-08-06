using UnityEngine;

public class JournalPage : MonoBehaviour
{
    [SerializeField] private GameObject placeholder;

    // Call this when adding a journal entry to this page
    public void DisablePlaceholderIfNeeded()
    {
        if (placeholder != null && placeholder.activeSelf)
        {
            placeholder.SetActive(false);
        }
    }
}

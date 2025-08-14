using UnityEngine;

[System.Serializable]
public class JournalTriggerEntry
{
    public string key;         // unique identifier (e.g. "ancientSword")
    public string displayWord; // visible text in the journal (e.g. "Ancient Sword")
}

public class JournalTrigger : MonoBehaviour
{
    [Header("Multiple entries (preferred)")]
    [Tooltip("Add one or more entries here. If empty, legacy single entry fields below will be used.")]
    public JournalTriggerEntry[] entries = new JournalTriggerEntry[0];

    [Header("Legacy single entry (kept for compatibility)")]
    [Tooltip("If 'entries' is empty, these fields are used (same behavior as the old single-entry trigger).")]
    public string keyword;
    public string displayWord;

    [Header("Options")]
    [Tooltip("If true, the trigger will try to add entries when a public AddEntryToJournal() call is made.")]
    public bool enabledOnStart = true;

    private void Start()
    {
        // Optionally do nothing here — kept for clarity/future hooks.
        if (!enabledOnStart)
            Debug.Log($"[JournalTrigger] '{gameObject.name}' is disabled at start (enabledOnStart=false).");
    }

    /// <summary>
    /// Add the configured entries to the JournalManager.
    /// This is intentionally repeatable — JournalManager will ignore duplicate keys.
    /// </summary>
    public void AddEntryToJournal()
    {
        if (!enabledOnStart)
            return;

        if (JournalManager.Instance == null)
        {
            Debug.LogWarning("[JournalTrigger] JournalManager.Instance not found. Cannot add entries.");
            return;
        }

        // If entries array is populated, use it (preferred).
        if (entries != null && entries.Length > 0)
        {
            foreach (var e in entries)
            {
                if (e == null) continue;
                if (string.IsNullOrWhiteSpace(e.key)) continue;

                JournalManager.Instance.AddEntry(e.key, e.displayWord ?? "");
            }
            return;
        }

        // Fallback to legacy single entry
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            JournalManager.Instance.AddEntry(keyword, displayWord ?? "");
        }
    }

    /// <summary>
    /// Replace the entries list for this trigger at runtime.
    /// </summary>
    public void SetEntries(JournalTriggerEntry[] newEntries)
    {
        entries = newEntries ?? new JournalTriggerEntry[0];
    }

    /// <summary>
    /// Convenience to set a single entry (will replace the entries array).
    /// Also updates legacy fields for inspector readability.
    /// </summary>
    public void SetSingleEntry(string key, string display)
    {
        entries = new JournalTriggerEntry[] { new JournalTriggerEntry { key = key, displayWord = display } };
        keyword = key;
        displayWord = display;
    }

    /// <summary>
    /// Clear all configured entries.
    /// </summary>
    public void ClearEntries()
    {
        entries = new JournalTriggerEntry[0];
        keyword = "";
        displayWord = "";
    }
}

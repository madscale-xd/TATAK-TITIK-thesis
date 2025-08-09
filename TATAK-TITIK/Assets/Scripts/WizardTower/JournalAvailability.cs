using UnityEngine;
using System;

public class JournalAvailability : MonoBehaviour
{
    // Singleton accessor for easy cross-script calls
    public static JournalAvailability Instance { get; private set; }

    [Header("Journal Availability")]
    [Tooltip("True when the player's journal becomes available (e.g. after the WizardTower item interaction).")]
    public bool isAvailable = false;

    /// <summary>
    /// Fired whenever availability changes. Subscribers receive the new value.
    /// </summary>
    public event Action<bool> OnAvailabilityChanged;

    private void Awake()
    {
        // Basic singleton pattern so other scripts can easily do JournalAvailability.Instance
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // optional: keep across scenes if you want persistence
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    /// <summary>
    /// Turn journal availability on.
    /// Call this from your WizardTower item interaction script when the condition is met.
    /// </summary>
    public void EnableJournal()
    {
        SetAvailable(true);
    }

    /// <summary>
    /// Turn journal availability off (e.g. after clearing saves).
    /// Call this from SaveLoadManager.ClearGame(...) or wherever appropriate.
    /// </summary>
    public void DisableJournal()
    {
        SetAvailable(false);
    }

    /// <summary>
    /// Set availability explicitly.
    /// </summary>
    public void SetAvailable(bool available)
    {
        if (isAvailable == available) return;
        isAvailable = available;
        OnAvailabilityChanged?.Invoke(isAvailable);

        // If it just became available, do an automatic save of the current slot
        if (isAvailable)
        {
            if (SaveLoadManager.Instance != null)
            {
                int slot = SaveLoadManager.Instance.currentSaveSlot;
                SaveLoadManager.Instance.SaveGame(slot);
                Debug.Log("[JournalAvailability] Saved game because journal became available.");
            }
            else
            {
                Debug.LogWarning("[JournalAvailability] SaveLoadManager not found — couldn't save automatically.");
            }
        }
    }


    /// <summary>
    /// Query helper (redundant since isAvailable is public, but nice for readability).
    /// </summary>
    public bool IsAvailable()
    {
        return isAvailable;
    }
}

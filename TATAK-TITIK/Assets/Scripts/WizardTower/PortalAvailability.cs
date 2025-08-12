using System;
using UnityEngine;

public class PortalAvailability : MonoBehaviour
{
    public event Action<bool> OnAvailabilityChanged;

    private bool available = false;

    // NOTE: No static Instance and no DontDestroyOnLoad — this object lives in the current scene only.
    private void Awake()
    {
        // Keep Awake minimal; the GameObject must be active in the scene so this runs.
    }

    public bool IsAvailable() => available;

    public void SetAvailable(bool value)
    {
        if (available == value) return;
        available = value;
        OnAvailabilityChanged?.Invoke(available);
        Debug.Log($"[PortalAvailability] SetAvailable = {available}");
    }

    // Optional convenience
    public void DisablePortal()
    {
        SetAvailable(false);
    }
}

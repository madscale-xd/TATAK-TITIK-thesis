using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class PortalAcquisition : MonoBehaviour
{
    [Header("Pickup")]
    public string playerTag = "Player";  // tag the player uses

    [Tooltip("Scene name to load when the portal is picked up.")]
    public string sceneToLoad = "TestScene";

    [Tooltip("Optional unique persist ID you can use with your SaveLoadManager to detect duplicates or saved state.")]
    public string persistID = "";

    Collider myCollider;
    Renderer[] renderers;
    bool pickedUp = false;

    void Start()
    {
        myCollider = GetComponent<Collider>();
        if (myCollider != null) myCollider.isTrigger = true;
        renderers = GetComponentsInChildren<Renderer>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (pickedUp) return;
        if (!other.CompareTag(playerTag)) return;

        pickedUp = true;
        // disable the collider immediately to avoid double-triggering while scene loads
        if (myCollider != null) myCollider.enabled = false;
        StartCoroutine(HandlePickupCoroutine());
    }

    private IEnumerator HandlePickupCoroutine()
    {
        // Ensure a valid save slot (so any SaveLoad calls use a valid slot)
        var slm = SaveLoadManager.Instance;
        if (slm != null && slm.currentSaveSlot <= 0)
        {
            Debug.Log("[PortalAcquisition] No active save slot found. Using slot 1.");
            slm.currentSaveSlot = 1;
        }

        // Find and enable the PortalAvailability in the current scene (not a singleton)
        PortalAvailability availability = FindObjectOfType<PortalAvailability>();
        if (availability != null)
        {
            availability.SetAvailable(true);
            Debug.Log("[PortalAcquisition] Portal enabled via PortalAvailability.");
        }
        else
        {
            Debug.LogWarning("[PortalAcquisition] PortalAvailability not found in scene. Couldn't enable portal automatically.");
        }

        // Persist portal activation in save (optional)
        if (!string.IsNullOrEmpty(persistID) && SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.MarkObjectInteracted(persistID);
            SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.currentSaveSlot);
            Debug.Log($"[PortalAcquisition] Persisted portal interaction id='{persistID}'");
        }

        // One frame to allow other immediate systems to react (optional)
        yield return null;

        // Load the target scene immediately
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.Log($"[PortalAcquisition] Loading scene '{sceneToLoad}' upon contact.");
            SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogWarning("[PortalAcquisition] sceneToLoad is empty — not loading any scene.");
        }
    }
}

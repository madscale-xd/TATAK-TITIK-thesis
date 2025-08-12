using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class PortalAcquisition : MonoBehaviour
{
    [Header("Pickup")]
    public string playerTag = "Player";  // tag the player uses
    public float destroyDelay = 0.15f;   // small delay so effects can play

    [Tooltip("Scene name to load when the portal is picked up. Change in inspector.")]
    public string sceneToLoad = "TestScene";

    Vector3 startPosition;
    Collider myCollider;
    Renderer[] renderers;
    bool pickedUp = false;

    void Start()
    {
        startPosition = transform.position;
        myCollider = GetComponent<Collider>();
        if (myCollider != null) myCollider.isTrigger = true;
        renderers = GetComponentsInChildren<Renderer>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (pickedUp) return;
        if (!other.CompareTag(playerTag)) return;

        pickedUp = true;
        StartCoroutine(HandlePickupCoroutine());
    }

    private IEnumerator HandlePickupCoroutine()
    {
        // Try to ensure a valid save slot BEFORE enabling availability so the auto-save uses the correct slot
        var slm = SaveLoadManager.Instance;
        if (slm != null && slm.currentSaveSlot <= 0)
        {
            Debug.Log("[PortalAcquisition] No active save slot found. Using slot 1.");
            slm.currentSaveSlot = 1;
        }

        // Find the PortalAvailability in the current scene (not a singleton)
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

        // Optional: play a pickup VFX/SFX here (not included). Hide visuals & disable collider immediately:
        if (myCollider != null) myCollider.enabled = false;
        foreach (var r in renderers) r.enabled = false;

        // Wait a tick so any sounds/particles can play, then load the scene
        yield return new WaitForSeconds(destroyDelay);

        // Load the target scene (single mode). You can change this to LoadSceneAsync if you prefer.
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.Log($"[PortalAcquisition] Loading scene '{sceneToLoad}' after pickup.");
            SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogWarning("[PortalAcquisition] sceneToLoad is empty — not loading any scene.");
        }
    }
}

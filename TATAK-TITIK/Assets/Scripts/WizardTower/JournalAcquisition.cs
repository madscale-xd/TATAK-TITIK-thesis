using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class JournalAcquisition : MonoBehaviour
{
    [Header("Motion")]
    public float spinSpeedX = 120f;      // degrees per second around local X
    public float spinSpeedY = 90f;       // degrees per second around local Y
    public float bobAmplitude = 0.25f;   // vertical bob amount (meters)
    public float bobFrequency = 0.8f;    // cycles per second

    [Header("Pickup")]
    public string playerTag = "Player";  // tag the player uses
    public float destroyDelay = 0.15f;   // small delay so effects can play

    Vector3 startPosition;
    float bobPhase;
    bool pickedUp = false;
    Collider myCollider;
    Renderer[] renderers;

    void Start()
    {
        startPosition = transform.position;
        bobPhase = Random.Range(0f, Mathf.PI * 2f); // so multiple pickups look natural if present
        myCollider = GetComponent<Collider>();
        myCollider.isTrigger = true; // recommended; script requires trigger collider
        renderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        if (pickedUp) return;

        // Spin on local X and Y
        transform.Rotate(spinSpeedX * Time.deltaTime, spinSpeedY * Time.deltaTime, 0f, Space.Self);

        // Bob up and down (bobFrequency is in cycles per second)
        float y = startPosition.y + Mathf.Sin((Time.time + bobPhase) * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
        Vector3 p = transform.position;
        p.y = y;
        transform.position = p;
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
        // Dynamically look up JournalAvailability (FindObjectOfType first, fallback to Instance)
        JournalAvailability availability = FindObjectOfType<JournalAvailability>();
        if (availability == null)
            availability = JournalAvailability.Instance;

        // Try to ensure a valid save slot BEFORE enabling availability so the auto-save uses the correct slot
        var slm = SaveLoadManager.Instance;
        if (slm != null)
        {
            // If currentSaveSlot is not valid (<= 0), assign slot 1
            if (slm.currentSaveSlot <= 0)
            {
                Debug.Log("[JournalAcquisition] No active save slot found. Using slot 1.");
                slm.currentSaveSlot = 1;
            }
        }
        else
        {
            Debug.LogWarning("[JournalAcquisition] SaveLoadManager.Instance not found. Journal will still be enabled but automatic save might not occur.");
        }

        if (availability != null)
        {
            availability.SetAvailable(true);
            Debug.Log("[JournalAcquisition] Journal enabled via JournalAvailability.");
        }
        else
        {
            Debug.LogWarning("[JournalAcquisition] JournalAvailability not found in scene. Couldn't enable journal automatically.");
        }

        // Optional: play a pickup VFX/SFX here (not included). Hide visuals & disable collider immediately:
        if (myCollider != null) myCollider.enabled = false;
        foreach (var r in renderers) r.enabled = false;

        // Wait a tick so any sounds/particles can play, then destroy
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }
}

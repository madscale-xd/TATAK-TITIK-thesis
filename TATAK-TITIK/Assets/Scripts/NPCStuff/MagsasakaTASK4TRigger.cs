using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class MagsasakaTASK4TRigger : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("Name of the item required and expected to be EQUIPPED by the player.")]
    public string requiredItem = "Sombrero";
    public string interactionPrompt = "Press E to interact";

    [Tooltip("If true, one unit of the item will be consumed on successful interaction.")]
    public bool consumeItem = false;

    [Header("Visual Feedback (optional)")]
    public Color targetColor = Color.gray;
    public float colorChangeSpeed = 3f;

    [Header("Save / Events")]
    [SerializeField] private string customInteractableID = ""; // optional override
    public UnityEvent onSuccessfulInteraction;
    public UnityEvent onFailedInteraction;

    // runtime
    private bool playerNearby = false;
    private bool hasInteracted = false;
    private string interactableID;
    private Renderer[] renderers;
    private Color originalColor;
    private float transitionProgress = 0f;

    [SerializeField] NPCManager Magsasaka;
    [SerializeField] NavMeshNPCController MagCon;

    public string[] magsasaka2Lines;
    JournalTriggerEntry[] magsasaka2Journal =
        new JournalTriggerEntry[] { new JournalTriggerEntry { key = "salamat", displayWord = "ᜐᜎᜋᜆ᜔" }, };

    [SerializeField] GameObject SombreroHat;
    [SerializeField] BaybayinManager BayMan;
    [SerializeField] DayNightCycle DNC;
    void Start()
    {
        // generate persistent ID (can override with customInteractableID)
        if (!string.IsNullOrEmpty(customInteractableID))
            interactableID = customInteractableID;
        else
            interactableID = gameObject.scene.name + "_" + transform.position.ToString();

        // Early check: was this already interacted in a previous save?
        if (SaveLoadManager.Instance != null && SaveLoadManager.Instance.IsObjectInteracted(interactableID))
        {
            hasInteracted = true;
            // optionally apply final visual state immediately
        }

        // cache renderers for optional color feedback
        renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            originalColor = renderers[0].material.color;
            if (hasInteracted)
            {
                transitionProgress = 1f;
                foreach (var r in renderers)
                    if (r != null) r.material.color = Color.Lerp(originalColor, targetColor, 1f);
            }
        }

        // ensure collider is trigger for OnTriggerEnter/Exit (helpful when attached in editor)
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;
    }

    void Update()
    {
        // keypress-based interaction while near the NPC
        if (!hasInteracted && playerNearby && Input.GetKeyDown(KeyCode.E))
        {
            BayMan.MarkTaskStarted("task3");
            TryInteract();
        }

        // Visual color transition when interaction has occurred
        if (renderers != null && renderers.Length > 0)
        {
            if (hasInteracted && transitionProgress < 1f)
            {
                transitionProgress = Mathf.MoveTowards(transitionProgress, 1f, Time.deltaTime * colorChangeSpeed);
                foreach (var r in renderers)
                    if (r != null) r.material.color = Color.Lerp(originalColor, targetColor, transitionProgress);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerNearby = true;

        // show a small prompt (uses your FloatingNotifier if present)
        if (!hasInteracted)
        {
            if (FloatingNotifier.Instance != null)
                FloatingNotifier.Instance.ShowMessage(interactionPrompt, Color.white);
            else
                Debug.Log(interactionPrompt);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerNearby = false;

        // clear prompt — best-effort
        if (FloatingNotifier.Instance != null)
            FloatingNotifier.Instance.ShowMessage("", Color.clear);
    }

    public void TryInteract()
    {
        if (hasInteracted) return;

        // Safety: ensure InventoryManager exists
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("No InventoryManager found in scene.");
            if (FloatingNotifier.Instance != null)
                FloatingNotifier.Instance.ShowMessage("You can't interact right now.", Color.red);
            return;
        }

        // Check equipped item first (your requirement)
        if (InventoryManager.Instance.equippedItem != requiredItem)
        {
            // optional: check if player has item in inventory but not equipped
            InventoryItem found = InventoryManager.Instance.items.Find(i => i.itemName == requiredItem && i.quantity > 0);
            string msg = (found != null)
                ? $"You need to equip the {requiredItem} to interact with {gameObject.name}."
                : $"You need a {requiredItem} to interact with {gameObject.name}.";
            if (FloatingNotifier.Instance != null)
                FloatingNotifier.Instance.ShowMessage(msg, Color.red);
            else
                Debug.Log(msg);

            onFailedInteraction?.Invoke();
            return;
        }

        // At this point the required item is equipped. Optionally consume one unit.
        if (consumeItem)
        {
            InventoryItem equipped = InventoryManager.Instance.items.Find(i => i.itemName == requiredItem && i.quantity > 0);
            if (equipped != null)
            {
                equipped.quantity--;
                if (equipped.quantity <= 0)
                    InventoryManager.Instance.items.Remove(equipped);

                InventoryManager.Instance.inventoryUI?.UpdateInventoryUI();
            }
            else
            {
                // surprising: equipped but not in list -> treat as failure
                if (FloatingNotifier.Instance != null)
                    FloatingNotifier.Instance.ShowMessage($"No {requiredItem} found in inventory to consume.", Color.red);
                onFailedInteraction?.Invoke();
                return;
            }
        }

        // Success: perform the task-specific logic
        PerformInteraction();
    }

    private void PerformInteraction()
    {
        hasInteracted = true;

        // Example feedback
        if (FloatingNotifier.Instance != null)
            FloatingNotifier.Instance.ShowMessage($"You gave the {requiredItem} to {gameObject.name}. Task complete!", Color.white);
        else
            Debug.Log($"You gave the {requiredItem} to {gameObject.name}. Task complete!");

        SombreroHat.SetActive(true);
        Magsasaka.ChangeNPCID("Magsasaka2");
        Magsasaka.SetDialogueLines(magsasaka2Lines);
        Magsasaka.SetJournalEntries(magsasaka2Journal);
        Magsasaka.PlayDialogue("MAGSASAKA", magsasaka2Lines, magsasaka2Journal);
        BayMan.MarkTaskCompleted("task3");
        DNC.SetTimeOfDay(9f, 5f);
        BayMan.Task4();

        // persist the interaction
        if (SaveLoadManager.Instance != null)
            SaveLoadManager.Instance.MarkObjectInteracted(interactableID);

        // optional: if you want to treat it also as a pickup (depends on your save system)
        if (SaveLoadManager.Instance != null)
            SaveLoadManager.Instance.MarkPickupCollected(interactableID);

        // optional: you might want to disable/hide NPC or change its state here
        // gameObject.SetActive(false);

        // UnityEvent so designers can link further behaviour in the Inspector
        onSuccessfulInteraction?.Invoke();

        // begin color transition to "done" state
        transitionProgress = 0f;
    }

    public void KeepMagsasakaUpdated()
    {
        Magsasaka.ChangeNPCID("Magsasaka2");
        Magsasaka.SetDialogueLines(magsasaka2Lines);
        Magsasaka.SetJournalEntries(magsasaka2Journal);
    }
}

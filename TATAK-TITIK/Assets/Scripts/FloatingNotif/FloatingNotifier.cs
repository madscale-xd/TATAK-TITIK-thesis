using UnityEngine;

public class FloatingNotifier : MonoBehaviour
{
    public static FloatingNotifier Instance;

    [Header("Floating Message Prefab")]
    public FloatingMessage floatingMessagePrefab;

    [Header("Offset and Stacking")]
    public Vector3 spawnOffset = new Vector3(0, 2f, 0);
    public float verticalSpacing = 0.3f;

    private int messageCount = 0;

    private Transform playerTransform;

    private float lastMessageTime = 0f;
    public float messageCooldown = 0.1f;
    public int maxMessagesOnScreen = 5;


    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogWarning("FloatingNotifier: Player with tag 'Player' not found!");
        }
    }

    // No need to pass target now — uses its own position
   public void ShowMessage(string message, Color color)
    {
        if (Time.time - lastMessageTime < messageCooldown)
            return;

        if (messageCount >= maxMessagesOnScreen)
            return;

        lastMessageTime = Time.time;

        Vector3 spawnPosition = playerTransform.position + spawnOffset + Vector3.up * (verticalSpacing * messageCount);
        FloatingMessage newMsg = Instantiate(floatingMessagePrefab, spawnPosition, Quaternion.identity);
        newMsg.SetText(message, color);

        messageCount++;
        StartCoroutine(ResetMessageCountAfterDelay(0.5f));
    }

    private System.Collections.IEnumerator ResetMessageCountAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        messageCount = Mathf.Max(0, messageCount - 1);
    }
}

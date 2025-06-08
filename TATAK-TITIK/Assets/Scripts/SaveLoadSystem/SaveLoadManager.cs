using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SaveLoadManager : MonoBehaviour
{
    public Transform playerTransform;
    public int currentSaveSlot = 1; // Default slot to save/load (1-5)
    private const int maxSaveSlots = 5;

    private Vector3 positionToLoad;
    private bool shouldApplyPosition = false;

    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        DontDestroyOnLoad(gameObject); // Persist across scenes
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Helper: Find the player by tag
    private void AssignPlayerTransform()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogError("[SaveLoadManager] Player with tag 'Player' not found.");
        }
    }

    public void SaveGame(int slot)
    {
        if (slot < 1 || slot > maxSaveSlots)
        {
            Debug.LogError("Save slot out of range!");
            return;
        }

        AssignPlayerTransform();
        if (playerTransform == null)
        {
            Debug.LogError("Cannot save: playerTransform is null.");
            return;
        }

        SaveData data = new SaveData(playerTransform.position);
        SaveSystem.Save(data, slot);
        Debug.Log($"Game saved in slot {slot} at position {playerTransform.position}");
    }

    public void LoadGame(int slot)
    {
        if (slot < 1 || slot > maxSaveSlots)
        {
            Debug.LogError("Load slot out of range!");
            return;
        }

        SaveData data = SaveSystem.Load(slot);
        if (data != null)
        {
            positionToLoad = data.GetPosition();
            shouldApplyPosition = true;

            Debug.Log($"Loading scene for save slot {slot} with saved position {positionToLoad}");
        }
        else
        {
            Debug.LogWarning($"No save file found in slot {slot}. Starting fresh.");
            shouldApplyPosition = false; // Ensure nothing is applied
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene("TestScene");
    }

    public void ClearGame(int slot)
    {
        if (slot < 1 || slot > maxSaveSlots)
        {
            Debug.LogError("Clear slot out of range!");
            return;
        }

        SaveSystem.Delete(slot);
        Debug.Log($"Save slot {slot} has been cleared.");

        LoadGame(slot); // This will try to load, but if nothing exists, it'll just load the scene normally.
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (shouldApplyPosition)
        {
            StartCoroutine(SetPlayerPositionNextFrame());
        }
    }

    private IEnumerator SetPlayerPositionNextFrame()
    {
        yield return null;

        AssignPlayerTransform();
        if (playerTransform == null)
        {
            yield break;
        }

        CharacterController controller = playerTransform.GetComponent<CharacterController>();
        PlayerMovement movement = playerTransform.GetComponent<PlayerMovement>();

        if (movement != null)
            movement.enabled = false;

        if (controller != null)
            controller.enabled = false;

        playerTransform.position = positionToLoad;
        Debug.Log($"[SaveLoadManager] Position restored to {positionToLoad}");

        yield return null;

        if (controller != null)
            controller.enabled = true;

        if (movement != null)
            movement.enabled = true;

        shouldApplyPosition = false;
    }

    // Button helper methods
    public void SaveSlot1() => SaveGame(1);
    public void LoadSlot1() => LoadGame(1);

    public void SaveSlot2() => SaveGame(2);
    public void LoadSlot2() => LoadGame(2);

    public void SaveSlot3() => SaveGame(3);
    public void LoadSlot3() => LoadGame(3);

    public void SaveSlot4() => SaveGame(4);
    public void LoadSlot4() => LoadGame(4);

    public void SaveSlot5() => SaveGame(5);
    public void LoadSlot5() => LoadGame(5);

    public void ClearSlot1() => ClearGame(1);
    public void ClearSlot2() => ClearGame(2);
    public void ClearSlot3() => ClearGame(3);
    public void ClearSlot4() => ClearGame(4);
    public void ClearSlot5() => ClearGame(5);

}

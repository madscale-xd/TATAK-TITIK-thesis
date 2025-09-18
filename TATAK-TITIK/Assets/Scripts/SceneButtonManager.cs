using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneButtonManager : MonoBehaviour
{
    public GameObject SAVEPanel;
    public GameObject JournalPanel;
    public GameObject JournalButton;
    public GameObject JournalPrev;
    public GameObject JournalNext;
    public GameObject InventoryPanel;
    public GameObject EXITPanel;
    [SerializeField] private DialogueManager dialogueManager;
    private bool jKeyEnabled = true;
    private bool escKeyEnabled = true;
    private bool eKeyEnabled = true;
    private bool isMainMenuScene = false;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Subscribe if the JournalAvailability instance already exists
        if (JournalAvailability.Instance != null)
            JournalAvailability.Instance.OnAvailabilityChanged += OnJournalAvailabilityChanged;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (JournalAvailability.Instance != null)
            JournalAvailability.Instance.OnAvailabilityChanged -= OnJournalAvailabilityChanged;
    }

    private void OnJournalAvailabilityChanged(bool available)
    {
        if (JournalButton != null)
            JournalButton.SetActive(available);
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isMainMenuScene = scene.name == "MainMenu";

        DeactivateIfValid(SAVEPanel);
        DeactivateIfValid(JournalPanel);
        DeactivateIfValid(EXITPanel);

        if (isMainMenuScene)
        {
            DeactivateIfValid(InventoryPanel);
        }
        else
        {
            ActivateIfValid(InventoryPanel);
        }

        EnableJKey();
        EnableEscKey();
        EnableEKey();
    }


    public void GoToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void ToggleExitPanel()
    {
        if (isMainMenuScene) return;

        // In ToggleExitPanel(), before calling ForceHidePrompt():
        if (dialogueManager != null && !dialogueManager.IsDialogueVisible())
        {
            dialogueManager.ForceHidePrompt();
        }

        bool isNowActive = !EXITPanel.activeSelf;

        EXITPanel.SetActive(isNowActive);

        // Set journal button per availability every time exit is shown/hidden
        if (JournalButton != null)
        {
            bool shouldShow = JournalAvailability.Instance != null && JournalAvailability.Instance.IsAvailable();
            JournalButton.SetActive(shouldShow);
        }
        
        DisableEKey();

        DeactivateIfValid(InventoryPanel);

        Time.timeScale = isNowActive ? 0f : 1f;

        if (Time.timeScale == 1f)
        {
            dialogueManager.RefreshPromptIfNeeded();
            ToggleIfValid(InventoryPanel);
            EnableJKey();
            EnableEscKey();
            EnableEKey();
        }
    }


    public void ToggleSavePanel()
    {
        if (isMainMenuScene) return;

        ToggleIfValid(SAVEPanel);
        ToggleIfValid(EXITPanel);
        DeactivateIfValid(InventoryPanel);
        DisableJKey();
    }

    public void ToggleJournalPanel()
    {
        if (JournalAvailability.Instance != null && !JournalAvailability.Instance.IsAvailable())
        {
            Debug.Log("Journal is not available yet.");
            return;
        }

        if (isMainMenuScene) return;

        if (dialogueManager != null && !dialogueManager.IsDialogueVisible())
        dialogueManager.ForceHidePrompt();
        ToggleIfValid(JournalPanel);
        ToggleIfValid(EXITPanel);
        DeactivateIfValid(InventoryPanel);
        EnableJKey();
        DisableEKey();

        // Handle Journal Navigation Buttons
        bool isNowActive = JournalPanel.activeSelf;
        if (JournalPrev != null) JournalPrev.SetActive(isNowActive);
        if (JournalNext != null) JournalNext.SetActive(isNowActive);
    }

    public void ToggleJournalPanelJ()
    {
        if (JournalAvailability.Instance != null && !JournalAvailability.Instance.IsAvailable())
        {
            Debug.Log("Journal is not available yet.");
            return;
        }
        
        if (isMainMenuScene) return;

        bool isNowActive = !JournalPanel.activeSelf;
        JournalPanel.SetActive(isNowActive);

        // Handle Journal Navigation Buttons
        if (JournalPrev != null) JournalPrev.SetActive(isNowActive);
        if (JournalNext != null) JournalNext.SetActive(isNowActive);

        if (isNowActive)
        {
            if (dialogueManager != null && !dialogueManager.IsDialogueVisible())
            dialogueManager.ForceHidePrompt();
            Time.timeScale = 0f;
            DeactivateIfValid(InventoryPanel);
            DisableEKey();
        }
        else
        {
            dialogueManager.RefreshPromptIfNeeded();
            Time.timeScale = 1f;
            ToggleIfValid(InventoryPanel);
            EnableJKey();
            EnableEscKey();
            EnableEKey();
        }
    }

    public void ToggleInventoryPanel()
    {
        if (isMainMenuScene) return;

        ToggleIfValid(InventoryPanel);
        ToggleIfValid(EXITPanel);
    }

    public void BackToExitFromSaveLoad()
    {
        if (isMainMenuScene) return;

        ToggleIfValid(EXITPanel);
        ToggleIfValid(SAVEPanel);
        EnableEscKey();
        EnableEKey();
    }

    // Key toggles
    public void DisableJKey() => jKeyEnabled = false;
    public void EnableJKey() => jKeyEnabled = true;
    public void DisableEscKey() => escKeyEnabled = false;
    public void EnableEscKey() => escKeyEnabled = true;
    public void DisableEKey() => eKeyEnabled = false;
    public void EnableEKey() => eKeyEnabled = true;

    public bool IsJKeyEnabled() => jKeyEnabled;
    public bool IsEscKeyEnabled() => escKeyEnabled;
    public bool IsEKeyEnabled() => eKeyEnabled;

    private void ToggleIfValid(GameObject panel)
    {
        if (panel != null)
            panel.SetActive(!panel.activeSelf);
    }

    private void DeactivateIfValid(GameObject panel)
    {
        if (panel != null && panel.activeSelf)
            panel.SetActive(false);
    }
    private void ActivateIfValid(GameObject panel)
    {
        if (panel != null && !panel.activeSelf)
            panel.SetActive(true);
    }
}

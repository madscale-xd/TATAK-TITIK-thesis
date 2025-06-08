using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneButtonManager : MonoBehaviour
{
    public GameObject SAVEPanel;
    public GameObject JournalPanel;
    public GameObject InventoryPanel;
    public GameObject EXITPanel;

    private bool jKeyEnabled = true;
    private bool escKeyEnabled = true;

    public void GoToMainMenu()
    {
        SceneManager.LoadScene("MainMenu"); // Replace with your actual main menu scene name
    }

    public void ToggleExitPanel()           // RESUME / PAUSE GAME
    {
        bool isNowActive = !EXITPanel.activeSelf;
        EXITPanel.SetActive(isNowActive);
        DisableJKey();

        DeactivateIfValid(InventoryPanel);

        Time.timeScale = isNowActive ? 0f : 1f;

        if (Time.timeScale == 1f)
        {
            ToggleIfValid(InventoryPanel);
            EnableJKey();
            EnableEscKey();
        }
    }

    public void ToggleSavePanel()           //PAUSE to SAVELOAD
    {
        ToggleIfValid(SAVEPanel);
        ToggleIfValid(EXITPanel);
        DeactivateIfValid(InventoryPanel);
        DisableEscKey();
        DisableJKey();
    }

    public void ToggleJournalPanel()        //PAUSE to JOURNAL
    {
        ToggleIfValid(JournalPanel);
        ToggleIfValid(EXITPanel);
        DeactivateIfValid(InventoryPanel);
        DisableEscKey();
        EnableJKey();
    }

    public void ToggleJournalPanelJ() // JOURNAL to RESUME / PAUSE
    {
        bool isNowActive = !JournalPanel.activeSelf;
        JournalPanel.SetActive(isNowActive);

        if (isNowActive)
        {
            // Pausing game
            Time.timeScale = 0f;
            DeactivateIfValid(InventoryPanel);
            DisableEscKey();
        }
        else
        {
            // Resuming game
            Time.timeScale = 1f;
            ToggleIfValid(InventoryPanel);
            EnableJKey();
            EnableEscKey();
        }
    }

    public void ToggleInventoryPanel()
    {
        ToggleIfValid(InventoryPanel);
        ToggleIfValid(EXITPanel);
    }

    public void BackToExitFromSaveLoad()    //SAVELOAD to PAUSE
    {
        ToggleIfValid(EXITPanel);
        ToggleIfValid(SAVEPanel);
        EnableEscKey();
    }

    // New methods to enable/disable J key
    public void DisableJKey()
    {
        jKeyEnabled = false;
    }

    public void EnableJKey()
    {
        jKeyEnabled = true;
    }

    // New methods to enable/disable ESC key
    public void DisableEscKey()
    {
        escKeyEnabled = false;
    }

    public void EnableEscKey()
    {
        escKeyEnabled = true;
    }

    // Call this method from your input check/update method to test if key is allowed
    public bool IsJKeyEnabled()
    {
        return jKeyEnabled;
    }

    public bool IsEscKeyEnabled()
    {
        return escKeyEnabled;
    }

    private void ToggleIfValid(GameObject panel)
    {
        if (panel != null)
        {
            panel.SetActive(!panel.activeSelf);
        }
        else
        {
            Debug.LogWarning("Panel reference is missing in the inspector.");
        }
    }

    private void DeactivateIfValid(GameObject panel)
    {
        if (panel != null && panel.activeSelf)
        {
            panel.SetActive(false);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuButtonManager : MonoBehaviour
{
    public GameObject MenuPanel;
    public GameObject SettingsPanel;
    public GameObject SaveLoadPanel;
    public void QuitGame()
    {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void MainMenuSaveLoad()   
    {
        ToggleIfValid(MenuPanel);
        ToggleIfValid(SaveLoadPanel);
    }

    public void MainMenuSettings()    
    {
        ToggleIfValid(MenuPanel);
        ToggleIfValid(SettingsPanel);
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
}

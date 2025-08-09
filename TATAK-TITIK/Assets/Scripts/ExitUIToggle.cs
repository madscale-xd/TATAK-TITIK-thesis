using UnityEngine;

public class ExitUIToggle : MonoBehaviour
{
    private SceneButtonManager sbm;

    void Start()
    {
        sbm = FindObjectOfType<SceneButtonManager>();
    }

    void Update()
    {
        if (sbm == null || !sbm.IsEscKeyEnabled()) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            GameObject savePanel = sbm.SAVEPanel;
            GameObject journalPanel = sbm.JournalPanel;
            GameObject exitPanel = sbm.EXITPanel;

            if (savePanel != null && savePanel.activeSelf)
            {
                savePanel.SetActive(false);
                if (exitPanel != null) exitPanel.SetActive(true);
                sbm.EnableEscKey();
                Time.timeScale = 0f; // keep paused when exit panel shows
            }
            else if (journalPanel != null && journalPanel.activeSelf)
            {
                journalPanel.SetActive(false);
                if (sbm.JournalPrev != null) sbm.JournalPrev.SetActive(false);
                if (sbm.JournalNext != null) sbm.JournalNext.SetActive(false);
                if (exitPanel != null) exitPanel.SetActive(true);
                sbm.EnableEscKey();
                sbm.EnableJKey();
                sbm.EnableEKey();
                Time.timeScale = 0f; // keep paused
            }
            else
            {
                sbm.ToggleExitPanel();

                // Pause or unpause depending on panel state
                if (exitPanel != null && exitPanel.activeSelf)
                {
                    Time.timeScale = 0f; // pause
                }
                else
                {
                    Time.timeScale = 1f; // resume
                }
            }
        }
    }
}

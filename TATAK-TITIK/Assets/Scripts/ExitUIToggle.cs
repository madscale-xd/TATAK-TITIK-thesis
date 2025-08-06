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
            }
            else if (journalPanel != null && journalPanel.activeSelf)
            {
                journalPanel.SetActive(false);
                if (sbm.JournalPrev != null) sbm.JournalPrev.SetActive(false);
                if (sbm.JournalNext != null) sbm.JournalNext.SetActive(false);
                // Time stays paused
                if (exitPanel != null) exitPanel.SetActive(true);
                sbm.EnableEscKey();
                sbm.EnableJKey();
                sbm.EnableEKey();
            }
            else
            {
                sbm.ToggleExitPanel();
            }
        }
    }
}

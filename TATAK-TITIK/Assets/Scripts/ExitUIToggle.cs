using UnityEngine;

public class ExitUIToggle : MonoBehaviour
{
    private SceneButtonManager SBM;
    void Start()
    {
    }
    void Update()
    {
        SceneButtonManager sbm = FindObjectOfType<SceneButtonManager>();
        if (sbm.IsEscKeyEnabled() && Input.GetKeyDown(KeyCode.Escape))
        {
            sbm.ToggleExitPanel();
        }
    }
}

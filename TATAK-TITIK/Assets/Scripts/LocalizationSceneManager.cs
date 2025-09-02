using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class LocalizationToEnglishArray : MonoBehaviour
{
    public static LocalizationToEnglishArray Instance;

    [Header("Name of the Baybayin scene (default 'Baybayin')")]
    public string baybayinSceneName = "Baybayin";

    [Header("Assign TMP elements you want changed (index matches englishTexts)")]
    public TextMeshProUGUI[] targets;

    [Header("English replacements (same length/order as targets). Leave entry empty to skip that target.")]
    [TextArea(1, 3)]
    public string[] englishTexts;

    // cached originals (by index)
    private string[] originalTexts;

    private void Awake()
    {
        // singleton behavior
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // ensure arrays are safe and cache originals
        int len = Mathf.Max( (targets != null ? targets.Length : 0), (englishTexts != null ? englishTexts.Length : 0) );
        originalTexts = new string[len];

        // If englishTexts shorter than targets, we allow it but treat missing entries as empty (skip).
        for (int i = 0; i < len; i++)
        {
            if (targets != null && i < targets.Length && targets[i] != null)
                originalTexts[i] = targets[i].text ?? string.Empty;
            else
                originalTexts[i] = string.Empty;
        }

        // subscribe to scene loading and apply for current scene
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyForScene(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyForScene(scene);
    }

    private void ApplyForScene(Scene scene)
    {
        bool isBaybayin = string.Equals(scene.name, baybayinSceneName, System.StringComparison.Ordinal);
        if (isBaybayin) RestoreOriginals();
        else ApplyEnglish();
    }

    private void RestoreOriginals()
    {
        if (targets == null) return;
        int len = Mathf.Min(targets.Length, originalTexts.Length);
        for (int i = 0; i < len; i++)
        {
            if (targets[i] == null) continue;
            targets[i].text = originalTexts[i];
        }
    }

    private void ApplyEnglish()
    {
        if (targets == null) return;
        int len = Mathf.Min(targets.Length, englishTexts != null ? englishTexts.Length : 0);
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;

            // if there is an english entry for this index and it's not empty, apply it
            if (i < len && !string.IsNullOrEmpty(englishTexts[i]))
            {
                targets[i].text = englishTexts[i];
            }
            // else: skip — do not overwrite with empty string
        }
    }

    // Public helpers for testing or runtime control
    public void ForceApplyEnglishNow() => ApplyEnglish();
    public void ForceRestoreOriginalsNow() => RestoreOriginals();
}

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Sources")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;

    [Header("BGM")]
    public AudioClip mainMenuBGM;
    public AudioClip gameBGM;
    public AudioClip gameOverBGM;

    [Header("SFX")]

    public AudioClip buttonSFX;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            sfxSource.ignoreListenerPause = true;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private IEnumerator StopAudioAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        bgmSource.Stop();
        sfxSource.Stop();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Stop old audio immediately, not with delay
        bgmSource.Stop();
        sfxSource.Stop();

        if (scene.name == "MainMenu")
            PlayBGM(mainMenuBGM);
        else if (scene.name == "TestScene")
            PlayBGM(gameBGM);
    }

    public void PlayBGM(AudioClip clip)
    {
        if (bgmSource.clip == clip) return;
        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        sfxSource.PlayOneShot(clip);
    }

    // Separated SFX methods
}
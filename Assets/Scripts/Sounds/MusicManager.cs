using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    private AudioSource[] audioSources;

    [SerializeField] private string targetScene = "MainMenu";

    private void Awake()
    {
        audioSources = GetComponents<AudioSource>();
        UpdateAudioForScene(SceneManager.GetActiveScene().name);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopAllAudio();
    }

    private void OnDestroy()
    {
        StopAllAudio();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateAudioForScene(scene.name);
    }

    private void UpdateAudioForScene(string sceneName)
    {
        if (sceneName == targetScene)
            PlayAllAudio();
        else
            StopAllAudio();
    }

    private void PlayAllAudio()
    {
        if (audioSources == null || audioSources.Length == 0)
            audioSources = GetComponents<AudioSource>();

        foreach (AudioSource source in audioSources)
        {
            if (source == null || source.isPlaying)
                continue;

            source.Play();
        }
    }

    private void StopAllAudio()
    {
        if (audioSources == null)
            return;

        foreach (AudioSource source in audioSources)
        {
            if (source == null)
                continue;

            source.Stop();
        }
    }
}

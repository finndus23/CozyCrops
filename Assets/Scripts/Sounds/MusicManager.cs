using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    private static MusicManager instance;
    private AudioSource audioSource;

    [SerializeField] private string targetScene = "MainMenu";

    private void Awake()
    {
        // Prüfen ob schon ein Manager existiert
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        // Objekt zwischen Szenen behalten
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Nur in bestimmter Szene Musik abspielen
        if (scene.name == targetScene)
        {
            audioSource.Play();
        }
        else
        {
            audioSource.Stop();
        }
    }
}
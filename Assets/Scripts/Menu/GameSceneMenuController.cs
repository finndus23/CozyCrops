using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameSceneMenuController : MonoBehaviour
{
    [Header("Optional UI")]
    [SerializeField] private GameObject pausePanel;

    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Behaviour")]
    [SerializeField] private bool saveBeforeReturningToMenu = true;
    [SerializeField] private bool pauseWithEscape = true;
    [SerializeField] private bool freezeTimeWhenPaused = true;

    private bool isPaused;

    private void Start()
    {
        SetPaused(false);
    }

    private void Update()
    {
        if (!pauseWithEscape)
            return;

        if (WasEscapePressed())
            TogglePauseMenu();
    }

    private bool WasEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    public void TogglePauseMenu()
    {
        SetPaused(!isPaused);
    }

    public void ResumeGame()
    {
        SetPaused(false);
    }

    public void SaveGame()
    {
        if (FarmSaveManager.Instance != null)
        {
            FarmSaveManager.Instance.SaveNow();
            return;
        }

        Debug.LogWarning("[GameSceneMenuController] Kein FarmSaveManager gefunden. Save nicht möglich.");
    }

    public void LoadGame()
    {
        if (FarmSaveManager.Instance != null)
        {
            FarmSaveManager.Instance.LoadNow();
            return;
        }

        Debug.LogWarning("[GameSceneMenuController] Kein FarmSaveManager gefunden. Load nicht möglich.");
    }

    public void BackToMainMenu()
    {
        Time.timeScale = 1f;

        if (saveBeforeReturningToMenu)
            SaveGame();

        SceneLoadingScreen.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;

        if (FarmSaveManager.Instance != null)
            FarmSaveManager.Instance.SaveNow();

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetPaused(bool paused)
    {
        isPaused = paused;

        if (pausePanel != null)
            pausePanel.SetActive(isPaused);

        if (freezeTimeWhenPaused)
            Time.timeScale = isPaused ? 0f : 1f;
    }
}

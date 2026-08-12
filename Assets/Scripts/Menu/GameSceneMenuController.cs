using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameSceneMenuController : MonoBehaviour
{
    public static GameSceneMenuController Instance { get; private set; }
    public bool IsConfirmationOpen { get; private set; }

    [Header("Optional UI")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private Sprite confirmationPanelSprite;
    [SerializeField] private Sprite normalButtonSprite;
    [SerializeField] private Sprite confirmButtonSprite;

    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Behaviour")]
    [SerializeField] private bool saveBeforeReturningToMenu = true;
    [SerializeField] private bool pauseWithEscape = true;
    [SerializeField] private bool freezeTimeWhenPaused = true;

    private bool isPaused;
    private GameObject confirmationRoot;
    private float timeScaleBeforeConfirmation = 1f;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        SetPaused(false);
        CreateConfirmationPopup();
    }

    private void Update()
    {
        if (!pauseWithEscape)
            return;

        if (WasEscapePressed())
        {
            if (IsConfirmationOpen)
                CancelBackToMainMenu();
            else
                TogglePauseMenu();
        }
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

    public void RequestBackToMainMenu()
    {
        if (confirmationRoot == null)
            CreateConfirmationPopup();

        if (confirmationRoot == null)
        {
            BackToMainMenu();
            return;
        }

        timeScaleBeforeConfirmation = Time.timeScale;
        IsConfirmationOpen = true;
        confirmationRoot.SetActive(true);
        Time.timeScale = 0f;
    }

    public void CancelBackToMainMenu()
    {
        IsConfirmationOpen = false;
        if (confirmationRoot != null)
            confirmationRoot.SetActive(false);
        Time.timeScale = timeScaleBeforeConfirmation;
    }

    private void CreateConfirmationPopup()
    {
        if (confirmationRoot != null) return;

        Canvas canvas = hudCanvas != null ? hudCanvas : FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        confirmationRoot = CreateUiObject("ReturnToMenuConfirmation", canvas.transform);
        confirmationRoot.transform.SetAsLastSibling();

        Canvas popupCanvas = confirmationRoot.AddComponent<Canvas>();
        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = 100;
        confirmationRoot.AddComponent<GraphicRaycaster>();

        RectTransform rootRect = confirmationRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image blocker = confirmationRoot.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.5f);
        blocker.raycastTarget = true;

        GameObject panel = CreateUiObject("ConfirmationPanel", confirmationRoot.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(500f, 245f);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.sprite = confirmationPanelSprite;
        panelImage.type = confirmationPanelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        panelImage.color = confirmationPanelSprite != null ? Color.white : new Color(1f, 0.86f, 0.62f, 1f);

        GameObject questionObject = CreateUiObject("Question", panel.transform);
        RectTransform questionRect = questionObject.GetComponent<RectTransform>();
        questionRect.anchorMin = new Vector2(0.08f, 0.48f);
        questionRect.anchorMax = new Vector2(0.92f, 0.9f);
        questionRect.offsetMin = questionRect.offsetMax = Vector2.zero;

        TextMeshProUGUI question = questionObject.AddComponent<TextMeshProUGUI>();
        question.text = "Willst du wirklich zum Menü zurückkehren?";
        question.fontSize = 25f;
        question.fontStyle = FontStyles.Bold;
        question.color = new Color(0.22f, 0.12f, 0.06f, 1f);
        question.alignment = TextAlignmentOptions.Center;
        question.textWrappingMode = TextWrappingModes.Normal;
        question.raycastTarget = false;

        CreateConfirmationButton(panel.transform, "Confirm", new Vector2(-95f, -70f), "JA",
            confirmButtonSprite, BackToMainMenu);
        CreateConfirmationButton(panel.transform, "Cancel", new Vector2(95f, -70f), "ABBRECHEN",
            normalButtonSprite, CancelBackToMainMenu);

        confirmationRoot.SetActive(false);
    }

    private void CreateConfirmationButton(Transform parent, string objectName, Vector2 position,
        string label, Sprite sprite, UnityEngine.Events.UnityAction action)
    {
        GameObject go = CreateUiObject(objectName, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(165f, 58f);

        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        colors.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
        button.colors = colors;
        button.onClick.AddListener(action);

        GameObject textObject = CreateUiObject("Text", go.transform);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 19f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.22f, 0.12f, 0.06f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject go = new(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
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

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoadingScreen : MonoBehaviour
{
    private const float DefaultMinimumDisplayTime = 0.35f;

    private static SceneLoadingScreen instance;

    private GameObject overlay;
    private bool isLoading;

    private static SceneLoadingScreen Instance
    {
        get
        {
            if (instance != null)
                return instance;

            GameObject loaderObject = new GameObject("Scene Loading Screen");
            instance = loaderObject.AddComponent<SceneLoadingScreen>();
            DontDestroyOnLoad(loaderObject);
            return instance;
        }
    }

    public static void LoadScene(string sceneName)
    {
        Instance.StartCoroutine(Instance.LoadSceneInternal(sceneName, null, DefaultMinimumDisplayTime));
    }

    public static IEnumerator LoadSceneRoutine(string sceneName, Action<bool> onComplete = null)
    {
        yield return Instance.LoadSceneInternal(sceneName, onComplete, DefaultMinimumDisplayTime);
    }

    private IEnumerator LoadSceneInternal(string sceneName, Action<bool> onComplete, float minimumDisplayTime)
    {
        if (isLoading)
        {
            Debug.LogWarning("[SceneLoadingScreen] Es wird bereits eine Scene geladen.");
            onComplete?.Invoke(false);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[SceneLoadingScreen] Kein Scene-Name gesetzt.");
            onComplete?.Invoke(false);
            yield break;
        }

        isLoading = true;
        Show();

        float shownAt = Time.realtimeSinceStartup;
        yield return null;

        AsyncOperation operation = null;

        try
        {
            operation = SceneManager.LoadSceneAsync(sceneName);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SceneLoadingScreen] Scene '{sceneName}' konnte nicht geladen werden: {ex.Message}");
        }

        if (operation == null)
        {
            Hide();
            isLoading = false;
            onComplete?.Invoke(false);
            yield break;
        }

        while (!operation.isDone)
            yield return null;

        float remainingDisplayTime = minimumDisplayTime - (Time.realtimeSinceStartup - shownAt);
        if (remainingDisplayTime > 0f)
            yield return new WaitForSecondsRealtime(remainingDisplayTime);

        Hide();
        isLoading = false;
        onComplete?.Invoke(true);
    }

    private void Show()
    {
        if (overlay != null)
        {
            overlay.SetActive(true);
            return;
        }

        overlay = new GameObject("Loading Overlay");
        DontDestroyOnLoad(overlay);

        Canvas canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        overlay.AddComponent<CanvasScaler>();
        overlay.AddComponent<GraphicRaycaster>();

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(overlay.transform, false);

        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0.08f, 0.11f, 0.1f, 1f);

        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        GameObject textObject = new GameObject("Loading Text");
        textObject.transform.SetParent(overlay.transform, false);

        Text text = textObject.AddComponent<Text>();
        text.text = "Loading...";
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 44;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void Hide()
    {
        if (overlay != null)
            overlay.SetActive(false);
    }
}

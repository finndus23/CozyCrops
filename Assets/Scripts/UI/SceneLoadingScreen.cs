using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoadingScreen : MonoBehaviour
{
    private const float DefaultMinimumDisplayTime = 1.5f;

    private static SceneLoadingScreen instance;

    private GameObject overlay;
    private Image loadingImage;
    private LoadingScreenAssets assets;
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

        string targetSceneName = ResolveSceneName(sceneName);
        string sourceSceneName = ResolveSceneName(SceneManager.GetActiveScene().name);
        Sprite loadingSprite = GetLoadingSprite(sourceSceneName, targetSceneName);

        isLoading = true;
        Show(loadingSprite);

        float shownAt = Time.realtimeSinceStartup;
        yield return null;

        AsyncOperation operation = null;

        try
        {
            operation = SceneManager.LoadSceneAsync(targetSceneName);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SceneLoadingScreen] Scene '{targetSceneName}' konnte nicht geladen werden: {ex.Message}");
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

    private void Show(Sprite loadingSprite)
    {
        if (overlay != null)
        {
            overlay.SetActive(true);
            SetLoadingImage(loadingSprite);
            return;
        }

        assets = Resources.Load<LoadingScreenAssets>("LoadingScreenAssets");

        overlay = new GameObject("Loading Overlay");
        DontDestroyOnLoad(overlay);

        Canvas canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = overlay.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
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

        GameObject imageObject = new GameObject("Loading Image");
        imageObject.transform.SetParent(overlay.transform, false);

        loadingImage = imageObject.AddComponent<Image>();
        loadingImage.color = Color.white;
        loadingImage.preserveAspect = false;
        loadingImage.raycastTarget = false;

        RectTransform imageRect = loadingImage.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        GameObject logoObject = new GameObject("Cozy Crops Logo");
        logoObject.transform.SetParent(overlay.transform, false);

        Image logoImage = logoObject.AddComponent<Image>();
        logoImage.sprite = assets != null ? assets.logo : null;
        logoImage.preserveAspect = true;
        logoImage.raycastTarget = false;
        logoImage.color = logoImage.sprite != null ? Color.white : Color.clear;

        RectTransform logoRect = logoImage.GetComponent<RectTransform>();
        logoRect.anchorMin = new Vector2(0f, 1f);
        logoRect.anchorMax = new Vector2(0f, 1f);
        logoRect.pivot = new Vector2(0f, 1f);
        logoRect.anchoredPosition = new Vector2(28f, -24f);
        logoRect.sizeDelta = new Vector2(260f, 130f);

        GameObject textObject = new GameObject("Loading Text");
        textObject.transform.SetParent(overlay.transform, false);

        Text text = textObject.AddComponent<Text>();
        text.text = "Loading...";
        text.alignment = TextAnchor.LowerRight;
        text.fontSize = 46;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(1f, 0f);
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(1f, 0f);
        textRect.offsetMin = new Vector2(-420f, 26f);
        textRect.offsetMax = new Vector2(-32f, 120f);

        SetLoadingImage(loadingSprite);
    }

    private void Hide()
    {
        if (overlay != null)
            overlay.SetActive(false);
    }

    private Sprite GetLoadingSprite(string sourceSceneName, string targetSceneName)
    {
        if (assets == null)
            assets = Resources.Load<LoadingScreenAssets>("LoadingScreenAssets");

        if (assets == null)
            return null;

        if (IsScene(sourceSceneName, "Marketplace") && IsScene(targetSceneName, "SampleScene"))
            return assets.toFarmImage;

        if (IsScene(sourceSceneName, "SampleScene") && IsScene(targetSceneName, "Marketplace"))
            return assets.toMarketImage;

        if (IsScene(sourceSceneName, "MainMenu") && IsScene(targetSceneName, "SampleScene"))
            return assets.homeToFarmImage;

        return assets.homeToFarmImage != null ? assets.homeToFarmImage : assets.toFarmImage;
    }

    private void SetLoadingImage(Sprite sprite)
    {
        if (loadingImage == null) return;

        loadingImage.sprite = sprite;
        loadingImage.enabled = sprite != null;
    }

    private static string ResolveSceneName(string sceneName)
    {
        if (string.Equals(sceneName, "GameScene", StringComparison.OrdinalIgnoreCase))
            return "SampleScene";

        if (string.Equals(sceneName, "MarketScene", StringComparison.OrdinalIgnoreCase))
            return "Marketplace";

        return sceneName;
    }

    private static bool IsScene(string sceneName, string expected)
    {
        return string.Equals(ResolveSceneName(sceneName), expected, StringComparison.OrdinalIgnoreCase);
    }

}

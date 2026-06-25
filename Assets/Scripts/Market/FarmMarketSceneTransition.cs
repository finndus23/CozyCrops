using UnityEngine;

public class FarmMarketSceneTransition : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string farmSceneName = "SampleScene";
    [SerializeField] private string marketSceneName = "Marketplace";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Save")]
    [SerializeField] private bool saveBeforeSceneChange = true;

    public void GoToMarket()
    {
        SaveBeforeChange();
        SceneLoadingScreen.LoadScene(marketSceneName);
    }

    public void GoToFarm()
    {
        SaveBeforeChange();
        SceneLoadingScreen.LoadScene(farmSceneName);
    }

    public void GoToMainMenu()
    {
        SaveBeforeChange();
        SceneLoadingScreen.LoadScene(mainMenuSceneName);
    }

    private void SaveBeforeChange()
    {
        if (!saveBeforeSceneChange)
            return;

        if (FarmSaveManager.Instance == null)
        {
            Debug.LogWarning("[FarmMarketSceneTransition] Kein FarmSaveManager gefunden. Szenenwechsel ohne Save.");
            return;
        }

        FarmSaveManager.Instance.SaveNow();
        Debug.Log("[FarmMarketSceneTransition] Vor Szenenwechsel gespeichert.");
    }
}

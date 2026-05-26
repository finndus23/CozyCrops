using UnityEngine;
using UnityEngine.SceneManagement;

public class FarmMarketSceneTransition : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string farmSceneName = "GameScene";
    [SerializeField] private string marketSceneName = "MarketScene";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Save")]
    [SerializeField] private bool saveBeforeSceneChange = true;

    public void GoToMarket()
    {
        SaveBeforeChange();
        SceneManager.LoadScene(marketSceneName);
    }

    public void GoToFarm()
    {
        SaveBeforeChange();
        SceneManager.LoadScene(farmSceneName);
    }

    public void GoToMainMenu()
    {
        SaveBeforeChange();
        SceneManager.LoadScene(mainMenuSceneName);
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
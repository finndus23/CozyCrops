using UnityEngine;

/// <summary>
/// Einziger Weg, wie die Szene zwischen Farm und Marktplatz wechselt — egal ob der Spieler
/// aufs Auto klickt (<see cref="CarClickHandler"/>) oder einen UI-Button dafür benutzt.
///
/// OnTraveledToFarmStatic/OnTraveledToMarketStatic saßen vorher direkt in CarClickHandler
/// und feuerten nur beim Autoklick. Ein UI-Button, der stattdessen direkt GoToFarm()/
/// GoToMarket() aufruft, löste den Szenenwechsel zwar korrekt aus, aber die beiden Events
/// nie — TravelToMarket/TravelToFarm-Missionsziele blieben also hängen, und TravelSfx blieb
/// stumm. Die Events gehören an den EINEN Ort, durch den jeder Wechsel garantiert läuft.
/// </summary>
public class FarmMarketSceneTransition : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string farmSceneName = "SampleScene";
    [SerializeField] private string marketSceneName = "Marketplace";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Save")]
    [SerializeField] private bool saveBeforeSceneChange = true;

    public static event System.Action OnTraveledToMarketStatic;
    public static event System.Action OnTraveledToFarmStatic;

    public void GoToMarket()
    {
        OnTraveledToMarketStatic?.Invoke();
        SaveBeforeChange();
        SceneLoadingScreen.LoadScene(marketSceneName);
    }

    public void GoToFarm()
    {
        OnTraveledToFarmStatic?.Invoke();
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
